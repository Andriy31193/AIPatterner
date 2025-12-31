// Unit tests for ContextBucketKeyBuilder
namespace AIPatterner.Tests.Unit.Services;

using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

public class ContextBucketKeyBuilderTests
{
    [Fact]
    public void BuildKey_ShouldFormatCorrectly()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ContextBucket:Format", "{dayType}*{timeBucket}*{location}" }
            })
            .Build();

        var builder = new ContextBucketKeyBuilder(config);
        var context = new ActionContext("evening", "weekday", "kitchen");

        var key = builder.BuildKey(context);

        key.Should().Be("weekday*evening*kitchen");
    }
}

