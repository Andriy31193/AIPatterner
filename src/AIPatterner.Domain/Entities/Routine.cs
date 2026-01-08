// Domain entity representing an intent-anchored learned routine
namespace AIPatterner.Domain.Entities;

/// <summary>
/// A Routine represents one intent (StateChange event) for one user.
/// It contains routine reminders that are learned from observed behavior after the intent occurs.
/// </summary>
public class Routine
{
    public Guid Id { get; private set; }
    public string PersonId { get; private set; }
    public string IntentType { get; private set; } // The ActionType of the StateChange event (e.g., "ArrivalHome")
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastIntentOccurredAtUtc { get; private set; }
    public DateTime? ObservationWindowStartUtc { get; private set; } // When the current observation window starts
    public DateTime? ObservationWindowEndsAtUtc { get; private set; } // When the current observation window closes
    public int ObservationWindowMinutes { get; private set; } = 60; // Duration of observation window in minutes (default 60)
    
    /// <summary>
    /// The active time-of-day context bucket selected at activation time.
    /// This is a classifier, not a scheduler. It must remain fixed for the duration of the learning window.
    /// </summary>
    public string? ActiveTimeContextBucket { get; private set; }

    private Routine() { } // EF Core

    public Routine(string personId, string intentType, DateTime intentOccurredAtUtc, int observationWindowMinutes = 60)
    {
        if (string.IsNullOrWhiteSpace(personId))
            throw new ArgumentException("PersonId cannot be null or empty", nameof(personId));
        if (string.IsNullOrWhiteSpace(intentType))
            throw new ArgumentException("IntentType cannot be null or empty", nameof(intentType));
        if (observationWindowMinutes < 1)
            throw new ArgumentException("ObservationWindowMinutes must be at least 1", nameof(observationWindowMinutes));

        Id = Guid.NewGuid();
        PersonId = personId;
        IntentType = intentType;
        CreatedAtUtc = DateTime.UtcNow;
        LastIntentOccurredAtUtc = intentOccurredAtUtc;
        ObservationWindowMinutes = observationWindowMinutes;
    }

    /// <summary>
    /// Opens an observation (learning) window starting from the intent occurrence time.
    /// Also sets the active time context bucket for this activation.
    /// </summary>
    public void OpenObservationWindow(DateTime intentOccurredAtUtc, int windowMinutes, string activeTimeContextBucket)
    {
        LastIntentOccurredAtUtc = intentOccurredAtUtc;
        ObservationWindowStartUtc = intentOccurredAtUtc;
        ObservationWindowEndsAtUtc = intentOccurredAtUtc.AddMinutes(windowMinutes);
        ActiveTimeContextBucket = activeTimeContextBucket;
    }

    /// <summary>
    /// Checks if the observation window is currently open for a given timestamp.
    /// An event timestamp must be within [ObservationWindowStartUtc, ObservationWindowEndsAtUtc].
    /// </summary>
    public bool IsObservationWindowOpen(DateTime eventTimestamp)
    {
        if (!ObservationWindowStartUtc.HasValue || !ObservationWindowEndsAtUtc.HasValue)
        {
            return false;
        }
        
        return eventTimestamp >= ObservationWindowStartUtc.Value && 
               eventTimestamp <= ObservationWindowEndsAtUtc.Value;
    }

    /// <summary>
    /// Closes the observation window.
    /// </summary>
    public void CloseObservationWindow()
    {
        ObservationWindowStartUtc = null;
        ObservationWindowEndsAtUtc = null;
        ActiveTimeContextBucket = null;
    }

    /// <summary>
    /// Updates the observation window minutes setting for this routine.
    /// This affects future observation windows opened for this routine.
    /// </summary>
    public void UpdateObservationWindowMinutes(int minutes)
    {
        if (minutes < 1)
            throw new ArgumentException("ObservationWindowMinutes must be at least 1", nameof(minutes));
        ObservationWindowMinutes = minutes;
    }
}

