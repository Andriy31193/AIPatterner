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
    public Guid? UserId { get; private set; } // Nullable for backward compatibility
    public string IntentType { get; private set; } // The ActionType of the StateChange event (e.g., "ArrivalHome")
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastIntentOccurredAtUtc { get; private set; }
    public DateTime? ObservationWindowEndsAtUtc { get; private set; } // When the current observation window closes

    private Routine() { } // EF Core

    public Routine(string personId, string intentType, DateTime intentOccurredAtUtc, Guid? userId = null)
    {
        if (string.IsNullOrWhiteSpace(personId))
            throw new ArgumentException("PersonId cannot be null or empty", nameof(personId));
        if (string.IsNullOrWhiteSpace(intentType))
            throw new ArgumentException("IntentType cannot be null or empty", nameof(intentType));

        Id = Guid.NewGuid();
        PersonId = personId;
        UserId = userId;
        IntentType = intentType;
        CreatedAtUtc = DateTime.UtcNow;
        LastIntentOccurredAtUtc = intentOccurredAtUtc;
    }

    /// <summary>
    /// Opens an observation window starting from the intent occurrence time.
    /// </summary>
    public void OpenObservationWindow(DateTime intentOccurredAtUtc, int windowMinutes)
    {
        LastIntentOccurredAtUtc = intentOccurredAtUtc;
        ObservationWindowEndsAtUtc = intentOccurredAtUtc.AddMinutes(windowMinutes);
    }

    /// <summary>
    /// Checks if the observation window is currently open.
    /// </summary>
    public bool IsObservationWindowOpen(DateTime now)
    {
        return ObservationWindowEndsAtUtc.HasValue && 
               ObservationWindowEndsAtUtc.Value > now;
    }

    /// <summary>
    /// Closes the observation window.
    /// </summary>
    public void CloseObservationWindow()
    {
        ObservationWindowEndsAtUtc = null;
    }
}

