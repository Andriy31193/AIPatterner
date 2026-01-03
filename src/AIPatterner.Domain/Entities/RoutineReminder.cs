// Domain entity representing a reminder within a routine
namespace AIPatterner.Domain.Entities;

/// <summary>
/// A RoutineReminder is similar to ReminderCandidate but belongs to a Routine.
/// It represents a learned action that typically follows an intent (StateChange event).
/// </summary>
public class RoutineReminder
{
    public Guid Id { get; private set; }
    public Guid RoutineId { get; private set; }
    public string PersonId { get; private set; }
    public Guid? UserId { get; private set; } // Nullable for backward compatibility
    public string SuggestedAction { get; private set; }
    public double Confidence { get; private set; } // Probability/confidence level (0.0 to 1.0)
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; } // Audit: who created this reminder
    public DateTime? LastModifiedAtUtc { get; private set; } // Audit: when last modified
    public Guid? LastModifiedByUserId { get; private set; } // Audit: who last modified
    public DateTime? LastObservedAtUtc { get; private set; }
    public int ObservationCount { get; private set; } = 0;
    public Dictionary<string, string>? CustomData { get; private set; }

    private RoutineReminder() { } // EF Core

    public RoutineReminder(
        Guid routineId,
        string personId,
        string suggestedAction,
        double confidence,
        Dictionary<string, string>? customData = null,
        Guid? userId = null,
        Guid? createdByUserId = null)
    {
        if (string.IsNullOrWhiteSpace(personId))
            throw new ArgumentException("PersonId cannot be null or empty", nameof(personId));
        if (string.IsNullOrWhiteSpace(suggestedAction))
            throw new ArgumentException("SuggestedAction cannot be null or empty", nameof(suggestedAction));
        if (confidence < 0.0 || confidence > 1.0)
            throw new ArgumentException("Confidence must be between 0.0 and 1.0", nameof(confidence));

        Id = Guid.NewGuid();
        RoutineId = routineId;
        PersonId = personId;
        UserId = userId;
        SuggestedAction = suggestedAction;
        Confidence = confidence;
        CreatedAtUtc = DateTime.UtcNow;
        CreatedByUserId = createdByUserId;
        CustomData = customData;
    }

    public void IncreaseConfidence(double stepValue)
    {
        if (stepValue < 0.0)
            throw new ArgumentException("Step value must be non-negative", nameof(stepValue));
        
        Confidence = Math.Min(1.0, Confidence + stepValue);
    }

    public void DecreaseConfidence(double stepValue)
    {
        if (stepValue < 0.0)
            throw new ArgumentException("Step value must be non-negative", nameof(stepValue));
        
        Confidence = Math.Max(0.0, Confidence - stepValue);
    }

    public void UpdateConfidence(double value, ProbabilityAction action)
    {
        if (value < 0.0)
            throw new ArgumentException("Value must be non-negative", nameof(value));

        if (action == ProbabilityAction.Increase)
        {
            IncreaseConfidence(value);
        }
        else
        {
            DecreaseConfidence(value);
        }
    }

    /// <summary>
    /// Records an observation of this action occurring within the routine window.
    /// </summary>
    public void RecordObservation(DateTime observedAtUtc)
    {
        ObservationCount++;
        LastObservedAtUtc = observedAtUtc;
    }

    public void UpdateCustomData(Dictionary<string, string>? customData)
    {
        CustomData = customData;
    }

    public void SetUserId(Guid userId)
    {
        UserId = userId;
    }

    public void UpdateAuditInfo(Guid? modifiedByUserId)
    {
        LastModifiedAtUtc = DateTime.UtcNow;
        LastModifiedByUserId = modifiedByUserId;
    }
}

