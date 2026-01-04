// Service interface for getting signal-related policies
namespace AIPatterner.Application.Services;

/// <summary>
/// Service for getting signal-related policy configuration.
/// </summary>
public interface ISignalPolicyService
{
    /// <summary>
    /// Gets signal selection limit (top-K).
    /// </summary>
    Task<int> GetSignalSelectionLimitAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets signal similarity threshold.
    /// </summary>
    Task<double> GetSignalSimilarityThresholdAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets signal profile update alpha (EMA coefficient).
    /// </summary>
    Task<double> GetSignalProfileUpdateAlphaAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets whether to store event signal snapshots.
    /// </summary>
    Task<bool> GetStoreEventSignalSnapshotAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets signal mismatch penalty (optional, default 0.0 = no penalty).
    /// </summary>
    Task<double> GetSignalMismatchPenaltyAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets whether signal selection/matching is enabled.
    /// </summary>
    Task<bool> IsSignalSelectionEnabledAsync(CancellationToken cancellationToken);
}

