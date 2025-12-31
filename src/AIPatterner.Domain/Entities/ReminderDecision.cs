// Value object representing the decision made for a reminder candidate
namespace AIPatterner.Domain.Entities;

public class ReminderDecision
{
    public bool ShouldSpeak { get; private set; }
    public string? SpeechTemplateKey { get; private set; }
    public string Reason { get; private set; }
    public double ConfidenceLevel { get; private set; }
    public string? NaturalLanguagePhrase { get; private set; }

    private ReminderDecision() { } // EF Core

    public ReminderDecision(
        bool shouldSpeak,
        string reason,
        double confidenceLevel,
        string? speechTemplateKey = null,
        string? naturalLanguagePhrase = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason cannot be null or empty", nameof(reason));
        if (confidenceLevel < 0 || confidenceLevel > 1)
            throw new ArgumentException("ConfidenceLevel must be between 0 and 1", nameof(confidenceLevel));

        ShouldSpeak = shouldSpeak;
        SpeechTemplateKey = speechTemplateKey;
        Reason = reason;
        ConfidenceLevel = confidenceLevel;
        NaturalLanguagePhrase = naturalLanguagePhrase;
    }
}

