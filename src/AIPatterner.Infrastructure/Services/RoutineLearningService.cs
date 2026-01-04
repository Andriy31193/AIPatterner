// Service for learning routines from observed behavior
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Application.Handlers;
using AIPatterner.Application.Services;
using AIPatterner.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles learning of routine reminders from observed behavior after intent events.
/// </summary>
public class RoutineLearningService : IRoutineLearningService
{
    private readonly IRoutineRepository _routineRepository;
    private readonly IRoutineReminderRepository _routineReminderRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RoutineLearningService> _logger;
    private readonly AIPatterner.Domain.Services.ISignalSelector _signalSelector;
    private readonly AIPatterner.Domain.Services.ISignalSimilarityEvaluator _similarityEvaluator;
    private readonly AIPatterner.Application.Services.ISignalPolicyService _signalPolicyService;

    public RoutineLearningService(
        IRoutineRepository routineRepository,
        IRoutineReminderRepository routineReminderRepository,
        IEventRepository eventRepository,
        IConfiguration configuration,
        ILogger<RoutineLearningService> logger,
        AIPatterner.Domain.Services.ISignalSelector signalSelector,
        AIPatterner.Domain.Services.ISignalSimilarityEvaluator similarityEvaluator,
        AIPatterner.Application.Services.ISignalPolicyService signalPolicyService)
    {
        _routineRepository = routineRepository;
        _routineReminderRepository = routineReminderRepository;
        _eventRepository = eventRepository;
        _configuration = configuration;
        _logger = logger;
        _signalSelector = signalSelector;
        _similarityEvaluator = similarityEvaluator;
        _signalPolicyService = signalPolicyService;
    }

    /// <summary>
    /// Handles a StateChange event (intent). Closes all active windows, then opens observation window for matching routine.
    /// </summary>
    public async Task<Routine> HandleIntentAsync(ActionEvent intentEvent, CancellationToken cancellationToken)
    {
        if (intentEvent.EventType != EventType.StateChange)
        {
            throw new ArgumentException("Event must be of type StateChange", nameof(intentEvent));
        }

        // CRITICAL: Close all currently active routine learning windows for this person
        // This ensures only one routine is actively learning at a time
        var activeRoutines = await _routineRepository.GetActiveRoutinesAsync(
            intentEvent.PersonId,
            intentEvent.TimestampUtc,
            cancellationToken);

        if (activeRoutines.Any())
        {
            foreach (var activeRoutine in activeRoutines)
            {
                activeRoutine.CloseObservationWindow();
                _logger.LogInformation(
                    "Closed observation window for routine {RoutineId} (person {PersonId}, intent {IntentType}) due to new StateChange",
                    activeRoutine.Id, activeRoutine.PersonId, activeRoutine.IntentType);
            }
            await _routineRepository.UpdateMultipleAsync(activeRoutines, cancellationToken);
        }

        // Find or create routine for this intent
        var routine = await _routineRepository.GetByPersonAndIntentAsync(
            intentEvent.PersonId,
            intentEvent.ActionType,
            cancellationToken);

        // Get observation window from Policies configuration (default 60 minutes) for new routines
        var defaultObservationWindowMinutes = _configuration.GetValue<int>("Policies:RoutineObservationWindowMinutes", 60);

        if (routine == null)
        {
            // Create new routine with default observation window
            routine = new Routine(intentEvent.PersonId, intentEvent.ActionType, intentEvent.TimestampUtc, defaultObservationWindowMinutes);
            routine.OpenObservationWindow(intentEvent.TimestampUtc, routine.ObservationWindowMinutes);
            await _routineRepository.AddAsync(routine, cancellationToken);
            _logger.LogInformation(
                "Created new routine {RoutineId} for person {PersonId} with intent {IntentType}",
                routine.Id, intentEvent.PersonId, intentEvent.ActionType);
        }
        else
        {
            // Update existing routine - open new observation window using routine's configured minutes
            routine.OpenObservationWindow(intentEvent.TimestampUtc, routine.ObservationWindowMinutes);
            await _routineRepository.UpdateAsync(routine, cancellationToken);
            _logger.LogInformation(
                "Opened observation window for routine {RoutineId} (person {PersonId}, intent {IntentType})",
                routine.Id, intentEvent.PersonId, intentEvent.ActionType);
        }

        return routine;
    }

