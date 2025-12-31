// Domain service interface for building deterministic context bucket keys
namespace AIPatterner.Domain.Services;

using AIPatterner.Domain.Entities;

public interface IContextBucketKeyBuilder
{
    string BuildKey(ActionContext context);
}

