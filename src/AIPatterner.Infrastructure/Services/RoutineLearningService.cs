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
    private readonly IReminderCandidateRepository _reminderCandidateRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RoutineLearningService> _logger;
    private readonly AIPatterner.Domain.Services.ISignalSelector _signalSelector;
    private readonly AIPatterner.Domain.Services.ISignalSimilarityEvaluator _similarityEvaluator;
    private readonly AIPatterner.Application.Services.ISignalPolicyService _signalPolicyService;

    public RoutineLearningService(
        IRoutineRepository routineRepository,
        IRoutineReminderRepository routineReminderRepository,
        IReminderCandidateRepository reminderCandidateRepository,
        IEventRepository eventRepository,
        IConfiguration configuration,
        ILogger<RoutineLearningService> logger,
        AIPatterner.Domain.Services.ISignalSelector signalSelector,
        AIPatterner.Domain.Services.ISignalSimilarityEvaluator similarityEvaluator,
        AIPatterner.Application.Services.ISignalPolicyService signalPolicyService)
    {
        _routineRepository = routineRepository;
        _routineReminderRepository = routineReminderRepository;
        _reminderCandidateRepository = reminderCandidateRepository;
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
            var bucket = RoutineTimeContextBucketSelector.SelectBucket(intentEvent.TimestampUtc, _configuration);
            routine.OpenObservationWindow(intentEvent.TimestampUtc, routine.ObservationWindowMinutes, bucket);
            await _routineRepository.AddAsync(routine, cancellationToken);
            _logger.LogInformation(
                "Created new routine {RoutineId} for person {PersonId} with intent {IntentType}",
                routine.Id, intentEvent.PersonId, intentEvent.ActionType);
        }
        else
        {
            // Update existing routine - open new observation window using routine's configured minutes
            var bucket = RoutineTimeContextBucketSelector.SelectBucket(intentEvent.TimestampUtc, _configuration);
            routine.OpenObservationWindow(intentEvent.TimestampUtc, routine.ObservationWindowMinutes, bucket);
            await _routineRepository.UpdateAsync(routine, cancellationToken);
            _logger.LogInformation(
                "Opened observation window for routine {RoutineId} (person {PersonId}, intent {IntentType})",
                routine.Id, intentEvent.PersonId, intentEvent.ActionType);
        }

        // Schedule routine reminders for execution based on learned delays (bucket selected at activation)
        await ScheduleRoutineRemindersForActivationAsync(routine, intentEvent, cancellationToken);

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

        var routinesToClose = new List<Routine>();

        foreach (var routine in routines)
        {
            // Strict enforcement: if the window has ended, close it and do not learn.
            if (routine.ObservationWindowEndsAtUtc.HasValue && eventTime > routine.ObservationWindowEndsAtUtc.Value)
            {
                if (routine.ObservationWindowStartUtc.HasValue)
                {
                    routine.CloseObservationWindow();
                    routinesToClose.Add(routine);
                }
                continue;
            }

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

        if (routinesToClose.Count > 0)
        {
            await _routineRepository.UpdateMultipleAsync(routinesToClose, cancellationToken);
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
            var bucketKey = routine.ActiveTimeContextBucket ?? "evening";
            var existingReminder = await _routineReminderRepository.GetByRoutineBucketAndActionAsync(
                routine.Id,
                bucketKey,
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
            var bucketKey = routine.ActiveTimeContextBucket ?? "evening";
            var existingReminder = await _routineReminderRepository.GetByRoutineBucketAndActionAsync(
                routine.Id,
                bucketKey,
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
        var activeBucket = routine.ActiveTimeContextBucket ?? "evening";
        var reminder = await _routineReminderRepository.GetByRoutineBucketAndActionAsync(
            routine.Id,
            activeBucket,
            observedEvent.ActionType,
            cancellationToken);

        var defaultProbability = _configuration.GetValue<double>("Routine:DefaultRoutineProbability", 0.5);
        var increaseStep = _configuration.GetValue<double>("Routine:ProbabilityIncreaseStep", 0.1);

        if (reminder != null)
        {
            // Update existing reminder - increase probability
            reminder.IncreaseConfidence(increaseStep);
            reminder.RecordObservation(observedEvent.TimestampUtc);

            // Delay learning (relative to routine activation; updated only within learning window)
            if (routine.ObservationWindowStartUtc.HasValue)
            {
                var observedDelaySeconds = (observedEvent.TimestampUtc - routine.ObservationWindowStartUtc.Value).TotalSeconds;
                if (observedDelaySeconds >= 0)
                {
                    var baseAlpha = _configuration.GetValue<double>("Routine:DelayLearning:BaseAlpha", 0.2);
                    var halfLifeDays = _configuration.GetValue<double>("Routine:DelayLearning:HalfLifeDays", 30.0);
                    var maxEvidence = _configuration.GetValue<int>("Routine:DelayLearning:MaxEvidenceItems", 200);

                    reminder.RecordDelayObservation(
                        routine.ObservationWindowStartUtc.Value,
                        observedEvent.TimestampUtc,
                        observedDelaySeconds,
                        observedEvent.Id,
                        baseAlpha,
                        halfLifeDays,
                        maxEvidence);
                }
            }
            
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
                customData.Count > 0 ? customData : null,
                activeBucket);
            
            newReminder.RecordObservation(observedEvent.TimestampUtc);

            if (routine.ObservationWindowStartUtc.HasValue)
            {
                var observedDelaySeconds = (observedEvent.TimestampUtc - routine.ObservationWindowStartUtc.Value).TotalSeconds;
                if (observedDelaySeconds >= 0)
                {
                    var baseAlpha = _configuration.GetValue<double>("Routine:DelayLearning:BaseAlpha", 0.2);
                    var halfLifeDays = _configuration.GetValue<double>("Routine:DelayLearning:HalfLifeDays", 30.0);
                    var maxEvidence = _configuration.GetValue<int>("Routine:DelayLearning:MaxEvidenceItems", 200);

                    newReminder.RecordDelayObservation(
                        routine.ObservationWindowStartUtc.Value,
                        observedEvent.TimestampUtc,
                        observedDelaySeconds,
                        observedEvent.Id,
                        baseAlpha,
                        halfLifeDays,
                        maxEvidence);
                }
            }
            
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

    private async Task ScheduleRoutineRemindersForActivationAsync(
        Routine routine,
        ActionEvent intentEvent,
        CancellationToken cancellationToken)
    {
        // Bucket is selected only at activation (classifier, not scheduler)
        var bucket = routine.ActiveTimeContextBucket ?? "evening";

        var remindersInBucket = await _routineReminderRepository.GetByRoutineAndBucketAsync(
            routine.Id,
            bucket,
            cancellationToken);

        if (remindersInBucket.Count == 0 || !routine.ObservationWindowStartUtc.HasValue)
        {
            return;
        }

        var minSamples = _configuration.GetValue<double>("Routine:DelayLearning:MinSamplesForTiming", 3.0);
        var defaultSuggestionDelaySeconds = _configuration.GetValue<int>("Routine:DelayLearning:DefaultSuggestionDelaySeconds", 120);

        foreach (var rr in remindersInBucket)
        {
            var delaySeconds = rr.MedianDelayApproxSeconds ?? rr.EmaDelaySeconds ?? defaultSuggestionDelaySeconds;
            delaySeconds = Math.Clamp(delaySeconds, 0, routine.ObservationWindowMinutes * 60.0);

            var executeAt = routine.ObservationWindowStartUtc.Value.AddSeconds(delaySeconds);

            var style = rr.DelaySampleCount < 1.0 ? ReminderStyle.Ask :
                        rr.DelaySampleCount < minSamples ? ReminderStyle.Suggest :
                        ReminderStyle.Suggest;

            // For routine candidates, confidence controls whether we auto-execute; the evaluation layer still decides speak vs skip.
            var confidence = rr.Confidence;
            if (rr.DelaySampleCount < minSamples)
            {
                confidence = Math.Min(confidence, 0.6);
            }

            var customData = new Dictionary<string, string>
            {
                { "source", "routine" },
                { "routineId", routine.Id.ToString() },
                { "intentType", routine.IntentType },
                { "timeContextBucket", bucket },
                { "activationTimestampUtc", routine.ObservationWindowStartUtc.Value.ToString("o") },
                { "learnedDelaySeconds", delaySeconds.ToString("F0") },
                { "delaySampleCount", rr.DelaySampleCount.ToString("F2") }
            };

            var candidate = new ReminderCandidate(
                routine.PersonId,
                rr.SuggestedAction,
                executeAt,
                style,
                null,
                confidence,
                null,
                intentEvent.Id,
                customData);

            candidate.SetIsSafeToAutoExecute(rr.IsSafeToAutoExecute);
            await _reminderCandidateRepository.AddAsync(candidate, cancellationToken);
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

    /// <summary>
    /// Checks if an event is within any routine's learning window for the given person.
    /// Events within learning windows should ONLY affect routine reminders, not general reminders.
    /// </summary>
    public async Task<bool> IsEventWithinRoutineLearningWindowAsync(
        string personId,
        DateTime eventTimestampUtc,
        CancellationToken cancellationToken)
    {
        var activeRoutines = await _routineRepository.GetActiveRoutinesAsync(
            personId,
            eventTimestampUtc,
            cancellationToken);

        return activeRoutines.Any(r => r.IsObservationWindowOpen(eventTimestampUtc));
    }
}