    /// <summary>
    /// Processes an observed event within an open observation window.
    /// Creates or updates routine reminders based on observed actions.
    /// Only updates routine reminders if the event is within the observation window and matches time offset and state signal policies.
    /// </summary>
    public async Task ProcessObservedEventAsync(ActionEvent observedEvent, string? userPrompt, List<AIPatterner.Domain.ValueObjects.SignalState>? signalStates, CancellationToken cancellationToken)
    {
        // Find all routines for this person with open observation windows
        var routines = await _routineRepository.GetByPersonAsync(observedEvent.PersonId, cancellationToken);
        // Use the event's timestamp to check if windows were open when the event occurred
        var eventTime = observedEvent.TimestampUtc;

        foreach (var routine in routines)
        {
            if (!routine.IsObservationWindowOpen(eventTime))
            {
                continue; // Skip routines with closed windows
            }

            // Skip if this is the intent event itself
            if (observedEvent.ActionType == routine.IntentType)
            {
                continue;
            }

            // Skip StateChange events - only learn from regular Action events
            if (observedEvent.EventType == EventType.StateChange)
            {
                continue;
            }

            await LearnFromObservedActionAsync(routine, observedEvent, userPrompt, signalStates, cancellationToken);
        }
    }

    /// <summary>
    /// Learns from an observed action within a routine's observation window.
    /// Enforces time offset tolerance and state signal matching policies.
    /// </summary>
    private async Task LearnFromObservedActionAsync(
        Routine routine,
        ActionEvent observedEvent,
        string? userPrompt,
        List<AIPatterner.Domain.ValueObjects.SignalState>? signalStates,
        CancellationToken cancellationToken)
    {
        // Get matching policies
        var timeOffsetMinutes = _configuration.GetValue<int>("Policies:TimeOffsetMinutes", 45);
        var matchByStateSignals = _configuration.GetValue<bool>("Policies:MatchByStateSignals", true);

        // STRICT: Check time offset tolerance
        // For routine reminders, we check against the routine's observation window start time
        // Events must be within the observation window AND within time offset tolerance
        if (routine.ObservationWindowStartUtc.HasValue)
        {
            var timeSinceWindowStart = (observedEvent.TimestampUtc - routine.ObservationWindowStartUtc.Value).TotalMinutes;
            if (timeSinceWindowStart > timeOffsetMinutes)
            {
                _logger.LogWarning(
                    "Event {EventId} ignored for routine {RoutineId}: time offset {TimeOffset} minutes exceeds tolerance {Tolerance} minutes",
                    observedEvent.Id, routine.Id, timeSinceWindowStart, timeOffsetMinutes);
                return; // Event outside time offset tolerance
            }
        }

        // STRICT: Check state signal matching if enabled
        if (matchByStateSignals)
        {
            // Check if reminder has state signal requirements (stored in CustomData)
            // For new reminders, we'll check the event's state signals
            // For existing reminders, check if event matches the reminder's required state signals
            var existingReminder = await _routineReminderRepository.GetByRoutineAndActionAsync(
                routine.Id,
                observedEvent.ActionType,
                cancellationToken);

            if (existingReminder != null && existingReminder.CustomData != null && existingReminder.CustomData.Count > 0)
            {
                // Existing reminder has state signal requirements - check if event matches
                if (observedEvent.Context.StateSignals == null || observedEvent.Context.StateSignals.Count == 0)
                {
                    _logger.LogWarning(
                        "Event {EventId} ignored for routine reminder {ReminderId}: state signals required but event has none",
                        observedEvent.Id, existingReminder.Id);
                    return; // Event lacks required state signals
                }

                // All state signals in reminder must be present in event with matching values
                foreach (var reminderSignal in existingReminder.CustomData)
                {
                    if (!observedEvent.Context.StateSignals.ContainsKey(reminderSignal.Key) ||
                        observedEvent.Context.StateSignals[reminderSignal.Key] != reminderSignal.Value)
                    {
                        _logger.LogWarning(
                            "Event {EventId} ignored for routine reminder {ReminderId}: state signal {Key}={Value} not matched",
                            observedEvent.Id, existingReminder.Id, reminderSignal.Key, reminderSignal.Value);
                        return; // State signal mismatch
                    }
                }
            }
            // For new reminders, we'll accept the event and store its state signals in CustomData
        }

        // Check signal similarity if signal selection is enabled
        var isSignalSelectionEnabled = await _signalPolicyService.IsSignalSelectionEnabledAsync(cancellationToken);
        if (isSignalSelectionEnabled && signalStates != null && signalStates.Count > 0)
        {
            var existingReminder = await _routineReminderRepository.GetByRoutineAndActionAsync(
                routine.Id,
                observedEvent.ActionType,
                cancellationToken);
            
            if (existingReminder != null)
            {
                var reminderBaseline = existingReminder.GetSignalProfile();
                
                if (reminderBaseline != null && reminderBaseline.Signals != null && reminderBaseline.Signals.Count > 0)
                {
                    // Reminder has a baseline - check similarity
                    var selectionLimit = await _signalPolicyService.GetSignalSelectionLimitAsync(cancellationToken);
                    var eventProfile = _signalSelector.SelectAndNormalizeSignals(signalStates, selectionLimit);
                    var similarity = _similarityEvaluator.CalculateSimilarity(reminderBaseline, eventProfile);
                    var threshold = await _signalPolicyService.GetSignalSimilarityThresholdAsync(cancellationToken);
                    
                    if (similarity < threshold)
                    {
                        // Signal mismatch - skip this reminder update
                        _logger.LogInformation(
                            "Routine reminder {ReminderId} skipped due to signal mismatch: similarity {Similarity} < threshold {Threshold}",
                            existingReminder.Id, similarity, threshold);
                        return; // Do not update baseline or userPromptsList
                    }
                    
                    _logger.LogDebug(
                        "Routine reminder {ReminderId} passed signal similarity check: similarity {Similarity} >= threshold {Threshold}",
                        existingReminder.Id, similarity, threshold);
                }
                // If reminder has no baseline yet, allow normal behavior (will be created on first match)
            }
        }

        // Try to find existing routine reminder for this action
        var reminder = await _routineReminderRepository.GetByRoutineAndActionAsync(
            routine.Id,
            observedEvent.ActionType,
            cancellationToken);

        var defaultProbability = _configuration.GetValue<double>("Routine:DefaultRoutineProbability", 0.5);
        var increaseStep = _configuration.GetValue<double>("Routine:ProbabilityIncreaseStep", 0.1);

        if (reminder != null)
        {
            // Update existing reminder - increase probability
            reminder.IncreaseConfidence(increaseStep);
            reminder.RecordObservation(observedEvent.TimestampUtc);
            
            if (observedEvent.CustomData != null)
            {
                reminder.UpdateCustomData(observedEvent.CustomData);
            }

            // Append userPrompt if provided
            if (!string.IsNullOrWhiteSpace(userPrompt))
            {
                reminder.AppendUserPrompt(userPrompt, observedEvent.TimestampUtc);
            }
            
            // Update signal profile baseline if signal selection is enabled and event has signals
            // CRITICAL: Only update if event is within observation window (already checked above)
            if (isSignalSelectionEnabled && signalStates != null && signalStates.Count > 0)
            {
                var selectionLimit = await _signalPolicyService.GetSignalSelectionLimitAsync(cancellationToken);
                var eventProfile = _signalSelector.SelectAndNormalizeSignals(signalStates, selectionLimit);
                var alpha = await _signalPolicyService.GetSignalProfileUpdateAlphaAsync(cancellationToken);
                reminder.UpdateSignalProfile(eventProfile, alpha);
            }

            await _routineReminderRepository.UpdateAsync(reminder, cancellationToken);
            _logger.LogInformation(
                "Updated routine reminder {ReminderId} for routine {RoutineId} (action: {ActionType}, confidence: {Confidence})",
                reminder.Id, routine.Id, observedEvent.ActionType, reminder.Confidence);
        }
        else
        {
            // Create new routine reminder
            // Store state signals in CustomData if present
            var customData = observedEvent.CustomData ?? new Dictionary<string, string>();
            if (matchByStateSignals && observedEvent.Context.StateSignals != null && observedEvent.Context.StateSignals.Count > 0)
            {
                // Merge state signals into CustomData
                foreach (var signal in observedEvent.Context.StateSignals)
                {
                    customData[signal.Key] = signal.Value;
                }
            }

            var newReminder = new RoutineReminder(
                routine.Id,
                routine.PersonId,
                observedEvent.ActionType,
                defaultProbability,
                customData.Count > 0 ? customData : null);
            
            newReminder.RecordObservation(observedEvent.TimestampUtc);
            
            // Append userPrompt if provided
            if (!string.IsNullOrWhiteSpace(userPrompt))
            {
                newReminder.AppendUserPrompt(userPrompt, observedEvent.TimestampUtc);
            }
            
            // Initialize signal profile baseline if signal selection is enabled and event has signals
            // CRITICAL: Only initialize if event is within observation window (already checked above)
            if (isSignalSelectionEnabled && signalStates != null && signalStates.Count > 0)
            {
                var selectionLimit = await _signalPolicyService.GetSignalSelectionLimitAsync(cancellationToken);
                var eventProfile = _signalSelector.SelectAndNormalizeSignals(signalStates, selectionLimit);
                var alpha = await _signalPolicyService.GetSignalProfileUpdateAlphaAsync(cancellationToken);
                newReminder.UpdateSignalProfile(eventProfile, alpha);
            }
            
            await _routineReminderRepository.AddAsync(newReminder, cancellationToken);
            _logger.LogInformation(
                "Created new routine reminder {ReminderId} for routine {RoutineId} (action: {ActionType}, confidence: {Confidence})",
                newReminder.Id, routine.Id, observedEvent.ActionType, newReminder.Confidence);
        }
    }

