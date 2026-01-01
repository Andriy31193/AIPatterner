// Service implementation for getting matching policies from configuration
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Application.Handlers;
using AIPatterner.Application.Queries;
using AIPatterner.Application.Services;
using Microsoft.Extensions.Configuration;

public class MatchingPolicyService : IMatchingPolicyService
{
    private readonly IConfigurationRepository _configRepository;
    private readonly IConfiguration _configuration;

    public MatchingPolicyService(
        IConfigurationRepository configRepository,
        IConfiguration configuration)
    {
        _configRepository = configRepository;
        _configuration = configuration;
    }

    public async Task<MatchingCriteria> GetMatchingCriteriaAsync(CancellationToken cancellationToken)
    {
        var policies = await _configRepository.GetByCategoryAsync("MatchingPolicy", cancellationToken);

        var getValue = (string key, string defaultValue) =>
        {
            var config = policies.FirstOrDefault(c => c.Key == key);
            return config?.Value ?? defaultValue;
        };

        return new MatchingCriteria
        {
            MatchByActionType = getValue("MatchByActionType", "true") == "true",
            MatchByDayType = getValue("MatchByDayType", "true") == "true",
            MatchByPeoplePresent = getValue("MatchByPeoplePresent", "true") == "true",
            MatchByStateSignals = getValue("MatchByStateSignals", "true") == "true",
            MatchByTimeBucket = getValue("MatchByTimeBucket", "false") == "true",
            MatchByLocation = getValue("MatchByLocation", "false") == "true",
            TimeOffsetMinutes = int.TryParse(getValue("TimeOffsetMinutes", "30"), out var offset) ? offset : 30
        };
    }
}

