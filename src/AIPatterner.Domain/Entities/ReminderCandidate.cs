// Domain entity representing a scheduled reminder check candidate
namespace AIPatterner.Domain.Entities;

public enum ReminderStyle
{
    Ask,
    Suggest,
    Silent
}

public enum ReminderCandidateStatus
{
    Scheduled,
    Executed,
    Skipped,
    Expired
}

public class ReminderCandidate
{
    public Guid Id { get; private set; }
    public string PersonId { get; private set; }
    public string SuggestedAction { get; private set; }
    public DateTime CheckAtUtc { get; private set; }
    public Guid? TransitionId { get; private set; }
    public ReminderStyle Style { get; private set; }
    public ReminderCandidateStatus Status { get; private set; }
    public ReminderDecision? Decision { get; private set; }
    public double Confidence { get; private set; } // Probability/confidence level (0.0 to 1.0)
    public string? Occurrence { get; private set; } // Occurrence pattern (e.g., "daily", "weekly", "every 3 days", "weekdays")
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ExecutedAtUtc { get; private set; }
    public Guid? SourceEventId { get; private set; } // Event ID that created this reminder
    public Dictionary<string, string>? CustomData { get; private set; } // Custom data from source event

    private ReminderCandidate() { } // EF Core

    public ReminderCandidate(
        string personId,
        string suggestedAction,
        DateTime checkAtUtc,
        ReminderStyle style,
        Guid? transitionId = null,
        double confidence = 0.5,
        string? occurrence = null,
        Guid? sourceEventId = null,
        Dictionary<string, string>? customData = null)
    {
        if (string.IsNullOrWhiteSpace(personId))
            throw new ArgumentException("PersonId cannot be null or empty", nameof(personId));
        if (string.IsNullOrWhiteSpace(suggestedAction))
            throw new ArgumentException("SuggestedAction cannot be null or empty", nameof(suggestedAction));
        if (confidence < 0.0 || confidence > 1.0)
            throw new ArgumentException("Confidence must be between 0.0 and 1.0", nameof(confidence));

        Id = Guid.NewGuid();
        PersonId = personId;
        SuggestedAction = suggestedAction;
        CheckAtUtc = checkAtUtc;
        Style = style;
        TransitionId = transitionId;
        Confidence = confidence;
        Occurrence = occurrence;
        SourceEventId = sourceEventId;
        CustomData = customData;
        Status = ReminderCandidateStatus.Scheduled;
        CreatedAtUtc = DateTime.UtcNow;
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

    public void SetOccurrence(string? occurrence)
    {
        Occurrence = occurrence;
    }

    public void UpdateCheckAtUtc(DateTime checkAtUtc)
    {
        CheckAtUtc = checkAtUtc;
    }

    public void UpdateCustomData(Dictionary<string, string>? customData)
    {
        CustomData = customData;
    }

    public void MarkAsExecuted(ReminderDecision decision)
    {
        if (decision == null)
            throw new ArgumentNullException(nameof(decision));

        Status = ReminderCandidateStatus.Executed;
        Decision = decision;
        ExecutedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsSkipped()
    {
        Status = ReminderCandidateStatus.Skipped;
        ExecutedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsExpired()
    {
        Status = ReminderCandidateStatus.Expired;
    }

    public bool IsDue(DateTime now)
    {
        return Status == ReminderCandidateStatus.Scheduled && CheckAtUtc <= now;
    }
}

