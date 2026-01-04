// Interface for signal similarity evaluation
namespace AIPatterner.Domain.Services;

using AIPatterner.Domain.ValueObjects;

/// <summary>
/// Service for evaluating similarity between signal profiles.
/// </summary>
public interface ISignalSimilarityEvaluator
{
    /// <summary>
    /// Calculates weighted cosine similarity between two signal profiles.
    /// </summary>
    /// <param name="baseline">Reminder's baseline signal profile</param>
    /// <param name="eventProfile">Event's signal profile</param>
    /// <returns>Similarity score in range [0, 1] (1.0 = identical, 0.0 = completely different)</returns>
    double CalculateSimilarity(SignalProfile? baseline, SignalProfile? eventProfile);
}

