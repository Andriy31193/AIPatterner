// Unit tests for ActionTransition domain entity
namespace AIPatterner.Tests.Unit.Domain;

using AIPatterner.Domain.Entities;
using FluentAssertions;
using Xunit;

public class ActionTransitionTests
{
    [Fact]
    public void UpdateWithObservation_ShouldUpdateConfidenceAndDelay()
    {
        var transition = new ActionTransition("person1", "action1", "action2", "context1");
        var observedDelay = TimeSpan.FromMinutes(7);
        var alpha = 0.1;
        var beta = 0.2;

        transition.UpdateWithObservation(observedDelay, alpha, beta);

        transition.OccurrenceCount.Should().Be(1);
        transition.Confidence.Should().BeApproximately(0.1, 0.01);
        transition.AverageDelay.Should().Be(observedDelay);
    }

    [Fact]
    public void ApplyDecay_ShouldReduceConfidence()
    {
        var transition = new ActionTransition("person1", "action1", "action2", "context1");
        transition.UpdateWithObservation(TimeSpan.FromMinutes(5), 0.1, 0.2);
        var initialConfidence = transition.Confidence;

        transition.ApplyDecay(0.01);

        transition.Confidence.Should().BeLessThan(initialConfidence);
    }

    [Fact]
    public void ReduceConfidence_ShouldReduceByFactor()
    {
        var transition = new ActionTransition("person1", "action1", "action2", "context1");
        transition.UpdateWithObservation(TimeSpan.FromMinutes(5), 0.1, 0.2);
        var initialConfidence = transition.Confidence;

        transition.ReduceConfidence(0.2);

        transition.Confidence.Should().BeApproximately(initialConfidence * 0.8, 0.01);
    }
}

