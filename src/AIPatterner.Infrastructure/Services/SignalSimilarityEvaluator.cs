// Service implementation for signal similarity evaluation
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Domain.Services;
using AIPatterner.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for evaluating similarity between signal profiles using weighted cosine similarity.
/// </summary>
public class SignalSimilarityEvaluator : ISignalSimilarityEvaluator
{
    private readonly ILogger<SignalSimilarityEvaluator> _logger;
    private const double Epsilon = 1e-10; // Small value to avoid division by zero

    public SignalSimilarityEvaluator(ILogger<SignalSimilarityEvaluator> logger)
    {
        _logger = logger;
    }

    public double CalculateSimilarity(SignalProfile? baseline, SignalProfile? eventProfile)
    {
        // Handle null or empty profiles
        if (baseline == null || baseline.Signals == null || baseline.Signals.Count == 0)
        {
            // Empty baseline: require more evidence (do not auto-execute)
            return 0.0;
        }

        if (eventProfile == null || eventProfile.Signals == null || eventProfile.Signals.Count == 0)
        {
            // Empty event profile: no similarity
            return 0.0;
        }

        // Get union of all sensor keys
        var allKeys = baseline.Signals.Keys.Union(eventProfile.Signals.Keys).ToList();

        if (allKeys.Count == 0)
        {
            return 0.0;
        }

        // Build vectors over union of keys
        // For missing sensors, value = 0
        var baselineVector = new List<double>();
        var eventVector = new List<double>();

        foreach (var key in allKeys)
        {
            var baselineEntry = baseline.Signals.GetValueOrDefault(key);
            var eventEntry = eventProfile.Signals.GetValueOrDefault(key);

            // Vector component = weight * normalizedValue
            var baselineComponent = baselineEntry != null 
                ? baselineEntry.Weight * baselineEntry.NormalizedValue 
                : 0.0;
            
            var eventComponent = eventEntry != null 
                ? eventEntry.Weight * eventEntry.NormalizedValue 
                : 0.0;

            baselineVector.Add(baselineComponent);
            eventVector.Add(eventComponent);
        }

        // Calculate cosine similarity: dot(B, E) / (||B|| * ||E||)
        var dotProduct = 0.0;
        var baselineNormSquared = 0.0;
        var eventNormSquared = 0.0;

        for (int i = 0; i < baselineVector.Count; i++)
        {
            dotProduct += baselineVector[i] * eventVector[i];
            baselineNormSquared += baselineVector[i] * baselineVector[i];
            eventNormSquared += eventVector[i] * eventVector[i];
        }

        var baselineNorm = Math.Sqrt(baselineNormSquared);
        var eventNorm = Math.Sqrt(eventNormSquared);

        // Handle zero-norm vectors (epsilon protection)
        if (baselineNorm < Epsilon || eventNorm < Epsilon)
        {
            // Zero-norm vector: treat similarity as 0 (require more evidence)
            return 0.0;
        }

        var similarity = dotProduct / (baselineNorm * eventNorm);

        // Clamp to [0, 1] range (cosine similarity can theoretically be in [-1, 1] but with normalized vectors should be [0, 1])
        return Math.Max(0.0, Math.Min(1.0, similarity));
    }
}

