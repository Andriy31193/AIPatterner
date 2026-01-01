// Service interface for getting matching policies
namespace AIPatterner.Application.Services;

using AIPatterner.Application.Queries;

public interface IMatchingPolicyService
{
    Task<MatchingCriteria> GetMatchingCriteriaAsync(CancellationToken cancellationToken);
}

