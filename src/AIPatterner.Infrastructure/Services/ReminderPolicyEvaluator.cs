// Domain service implementation for evaluating reminder policies
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Domain.Entities;
using AIPatterner.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class ReminderPolicyEvaluator : IReminderPolicyEvaluator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReminderPolicyEvaluator> _logger;

    public ReminderPolicyEvaluator(IConfiguration configuration, ILogger<ReminderPolicyEvaluator> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<ReminderPolicyDecision?> EvaluateAsync(
        ActionTransition transition,
        ActionContext currentContext,
        UserReminderPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        if (!preferences.Enabled)
        {
            return Task.FromResult<ReminderPolicyDecision?>(null);
        }

        var minOccurrences = _configuration.GetValue<int>("Policy:MinimumOccurrences", 3);
        if (transition.OccurrenceCount < minOccurrences)
        {
            _logger.LogDebug(
                "Transition {TransitionId} has insufficient occurrences: {Count} < {Min}",
                transition.Id, transition.OccurrenceCount, minOccurrences);
            return Task.FromResult<ReminderPolicyDecision?>(null);
        }

        var minConfidence = _configuration.GetValue<double>("Policy:MinimumConfidence", 0.4);
        if (transition.Confidence < minConfidence)
        {
            _logger.LogDebug(
                "Transition {TransitionId} has insufficient confidence: {Confidence:F2} < {Min:F2}",
                transition.Id, transition.Confidence, minConfidence);
            return Task.FromResult<ReminderPolicyDecision?>(null);
        }

        var contextBucket = _configuration.GetValue<string>("ContextBucket:Format", "{dayType}*{timeBucket}*{location}");
        var expectedBucket = contextBucket
            .Replace("{dayType}", currentContext.DayType ?? "unknown")
            .Replace("{timeBucket}", currentContext.TimeBucket ?? "unknown")
            .Replace("{location}", currentContext.Location ?? "unknown");

        if (transition.ContextBucket != expectedBucket)
        {
            _logger.LogDebug(
                "Transition {TransitionId} context mismatch: {TransitionBucket} != {CurrentBucket}",
                transition.Id, transition.ContextBucket, expectedBucket);
            return Task.FromResult<ReminderPolicyDecision?>(null);
        }

        if (!transition.AverageDelay.HasValue)
        {
            return Task.FromResult<ReminderPolicyDecision?>(null);
        }

        var checkAt = DateTime.UtcNow.Add(transition.AverageDelay.Value);
        var style = preferences.DefaultStyle;

        var decision = new ReminderPolicyDecision
        {
            ShouldSchedule = true,
            Style = style,
            SuggestedCheckAt = checkAt,
            Reason = $"Transition observed {transition.OccurrenceCount} times with confidence {transition.Confidence:F2}"
        };

        return Task.FromResult<ReminderPolicyDecision?>(decision);
    }
}