    /// <summary>
    /// Handles feedback (probability decrease) for a routine reminder.
    /// Called when user responds "not today" or similar negative feedback.
    /// </summary>
    public async Task HandleFeedbackAsync(
        Guid routineReminderId,
        ProbabilityAction action,
        double value,
        CancellationToken cancellationToken)
    {
        var reminder = await _routineReminderRepository.GetByIdAsync(routineReminderId, cancellationToken);
        if (reminder == null)
        {
            _logger.LogWarning("Routine reminder {ReminderId} not found for feedback", routineReminderId);
            return;
        }

        reminder.UpdateConfidence(value, action);
        await _routineReminderRepository.UpdateAsync(reminder, cancellationToken);
        
        _logger.LogInformation(
            "Updated routine reminder {ReminderId} confidence to {Confidence} (action: {Action})",
            reminder.Id, reminder.Confidence, action);
    }

    /// <summary>
    /// Gets routine reminders that should be evaluated when an intent occurs.
    /// </summary>
    public async Task<List<RoutineReminder>> GetRemindersForIntentAsync(
        string personId,
        string intentType,
        CancellationToken cancellationToken)
    {
        var routine = await _routineRepository.GetByPersonAndIntentAsync(personId, intentType, cancellationToken);
        if (routine == null)
        {
            return new List<RoutineReminder>();
        }

        return await _routineReminderRepository.GetByRoutineAsync(routine.Id, cancellationToken);
    }
}

