// Domain service implementation for building deterministic context bucket keys
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Domain.Entities;
using AIPatterner.Domain.Services;
using Microsoft.Extensions.Configuration;

public class ContextBucketKeyBuilder : IContextBucketKeyBuilder
{
    private readonly IConfiguration _configuration;

    public ContextBucketKeyBuilder(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string BuildKey(ActionContext context)
    {
        var format = _configuration.GetValue<string>("ContextBucket:Format", "{dayType}*{timeBucket}*{location}");
        
        var key = format
            .Replace("{dayType}", context.DayType ?? "unknown")
            .Replace("{timeBucket}", context.TimeBucket ?? "unknown")
            .Replace("{location}", context.Location ?? "unknown");

        return key;
    }
}

