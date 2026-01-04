// Interface for signal selection and normalization
namespace AIPatterner.Domain.Services;

using AIPatterner.Domain.ValueObjects;

/// <summary>
/// Service for selecting and normalizing sensor signals.
/// </summary>
public interface ISignalSelector
{
    /// <summary>
    /// Selects top-K signals by importance and normalizes them into a signal profile vector.
    /// </summary>
    /// <param name="signalStates">Raw signal states from event</param>
    /// <param name="topK">Maximum number of signals to select (default from policy)</param>
    /// <returns>Normalized signal profile vector</returns>
    SignalProfile SelectAndNormalizeSignals(List<SignalState> signalStates, int topK);
}

