// Domain entity representing a learned transition between two actions in a specific context
namespace AIPatterner.Domain.Entities;

public class ActionTransition
{
    public Guid Id { get; private set; }
    public string PersonId { get; private set; }
    public string FromAction { get; private set; }
    public string ToAction { get; private set; }
    public string ContextBucket { get; private set; }
    public int OccurrenceCount { get; private set; }
    public double Confidence { get; private set; }
    public TimeSpan? AverageDelay { get; private set; }
    public DateTime LastObservedUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private ActionTransition() { } // EF Core

    public ActionTransition(
        string personId,
        string fromAction,
        string toAction,
        string contextBucket)
    {
        if (string.IsNullOrWhiteSpace(personId))
            throw new ArgumentException("PersonId cannot be null or empty", nameof(personId));
        if (string.IsNullOrWhiteSpace(fromAction))
            throw new ArgumentException("FromAction cannot be null or empty", nameof(fromAction));
        if (string.IsNullOrWhiteSpace(toAction))
            throw new ArgumentException("ToAction cannot be null or empty", nameof(toAction));
        if (string.IsNullOrWhiteSpace(contextBucket))
            throw new ArgumentException("ContextBucket cannot be null or empty", nameof(contextBucket));

        Id = Guid.NewGuid();
        PersonId = personId;
        FromAction = fromAction;
        ToAction = toAction;
        ContextBucket = contextBucket;
        OccurrenceCount = 0;
        Confidence = 0.0;
        AverageDelay = null;
        LastObservedUtc = DateTime.UtcNow;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateWithObservation(TimeSpan observedDelay, double alpha, double beta)
    {
        if (alpha < 0 || alpha > 1)
            throw new ArgumentException("Alpha must be between 0 and 1", nameof(alpha));
        if (beta < 0 || beta > 1)
            throw new ArgumentException("Beta must be between 0 and 1", nameof(beta));

        OccurrenceCount++;
        Confidence = alpha * 1.0 + (1 - alpha) * Confidence;
        
        if (AverageDelay.HasValue)
        {
            var totalSeconds = AverageDelay.Value.TotalSeconds * (1 - beta) + observedDelay.TotalSeconds * beta;
            AverageDelay = TimeSpan.FromSeconds(totalSeconds);
        }
        else
        {
            AverageDelay = observedDelay;
        }

        LastObservedUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ApplyDecay(double decayRate)
    {
        if (decayRate < 0 || decayRate > 1)
            throw new ArgumentException("DecayRate must be between 0 and 1", nameof(decayRate));

        Confidence *= (1 - decayRate);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ReduceConfidence(double reductionFactor)
    {
        if (reductionFactor < 0 || reductionFactor > 1)
            throw new ArgumentException("ReductionFactor must be between 0 and 1", nameof(reductionFactor));

        Confidence = Math.Max(0, Confidence * (1 - reductionFactor));
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

