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

    public RoutineLearningService(
        IRoutineRepository routineRepository,
        IRoutineReminderRepository routineReminderRepository,
        IEventRepository eventRepository,
        IConfiguration configuration,
        ILogger<RoutineLearningService> logger)
    {
        _routineRepository = routineRepository;
        _routineReminderRepository = routineReminderRepository;
        _eventRepository = eventRepository;
        _configuration = configuration;
        _logger = logger;
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

        var observationWindowMinutes = _configuration.GetValue<int>("Routine:ObservationWindowMinutes", 45);

        if (routine == null)
        {
            // Create new routine
            routine = new Routine(intentEvent.PersonId, intentEvent.ActionType, intentEvent.TimestampUtc);
            routine.OpenObservationWindow(intentEvent.TimestampUtc, observationWindowMinutes);
            await _routineRepository.AddAsync(routine, cancellationToken);
            _logger.LogInformation(
                "Created new routine {RoutineId} for person {PersonId} with intent {IntentType}",
                routine.Id, intentEvent.PersonId, intentEvent.ActionType);
        }
        else
        {
            // Update existing routine - open new observation window
            routine.OpenObservationWindow(intentEvent.TimestampUtc, observationWindowMinutes);
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
    /// </summary>
    public async Task ProcessObservedEventAsync(ActionEvent observedEvent, CancellationToken cancellationToken)
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

            await LearnFromObservedActionAsync(routine, observedEvent, cancellationToken);
        }
    }

    /// <summary>
    /// Learns from an observed action within a routine's observation window.
    /// </summary>
    private async Task LearnFromObservedActionAsync(
        Routine routine,
        ActionEvent observedEvent,
        CancellationToken cancellationToken)
    {
        // Try to find existing routine reminder for this action
        var existingReminder = await _routineReminderRepository.GetByRoutineAndActionAsync(
            routine.Id,
            observedEvent.ActionType,
            cancellationToken);

        var defaultProbability = _configuration.GetValue<double>("Routine:DefaultRoutineProbability", 0.5);
        var increaseStep = _configuration.GetValue<double>("Routine:ProbabilityIncreaseStep", 0.1);

        if (existingReminder != null)
        {
            // Update existing reminder - increase probability
            existingReminder.IncreaseConfidence(increaseStep);
            existingReminder.RecordObservation(observedEvent.TimestampUtc);
            
            if (observedEvent.CustomData != null)
            {
                existingReminder.UpdateCustomData(observedEvent.CustomData);
            }

            await _routineReminderRepository.UpdateAsync(existingReminder, cancellationToken);
            _logger.LogInformation(
                "Updated routine reminder {ReminderId} for routine {RoutineId} (action: {ActionType}, confidence: {Confidence})",
                existingReminder.Id, routine.Id, observedEvent.ActionType, existingReminder.Confidence);
        }
        else
        {
            // Create new routine reminder
            var newReminder = new RoutineReminder(
                routine.Id,
                routine.PersonId,
                observedEvent.ActionType,
                defaultProbability,
                observedEvent.CustomData);
            
            newReminder.RecordObservation(observedEvent.TimestampUtc);
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

