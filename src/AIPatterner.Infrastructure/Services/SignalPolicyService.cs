// Service implementation for getting signal-related policies
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Application.Handlers;
using AIPatterner.Application.Services;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Service for getting signal-related policy configuration.
/// </summary>
public class SignalPolicyService : ISignalPolicyService
{
    private readonly IConfigurationRepository _configRepository;
    private readonly IConfiguration _configuration;

    public SignalPolicyService(
        IConfigurationRepository configRepository,
        IConfiguration configuration)
    {
        _configRepository = configRepository;
        _configuration = configuration;
    }

    public async Task<int> GetSignalSelectionLimitAsync(CancellationToken cancellationToken)
    {
        var policies = await _configRepository.GetByCategoryAsync("MatchingPolicy", cancellationToken);
        var config = policies.FirstOrDefault(c => c.Key == "SignalSelectionLimit");
        if (config != null && int.TryParse(config.Value, out var limit))
        {
            return limit;
        }
        return _configuration.GetValue<int>("Policies:SignalSelectionLimit", 10);
    }

    public async Task<double> GetSignalSimilarityThresholdAsync(CancellationToken cancellationToken)
    {
        var policies = await _configRepository.GetByCategoryAsync("MatchingPolicy", cancellationToken);
        var config = policies.FirstOrDefault(c => c.Key == "SignalSimilarityThreshold");
        if (config != null && double.TryParse(config.Value, out var threshold))
        {
            return threshold;
        }
        return _configuration.GetValue<double>("Policies:SignalSimilarityThreshold", 0.70);
    }

    public async Task<double> GetSignalProfileUpdateAlphaAsync(CancellationToken cancellationToken)
    {
        var policies = await _configRepository.GetByCategoryAsync("MatchingPolicy", cancellationToken);
        var config = policies.FirstOrDefault(c => c.Key == "SignalProfileUpdateAlpha");
        if (config != null && double.TryParse(config.Value, out var alpha))
        {
            return alpha;
        }
        return _configuration.GetValue<double>("Policies:SignalProfileUpdateAlpha", 0.10);
    }

    public async Task<bool> GetStoreEventSignalSnapshotAsync(CancellationToken cancellationToken)
    {
        var policies = await _configRepository.GetByCategoryAsync("MatchingPolicy", cancellationToken);
        var config = policies.FirstOrDefault(c => c.Key == "StoreEventSignalSnapshot");
        if (config != null && bool.TryParse(config.Value, out var store))
        {
            return store;
        }
        return _configuration.GetValue<bool>("Policies:StoreEventSignalSnapshot", false);
    }

    public async Task<double> GetSignalMismatchPenaltyAsync(CancellationToken cancellationToken)
    {
        var policies = await _configRepository.GetByCategoryAsync("MatchingPolicy", cancellationToken);
        var config = policies.FirstOrDefault(c => c.Key == "SignalMismatchPenalty");
        if (config != null && double.TryParse(config.Value, out var penalty))
        {
            return penalty;
        }
        return _configuration.GetValue<double>("Policies:SignalMismatchPenalty", 0.0);
    }

    public async Task<bool> IsSignalSelectionEnabledAsync(CancellationToken cancellationToken)
    {
        var policies = await _configRepository.GetByCategoryAsync("MatchingPolicy", cancellationToken);
        var config = policies.FirstOrDefault(c => c.Key == "SignalSelectionEnabled");
        if (config != null && bool.TryParse(config.Value, out var enabled))
        {
            return enabled;
        }
        // Default: enabled if SignalSelectionLimit > 0
        var limit = await GetSignalSelectionLimitAsync(cancellationToken);
        return limit > 0;
    }
}

