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
    private readonly IRoutineLearningService _routineLearningService;
    private readonly AIPatterner.Domain.Services.ISignalSelector _signalSelector;
    private readonly AIPatterner.Application.Services.ISignalPolicyService _signalPolicyService;

    public IngestEventCommandHandler(
        IEventRepository eventRepository,
        ITransitionLearner transitionLearner,
        IReminderScheduler reminderScheduler,
        IReminderCandidateRepository reminderRepository,
        IMapper mapper,
        IExecutionHistoryService executionHistoryService,
        IConfiguration configuration,
        IMatchingRemindersService matchingService,
        IMatchingPolicyService policyService,
        IRoutineLearningService routineLearningService,
        AIPatterner.Domain.Services.ISignalSelector signalSelector,
        AIPatterner.Application.Services.ISignalPolicyService signalPolicyService)
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
        _routineLearningService = routineLearningService;
        _signalSelector = signalSelector;
        _signalPolicyService = signalPolicyService;
    }

    public async Task<IngestEventResponse> Handle(IngestEventCommand request, CancellationToken cancellationToken)
    {
        // Set default probability values if not provided (default: 0.1, Increase)
        const double defaultProbabilityValue = 0.1;
        const ProbabilityAction defaultProbabilityAction = ProbabilityAction.Increase;
        
        if (!request.Event.ProbabilityValue.HasValue)
        {
            request.Event.ProbabilityValue = defaultProbabilityValue;
        }
        
        if (!request.Event.ProbabilityAction.HasValue)
        {
            request.Event.ProbabilityAction = defaultProbabilityAction;
        }
        
        var actionEvent = _mapper.Map<ActionEvent>(request.Event);
        await _eventRepository.AddAsync(actionEvent, cancellationToken);

        // CRITICAL: StateChange events (intents) must NOT go through transition learning or reminder scheduling
        // They only activate routines
        if (actionEvent.EventType != EventType.StateChange)
        {
            await _transitionLearner.UpdateTransitionsAsync(actionEvent, cancellationToken);
        }

        Guid? relatedReminderId = null;

        // CRITICAL: StateChange events (intents) must NEVER match general reminders
        // They only activate routines and open observation windows
        if (actionEvent.EventType == EventType.StateChange)
        {
            // Handle intent event - open observation window
            await _routineLearningService.HandleIntentAsync(actionEvent, cancellationToken);
            
            // StateChange events do NOT create or match general reminders
            // Skip the general reminder matching logic below
        }
        else
        {
            // Regular Action event - check if it falls within any open observation windows
            // Convert SignalStateDto to SignalState first
            List<AIPatterner.Domain.ValueObjects.SignalState>? signalStates = null;
            if (request.Event.SignalStates != null && request.Event.SignalStates.Count > 0)
            {
                signalStates = new List<AIPatterner.Domain.ValueObjects.SignalState>();
                foreach (var dto in request.Event.SignalStates)
                {
                    // Convert JsonElement value to object
                    object? value = null;
                    if (dto.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        value = dto.Value.GetString();
                    }
                    else if (dto.Value.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        if (dto.Value.TryGetDouble(out var doubleVal))
                        {
                            value = doubleVal;
                        }
                        else if (dto.Value.TryGetInt32(out var intVal))
                        {
                            value = intVal;
                        }
                    }
                    else if (dto.Value.ValueKind == System.Text.Json.JsonValueKind.True)
                    {
                        value = true;
                    }
                    else if (dto.Value.ValueKind == System.Text.Json.JsonValueKind.False)
                    {
                        value = false;
                    }
                    
                    signalStates.Add(new AIPatterner.Domain.ValueObjects.SignalState
                    {
                        SensorId = dto.SensorId,
                        Value = value ?? string.Empty,
                        RawImportance = dto.RawImportance
                    });
                }
            }
            
            // Regular Action event - check if it falls within any open observation windows
            await _routineLearningService.ProcessObservedEventAsync(actionEvent, request.Event.UserPrompt, signalStates, cancellationToken);

            // Find matching reminder using strict policy-based matching (only for Action events)
            // Probability values are now guaranteed to be set (defaults applied above)
            // Get matching policies from configuration
            var matchingCriteria = await _policyService.GetMatchingCriteriaAsync(cancellationToken);
            
            // Find matching reminders using strict criteria
            var matchingResult = await _matchingService.FindMatchingRemindersAsync(
                actionEvent.Id,
                matchingCriteria,
                signalStates,
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
                // Use the values from request (which now have defaults if not provided)
                matchingReminder.UpdateConfidence(
                    request.Event.ProbabilityValue!.Value,
                    request.Event.ProbabilityAction!.Value);
                
                // Record new evidence for this matching event with context information
                // This accumulates evidence across days without locking into a specific day/weekday
                matchingReminder.RecordEvidence(
                    actionEvent.TimestampUtc,
                    actionEvent.Context.TimeBucket,
                    actionEvent.Context.DayType);
                
                // Update CheckAtUtc to the most recent event (for scheduling purposes)
                // But note: matching is now based on TimeWindowCenter, not CheckAtUtc
                matchingReminder.UpdateCheckAtUtc(actionEvent.TimestampUtc);
                
                // Re-evaluate pattern inference based on accumulated evidence
                // This gradually infers Daily/Weekly patterns only when there's enough evidence
                var minDailyEvidence = _configuration.GetValue<int>("Policy:MinDailyEvidence", 3);
                var minWeeklyEvidence = _configuration.GetValue<int>("Policy:MinWeeklyEvidence", 3);
                matchingReminder.UpdateInferredPattern(minDailyEvidence, minWeeklyEvidence);
                
                // Update CustomData if provided
                if (actionEvent.CustomData != null)
                {
                    matchingReminder.UpdateCustomData(actionEvent.CustomData);
                }
                
                // Append userPrompt if provided
                if (!string.IsNullOrWhiteSpace(request.Event.UserPrompt))
                {
                    matchingReminder.AppendUserPrompt(request.Event.UserPrompt, actionEvent.TimestampUtc);
                }
                
                // Update signal profile baseline if signal selection is enabled and event has signals
                var isSignalSelectionEnabled = await _signalPolicyService.IsSignalSelectionEnabledAsync(cancellationToken);
                if (isSignalSelectionEnabled && signalStates != null && signalStates.Count > 0)
                {
                    var selectionLimit = await _signalPolicyService.GetSignalSelectionLimitAsync(cancellationToken);
                    var eventProfile = _signalSelector.SelectAndNormalizeSignals(signalStates, selectionLimit);
                    var alpha = await _signalPolicyService.GetSignalProfileUpdateAlphaAsync(cancellationToken);
                    matchingReminder.UpdateSignalProfile(eventProfile, alpha);
                }
                
                await _reminderRepository.UpdateAsync(matchingReminder, cancellationToken);
                relatedReminderId = matchingReminder.Id;
                actionEvent.SetRelatedReminder(matchingReminder.Id);
                await _eventRepository.UpdateAsync(actionEvent, cancellationToken);
            }
            else
            {
                // No matching reminder found - check if one exists for this person+action
                // to avoid creating duplicates (prefer scheduled reminders within time window)
                var existingReminders = await _reminderRepository.GetByPersonAndActionAsync(
                    actionEvent.PersonId,
                    actionEvent.ActionType,
                    cancellationToken);
                
                // Prefer scheduled reminders that are within time window, otherwise any scheduled reminder
                var timeWindowSize = 45; // minutes
                ReminderCandidate? existingReminder = null;
                
                if (existingReminders.Any())
                {
                    // First try to find one within time window
                    var eventTimeOfDay = actionEvent.TimestampUtc.TimeOfDay;
                    existingReminder = existingReminders
                        .Where(r => r.Status == ReminderCandidateStatus.Scheduled && r.TimeWindowCenter.HasValue)
                        .Where(r =>
                        {
                            var reminderTime = r.TimeWindowCenter!.Value;
                            var timeDiff = Math.Abs((eventTimeOfDay - reminderTime).TotalMinutes);
                            if (timeDiff > 12 * 60) timeDiff = 24 * 60 - timeDiff;
                            return timeDiff <= timeWindowSize;
                        })
                        .OrderByDescending(r => r.CreatedAtUtc)
                        .FirstOrDefault();
                    
                    // If none in time window, use any scheduled reminder
                    if (existingReminder == null)
                    {
                        existingReminder = existingReminders
                            .Where(r => r.Status == ReminderCandidateStatus.Scheduled)
                            .OrderByDescending(r => r.CreatedAtUtc)
                            .FirstOrDefault();
                    }
                }
                
                if (existingReminder != null)
                {
                    // Update existing reminder instead of creating duplicate
                    existingReminder.UpdateConfidence(
                        request.Event.ProbabilityValue!.Value,
                        request.Event.ProbabilityAction!.Value);
                    
                    // Record new evidence with context
                    existingReminder.RecordEvidence(
                        actionEvent.TimestampUtc,
                        actionEvent.Context.TimeBucket,
                        actionEvent.Context.DayType);
                    
                    existingReminder.UpdateCheckAtUtc(actionEvent.TimestampUtc);
                    
                    // Update CustomData if provided
                    if (actionEvent.CustomData != null)
                    {
                        existingReminder.UpdateCustomData(actionEvent.CustomData);
                    }
                    
                    // Append userPrompt if provided
                    if (!string.IsNullOrWhiteSpace(request.Event.UserPrompt))
                    {
                        existingReminder.AppendUserPrompt(request.Event.UserPrompt, actionEvent.TimestampUtc);
                    }
                    
                    // Re-evaluate pattern inference
                    var minDailyEvidence = _configuration.GetValue<int>("Policy:MinDailyEvidence", 3);
                    var minWeeklyEvidence = _configuration.GetValue<int>("Policy:MinWeeklyEvidence", 3);
                    existingReminder.UpdateInferredPattern(minDailyEvidence, minWeeklyEvidence);
                    
                    // Update signal profile baseline if signal selection is enabled and event has signals
                    var isSignalSelectionEnabled = await _signalPolicyService.IsSignalSelectionEnabledAsync(cancellationToken);
                    if (isSignalSelectionEnabled && signalStates != null && signalStates.Count > 0)
                    {
                        var selectionLimit = await _signalPolicyService.GetSignalSelectionLimitAsync(cancellationToken);
                        var eventProfile = _signalSelector.SelectAndNormalizeSignals(signalStates, selectionLimit);
                        var alpha = await _signalPolicyService.GetSignalProfileUpdateAlphaAsync(cancellationToken);
                        existingReminder.UpdateSignalProfile(eventProfile, alpha);
                    }
                    
                    await _reminderRepository.UpdateAsync(existingReminder, cancellationToken);
                    relatedReminderId = existingReminder.Id;
                    actionEvent.SetRelatedReminder(existingReminder.Id);
                    await _eventRepository.UpdateAsync(actionEvent, cancellationToken);
                }
                else
                {
                    // No existing reminder - create new one as a hypothesis
                    var defaultConfidence = _configuration.GetValue<double>("Policy:DefaultReminderConfidence", 0.25);
                    
                    // CheckAtUtc must be identical to Event TimestampUtc (for scheduling)
                    var checkAtUtc = actionEvent.TimestampUtc;
                    
                    // DO NOT set occurrence immediately - it starts as Unknown/Flexible
                    // The ReminderCandidate constructor will initialize evidence tracking
                    // Occurrence will be inferred gradually as evidence accumulates
                    var newReminder = new ReminderCandidate(
                        actionEvent.PersonId,
                        actionEvent.ActionType,
                        checkAtUtc, // Use event timestamp
                        ReminderStyle.Suggest,
                        null,
                        defaultConfidence,
                        occurrence: null, // Start with no fixed occurrence pattern
                        actionEvent.Id, // SourceEventId
                        actionEvent.CustomData); // Copy CustomData
                    
                    // Record the first evidence with context information
                    newReminder.RecordEvidence(
                        actionEvent.TimestampUtc,
                        actionEvent.Context.TimeBucket,
                        actionEvent.Context.DayType);
                    
                    // The pattern inference is run to set initial status
                    var minDailyEvidence = _configuration.GetValue<int>("Policy:MinDailyEvidence", 3);
                    var minWeeklyEvidence = _configuration.GetValue<int>("Policy:MinWeeklyEvidence", 3);
                    newReminder.UpdateInferredPattern(minDailyEvidence, minWeeklyEvidence);
                    
                    // Initialize signal profile baseline if signal selection is enabled and event has signals
                    var isSignalSelectionEnabled = await _signalPolicyService.IsSignalSelectionEnabledAsync(cancellationToken);
                    if (isSignalSelectionEnabled && signalStates != null && signalStates.Count > 0)
                    {
                        var selectionLimit = await _signalPolicyService.GetSignalSelectionLimitAsync(cancellationToken);
                        var eventProfile = _signalSelector.SelectAndNormalizeSignals(signalStates, selectionLimit);
                        var alpha = await _signalPolicyService.GetSignalProfileUpdateAlphaAsync(cancellationToken);
                        newReminder.UpdateSignalProfile(eventProfile, alpha);
                    }

                    await _reminderRepository.AddAsync(newReminder, cancellationToken);
                    relatedReminderId = newReminder.Id;
                    actionEvent.SetRelatedReminder(newReminder.Id);
                    await _eventRepository.UpdateAsync(actionEvent, cancellationToken);
                }
            }
        }

        // Continue with existing scheduler logic (for transition-based reminders)
        // CRITICAL: StateChange events must NOT trigger reminder scheduling
        List<ReminderCandidate> scheduledCandidates = new List<ReminderCandidate>();
        if (actionEvent.EventType != EventType.StateChange)
        {
            scheduledCandidates = await _reminderScheduler.ScheduleCandidatesForEventAsync(
                actionEvent, cancellationToken);
        }

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

