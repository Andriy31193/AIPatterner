// Service implementation for retrieving current context
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Domain.Entities;
using Microsoft.Extensions.Logging;

public class ContextService : IContextService
{
    private readonly ILogger<ContextService> _logger;

    public ContextService(ILogger<ContextService> logger)
    {
        _logger = logger;
    }

    public Task<ActionContext> GetCurrentContextAsync(string personId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var timeBucket = GetTimeBucket(now);
        var dayType = GetDayType(now);

        var context = new ActionContext(
            timeBucket,
            dayType,
            null,
            new List<string> { personId },
            new Dictionary<string, string>());

        return Task.FromResult(context);
    }

    public Task<double> EvaluateInterruptionCostAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var cost = 0.0;

        if (context.StateSignals.ContainsKey("in_call") && context.StateSignals["in_call"] == "true")
        {
            cost += 0.5;
        }

        if (context.StateSignals.ContainsKey("calendar_busy") && context.StateSignals["calendar_busy"] == "true")
        {
            cost += 0.3;
        }

        return Task.FromResult(Math.Min(1.0, cost));
    }

    private static string GetTimeBucket(DateTime utcTime)
    {
        var hour = utcTime.Hour;
        return hour switch
        {
            >= 6 and < 12 => "morning",
            >= 12 and < 17 => "afternoon",
            >= 17 and < 22 => "evening",
            _ => "night"
        };
    }

    private static string GetDayType(DateTime utcTime)
    {
        return utcTime.DayOfWeek switch
        {
            DayOfWeek.Saturday or DayOfWeek.Sunday => "weekend",
            _ => "weekday"
        };
    }
}

