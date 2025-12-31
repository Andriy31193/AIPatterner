// Service implementation for evaluating reminder candidates
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Application.Handlers;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class ReminderEvaluationService : IReminderEvaluationService
{
    private readonly ApplicationDbContext _context;
    private readonly AIPatterner.Application.Handlers.ICooldownService _cooldownService;
    private readonly IContextService _contextService;
    private readonly ILLMClient _llmClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReminderEvaluationService> _logger;

    public ReminderEvaluationService(
        ApplicationDbContext context,
        AIPatterner.Application.Handlers.ICooldownService cooldownService,
        IContextService contextService,
        ILLMClient llmClient,
        IConfiguration configuration,
        ILogger<ReminderEvaluationService> logger)
    {
        _context = context;
        _cooldownService = cooldownService;
        _contextService = contextService;
        _llmClient = llmClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ReminderDecision> EvaluateAsync(ReminderCandidate candidate, CancellationToken cancellationToken)
    {
        var preferences = await _context.UserReminderPreferences
            .FirstOrDefaultAsync(p => p.PersonId == candidate.PersonId, cancellationToken);

        if (preferences == null || !preferences.Enabled)
        {
            return new ReminderDecision(
                false,
                "User preferences disabled",
                0.0);
        }

        var isCooldownActive = await _cooldownService.IsCooldownActiveAsync(
            candidate.PersonId,
            candidate.SuggestedAction,
            cancellationToken);

        if (isCooldownActive)
        {
            return new ReminderDecision(
                false,
                "Cooldown period active",
                0.0);
        }

        var todayCount = await _context.ReminderCandidates
            .CountAsync(c =>
                c.PersonId == candidate.PersonId &&
                c.Status == ReminderCandidateStatus.Executed &&
                c.ExecutedAtUtc.HasValue &&
                c.ExecutedAtUtc.Value.Date == DateTime.UtcNow.Date,
                cancellationToken);

        if (todayCount >= preferences.DailyLimit)
        {
            return new ReminderDecision(
                false,
                $"Daily limit reached: {todayCount}/{preferences.DailyLimit}",
                0.0);
        }

        var lastReminder = await _context.ReminderCandidates
            .Where(c =>
                c.PersonId == candidate.PersonId &&
                c.Status == ReminderCandidateStatus.Executed &&
                c.ExecutedAtUtc.HasValue)
            .OrderByDescending(c => c.ExecutedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastReminder?.ExecutedAtUtc.HasValue == true)
        {
            var timeSinceLast = DateTime.UtcNow - lastReminder.ExecutedAtUtc.Value;
            if (timeSinceLast < preferences.MinimumInterval)
            {
                return new ReminderDecision(
                    false,
                    $"Minimum interval not met: {timeSinceLast.TotalMinutes:F1} min < {preferences.MinimumInterval.TotalMinutes} min",
                    0.0);
            }
        }

        var currentContext = await _contextService.GetCurrentContextAsync(candidate.PersonId, cancellationToken);
        var interruptionCost = await _contextService.EvaluateInterruptionCostAsync(currentContext, cancellationToken);

        if (interruptionCost > _configuration.GetValue<double>("Policy:MaxInterruptionCost", 0.7))
        {
            return new ReminderDecision(
                false,
                $"Interruption cost too high: {interruptionCost:F2}",
                0.0);
        }

        var confidence = 0.7;
        if (candidate.TransitionId.HasValue)
        {
            var transition = await _context.ActionTransitions.FindAsync(
                new object[] { candidate.TransitionId.Value }, cancellationToken);
            if (transition != null)
            {
                confidence = transition.Confidence;
            }
        }

        var naturalLanguagePhrase = await _llmClient.GeneratePhraseAsync(
            candidate.SuggestedAction,
            candidate.PersonId,
            cancellationToken);

        return new ReminderDecision(
            true,
            "All checks passed",
            confidence,
            null,
            naturalLanguagePhrase);
    }

    public async Task<string> GenerateMemorySummaryAsync(
        ReminderCandidate candidate,
        ReminderDecision decision,
        CancellationToken cancellationToken)
    {
        var transition = candidate.TransitionId.HasValue
            ? await _context.ActionTransitions.FindAsync(
                new object[] { candidate.TransitionId.Value }, cancellationToken)
            : null;

        if (transition == null)
        {
            return $"{candidate.PersonId} was reminded about {candidate.SuggestedAction}.";
        }

        var confidenceLabel = transition.Confidence >= 0.7 ? "high" :
                              transition.Confidence >= 0.4 ? "medium" : "low";

        return $"{candidate.PersonId} often performs {transition.ToAction} after {transition.FromAction} " +
               $"in context {transition.ContextBucket} (confidence: {confidenceLabel}). " +
               $"Suggested reminder at {candidate.CheckAtUtc:HH:mm} UTC.";
    }
}

// Interfaces to be implemented
public interface IContextService
{
    Task<ActionContext> GetCurrentContextAsync(string personId, CancellationToken cancellationToken);
    Task<double> EvaluateInterruptionCostAsync(ActionContext context, CancellationToken cancellationToken);
}

public interface ILLMClient
{
    Task<string?> GeneratePhraseAsync(string action, string personId, CancellationToken cancellationToken);
}

