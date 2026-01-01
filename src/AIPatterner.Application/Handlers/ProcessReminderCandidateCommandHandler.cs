// MediatR handler for processing reminder candidates
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.Commands;
using AIPatterner.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;

public class ProcessReminderCandidateCommandHandler : IRequestHandler<ProcessReminderCandidateCommand, ProcessReminderCandidateResponse>
{
    private readonly IReminderCandidateRepository _candidateRepository;
    private readonly IReminderEvaluationService _evaluationService;
    private readonly INotificationService _notificationService;
    private readonly IMemoryGateway _memoryGateway;
    private readonly IExecutionHistoryService _executionHistoryService;
    private readonly IConfiguration _configuration;

    public ProcessReminderCandidateCommandHandler(
        IReminderCandidateRepository candidateRepository,
        IReminderEvaluationService evaluationService,
        INotificationService notificationService,
        IMemoryGateway memoryGateway,
        IExecutionHistoryService executionHistoryService,
        IConfiguration configuration)
    {
        _candidateRepository = candidateRepository;
        _evaluationService = evaluationService;
        _notificationService = notificationService;
        _memoryGateway = memoryGateway;
        _executionHistoryService = executionHistoryService;
        _configuration = configuration;
    }

    public async Task<ProcessReminderCandidateResponse> Handle(ProcessReminderCandidateCommand request, CancellationToken cancellationToken)
    {
        var candidate = await _candidateRepository.GetByIdAsync(request.CandidateId, cancellationToken);
        if (candidate == null)
        {
            throw new InvalidOperationException($"ReminderCandidate {request.CandidateId} not found");
        }

        if (!request.BypassDateCheck && !candidate.IsDue(DateTime.UtcNow))
        {
            return new ProcessReminderCandidateResponse
            {
                Executed = false,
                ShouldSpeak = false,
                Reason = "Candidate is not yet due"
            };
        }

        // Check if high confidence and should auto-execute
        var minProbabilityForExecution = _configuration.GetValue<double>("Policy:MinimumProbabilityForExecution", 0.7);
        var shouldAutoExecute = candidate.Confidence >= minProbabilityForExecution;

        // Low probability reminders should NOT be executed automatically
        // They can only be executed manually via "Execute now" button (BypassDateCheck = true)
        if (!request.BypassDateCheck && candidate.Confidence < minProbabilityForExecution)
        {
            return new ProcessReminderCandidateResponse
            {
                Executed = false,
                ShouldSpeak = false,
                Reason = $"Low probability reminder (confidence: {candidate.Confidence:P0} < threshold: {minProbabilityForExecution:P0}). Only high-probability reminders are auto-executed."
            };
        }

        var decision = await _evaluationService.EvaluateAsync(candidate, cancellationToken);

        // Auto-execute if high confidence, otherwise use decision
        var shouldExecute = shouldAutoExecute || decision.ShouldSpeak;

        if (shouldExecute)
        {
            candidate.MarkAsExecuted(decision);
            await _candidateRepository.UpdateAsync(candidate, cancellationToken);

            // Record execution history with CustomData
            var requestPayload = System.Text.Json.JsonSerializer.Serialize(new
            {
                candidateId = candidate.Id,
                personId = candidate.PersonId,
                suggestedAction = candidate.SuggestedAction,
                confidence = candidate.Confidence,
                checkAtUtc = candidate.CheckAtUtc,
                customData = candidate.CustomData,
                sourceEventId = candidate.SourceEventId
            });

            var responsePayload = System.Text.Json.JsonSerializer.Serialize(new
            {
                executed = true,
                shouldSpeak = decision.ShouldSpeak,
                naturalLanguagePhrase = decision.NaturalLanguagePhrase,
                reason = decision.Reason,
                autoExecuted = shouldAutoExecute
            });

            await _executionHistoryService.RecordExecutionAsync(
                "/api/v1/admin/force-check",
                requestPayload,
                responsePayload,
                DateTime.UtcNow,
                candidate.PersonId,
                null,
                candidate.SuggestedAction,
                candidate.Id,
                null,
                cancellationToken);

            if (decision.ShouldSpeak)
            {
                await _notificationService.SendReminderAsync(candidate, decision, cancellationToken);
            }

            var summary = await _evaluationService.GenerateMemorySummaryAsync(candidate, decision, cancellationToken);
            await _memoryGateway.PushSummaryAsync(summary, cancellationToken);

            return new ProcessReminderCandidateResponse
            {
                Executed = true,
                ShouldSpeak = decision.ShouldSpeak,
                NaturalLanguagePhrase = decision.NaturalLanguagePhrase,
                Reason = shouldAutoExecute ? $"Auto-executed (confidence: {candidate.Confidence:P0})" : decision.Reason
            };
        }
        else
        {
            candidate.MarkAsSkipped();
            await _candidateRepository.UpdateAsync(candidate, cancellationToken);

            // Record skipped execution
            var requestPayload = System.Text.Json.JsonSerializer.Serialize(new
            {
                candidateId = candidate.Id,
                personId = candidate.PersonId,
                suggestedAction = candidate.SuggestedAction,
                confidence = candidate.Confidence
            });

            var responsePayload = System.Text.Json.JsonSerializer.Serialize(new
            {
                executed = true,
                shouldSpeak = false,
                reason = decision.Reason
            });

            await _executionHistoryService.RecordExecutionAsync(
                "/api/v1/admin/force-check",
                requestPayload,
                responsePayload,
                DateTime.UtcNow,
                candidate.PersonId,
                null,
                candidate.SuggestedAction,
                candidate.Id,
                null,
                cancellationToken);

            return new ProcessReminderCandidateResponse
            {
                Executed = true,
                ShouldSpeak = false,
                Reason = decision.Reason
            };
        }
    }
}

// Interfaces to be implemented in Infrastructure
public interface IReminderEvaluationService
{
    Task<ReminderDecision> EvaluateAsync(ReminderCandidate candidate, CancellationToken cancellationToken);
    Task<string> GenerateMemorySummaryAsync(ReminderCandidate candidate, ReminderDecision decision, CancellationToken cancellationToken);
}

public interface INotificationService
{
    Task SendReminderAsync(ReminderCandidate candidate, ReminderDecision decision, CancellationToken cancellationToken);
}

public interface IMemoryGateway
{
    Task PushSummaryAsync(string summary, CancellationToken cancellationToken);
}

