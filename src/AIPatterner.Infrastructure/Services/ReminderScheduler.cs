// Service implementation for scheduling reminder candidates
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Application.Handlers;
using AIPatterner.Domain.Entities;
using AIPatterner.Domain.Services;
using AIPatterner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class ReminderScheduler : IReminderScheduler
{
    private readonly ApplicationDbContext _context;
    private readonly AIPatterner.Application.Handlers.ITransitionRepository _transitionRepository;
    private readonly IReminderPolicyEvaluator _policyEvaluator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReminderScheduler> _logger;

    public ReminderScheduler(
        ApplicationDbContext context,
        AIPatterner.Application.Handlers.ITransitionRepository transitionRepository,
        IReminderPolicyEvaluator policyEvaluator,
        IConfiguration configuration,
        ILogger<ReminderScheduler> logger)
    {
        _context = context;
        _transitionRepository = transitionRepository;
        _policyEvaluator = policyEvaluator;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<ReminderCandidate>> ScheduleCandidatesForEventAsync(
        ActionEvent actionEvent,
        CancellationToken cancellationToken)
    {
        var candidates = new List<ReminderCandidate>();

        var recentTransitions = await _transitionRepository.GetRecentTransitionsForPersonAsync(
            actionEvent.PersonId,
            actionEvent.ActionType,
            cancellationToken);

        var preferences = await _context.UserReminderPreferences
            .FirstOrDefaultAsync(p => p.PersonId == actionEvent.PersonId, cancellationToken);

        if (preferences == null)
        {
            preferences = new UserReminderPreferences(actionEvent.PersonId);
            await _context.UserReminderPreferences.AddAsync(preferences, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var defaultConfidence = _configuration.GetValue<double>("Policy:DefaultReminderConfidence", 0.5);
        var confidenceStep = _configuration.GetValue<double>("Policy:ConfidenceStepValue", 0.1);
        var timeOffsetMinutes = _configuration.GetValue<int>("Policy:ReminderMatchTimeOffsetMinutes", 30);

        foreach (var transition in recentTransitions)
        {
            var policyDecision = await _policyEvaluator.EvaluateAsync(
                transition,
                actionEvent.Context,
                preferences,
                cancellationToken);

            if (policyDecision?.ShouldSchedule == true)
            {
                // Check for existing matching reminder
                var timeOffset = TimeSpan.FromMinutes(timeOffsetMinutes);
                var existingReminder = await _context.ReminderCandidates
                    .Where(c => 
                        c.PersonId == actionEvent.PersonId &&
                        c.SuggestedAction == transition.ToAction &&
                        c.Status == ReminderCandidateStatus.Scheduled &&
                        Math.Abs((c.CheckAtUtc - policyDecision.SuggestedCheckAt).TotalMinutes) <= timeOffsetMinutes)
                    .FirstOrDefaultAsync(cancellationToken);

                if (existingReminder != null)
                {
                    // Increase confidence of existing reminder
                    existingReminder.IncreaseConfidence(confidenceStep);
                    _context.ReminderCandidates.Update(existingReminder);
                    await _context.SaveChangesAsync(cancellationToken);
                    candidates.Add(existingReminder);
                    _logger.LogInformation(
                        "Increased confidence of existing reminder {CandidateId} for {PersonId}, action: {Action}, new confidence: {Confidence}",
                        existingReminder.Id, actionEvent.PersonId, transition.ToAction, existingReminder.Confidence);
                }
                else
                {
                    // Create new reminder with default confidence
                    var candidate = new ReminderCandidate(
                        actionEvent.PersonId,
                        transition.ToAction,
                        policyDecision.SuggestedCheckAt,
                        policyDecision.Style,
                        transition.Id,
                        defaultConfidence);

                    await _context.ReminderCandidates.AddAsync(candidate, cancellationToken);
                    candidates.Add(candidate);
                    _logger.LogInformation(
                        "Scheduled reminder candidate {CandidateId} for {PersonId}, action: {Action}, check at: {CheckAt}, confidence: {Confidence}",
                        candidate.Id, actionEvent.PersonId, transition.ToAction, policyDecision.SuggestedCheckAt, candidate.Confidence);
                }
            }
        }

        if (candidates.Any())
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return candidates;
    }
}

