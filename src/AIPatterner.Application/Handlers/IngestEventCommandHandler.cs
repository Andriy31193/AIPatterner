// MediatR handler for ingesting action events
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Application.Helpers;
using AIPatterner.Application.Mappings;
using AIPatterner.Application.Services;
using AIPatterner.Domain.Entities;
using AIPatterner.Domain.Services;
using AutoMapper;
using MediatR;
using Microsoft.Extensions.Configuration;

public class IngestEventCommandHandler : IRequestHandler<IngestEventCommand, IngestEventResponse>
{
    private readonly IEventRepository _eventRepository;
    private readonly ITransitionLearner _transitionLearner;
    private readonly IReminderScheduler _reminderScheduler;
    private readonly IReminderCandidateRepository _reminderRepository;
    private readonly IMapper _mapper;
    private readonly IExecutionHistoryService _executionHistoryService;
    private readonly IConfiguration _configuration;
    private readonly IMatchingRemindersService _matchingService;
    private readonly IMatchingPolicyService _policyService;

    public IngestEventCommandHandler(
        IEventRepository eventRepository,
        ITransitionLearner transitionLearner,
        IReminderScheduler reminderScheduler,
        IReminderCandidateRepository reminderRepository,
        IMapper mapper,
        IExecutionHistoryService executionHistoryService,
        IConfiguration configuration,
        IMatchingRemindersService matchingService,
        IMatchingPolicyService policyService)
    {
        _eventRepository = eventRepository;
        _transitionLearner = transitionLearner;
        _reminderScheduler = reminderScheduler;
        _reminderRepository = reminderRepository;
        _mapper = mapper;
        _executionHistoryService = executionHistoryService;
        _configuration = configuration;
        _matchingService = matchingService;
        _policyService = policyService;
    }

    public async Task<IngestEventResponse> Handle(IngestEventCommand request, CancellationToken cancellationToken)
    {
        var actionEvent = _mapper.Map<ActionEvent>(request.Event);
        await _eventRepository.AddAsync(actionEvent, cancellationToken);

        await _transitionLearner.UpdateTransitionsAsync(actionEvent, cancellationToken);

        // Find matching reminder using strict policy-based matching
        Guid? relatedReminderId = null;
        if (request.Event.ProbabilityValue.HasValue && request.Event.ProbabilityAction.HasValue)
        {
            // Get matching policies from configuration
            var matchingCriteria = await _policyService.GetMatchingCriteriaAsync(cancellationToken);
            
            // Find matching reminders using strict criteria
            var matchingResult = await _matchingService.FindMatchingRemindersAsync(
                actionEvent.Id,
                matchingCriteria,
                cancellationToken);

            ReminderCandidate? matchingReminder = null;
            if (matchingResult.Items.Any())
            {
                // Get the best matching reminder (highest confidence, most recent)
                var bestMatch = matchingResult.Items
                    .OrderByDescending(r => r.Confidence)
                    .ThenByDescending(r => r.CheckAtUtc)
                    .First();
                
                matchingReminder = await _reminderRepository.GetByIdAsync(bestMatch.Id, cancellationToken);
            }

            if (matchingReminder != null)
            {
                // Update existing reminder probability (increase/decrease based on event)
                matchingReminder.UpdateConfidence(
                    request.Event.ProbabilityValue.Value,
                    request.Event.ProbabilityAction.Value);
                
                // CheckAtUtc must always be identical to Event TimestampUtc
                matchingReminder.UpdateCheckAtUtc(actionEvent.TimestampUtc);
                
                // Regenerate occurrence from the new CheckAtUtc
                var newOccurrence = OccurrenceGenerator.GenerateOccurrence(actionEvent.TimestampUtc);
                matchingReminder.SetOccurrence(newOccurrence);
                
                // Update CustomData if provided
                if (actionEvent.CustomData != null)
                {
                    matchingReminder.UpdateCustomData(actionEvent.CustomData);
                }
                
                await _reminderRepository.UpdateAsync(matchingReminder, cancellationToken);
                relatedReminderId = matchingReminder.Id;
                actionEvent.SetRelatedReminder(matchingReminder.Id);
                await _eventRepository.UpdateAsync(actionEvent, cancellationToken);
            }
            else
            {
                // No matching reminder found - create new one
                var defaultConfidence = _configuration.GetValue<double>("Policy:DefaultReminderConfidence", 0.5);
                
                // CheckAtUtc must be identical to Event TimestampUtc
                var checkAtUtc = actionEvent.TimestampUtc;
                
                // Auto-generate Occurrence from CheckAtUtc
                var occurrence = OccurrenceGenerator.GenerateOccurrence(checkAtUtc);
                
                var newReminder = new ReminderCandidate(
                    actionEvent.PersonId,
                    actionEvent.ActionType,
                    checkAtUtc, // Use event timestamp
                    ReminderStyle.Suggest,
                    null,
                    defaultConfidence,
                    occurrence, // Auto-generated occurrence
                    actionEvent.Id, // SourceEventId
                    actionEvent.CustomData); // Copy CustomData

                await _reminderRepository.AddAsync(newReminder, cancellationToken);
                relatedReminderId = newReminder.Id;
                actionEvent.SetRelatedReminder(newReminder.Id);
                await _eventRepository.UpdateAsync(actionEvent, cancellationToken);
            }
        }

        // Continue with existing scheduler logic (for transition-based reminders)
        var scheduledCandidates = await _reminderScheduler.ScheduleCandidatesForEventAsync(
            actionEvent, cancellationToken);

        // Record execution history for event ingestion
        var requestJson = System.Text.Json.JsonSerializer.Serialize(request.Event);
        var responseJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            eventId = actionEvent.Id,
            scheduledCandidateIds = scheduledCandidates.Select(c => c.Id).ToList(),
            relatedReminderId = relatedReminderId
        });

        await _executionHistoryService.RecordExecutionAsync(
            "/api/v1/events",
            requestJson,
            responseJson,
            DateTime.UtcNow,
            actionEvent.PersonId,
            null,
            actionEvent.ActionType,
            null,
            actionEvent.Id,
            cancellationToken);

        return new IngestEventResponse
        {
            EventId = actionEvent.Id,
            ScheduledCandidateIds = scheduledCandidates.Select(c => c.Id).ToList(),
            RelatedReminderId = relatedReminderId
        };
    }
}

// Interface for event repository (to be implemented in Infrastructure)
public interface IEventRepository
{
    Task AddAsync(ActionEvent actionEvent, CancellationToken cancellationToken);
    Task UpdateAsync(ActionEvent actionEvent, CancellationToken cancellationToken);
    Task<ActionEvent?> GetLastEventForPersonAsync(string personId, DateTime beforeUtc, CancellationToken cancellationToken);
    Task<ActionEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task DeleteAsync(ActionEvent actionEvent, CancellationToken cancellationToken);
    Task<List<ActionEvent>> GetFilteredAsync(
        string? personId,
        string? actionType,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<int> GetCountAsync(
        string? personId,
        string? actionType,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken);
}

// Interface for reminder scheduler (to be implemented in Infrastructure)
public interface IReminderScheduler
{
    Task<List<ReminderCandidate>> ScheduleCandidatesForEventAsync(ActionEvent actionEvent, CancellationToken cancellationToken);
}

