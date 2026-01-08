namespace AIPatterner.Infrastructure.Services;

using Microsoft.Extensions.Configuration;

/// <summary>
/// Selects a time-of-day bucket for a routine activation.
/// Uses UTC timestamps for storage; converts to a configurable "local time" for bucket selection only.
/// </summary>
public static class RoutineTimeContextBucketSelector
{
    public static string SelectBucket(DateTime activationTimestampUtc, IConfiguration configuration)
    {
        var offsetMinutes = configuration.GetValue<int?>("Routine:TimeContext:LocalTimeOffsetMinutes") ?? 0;
        var local = activationTimestampUtc.AddMinutes(offsetMinutes);

        // Configurable ranges (local time). Defaults:
        // Morning:   05:00 - 12:00
        // Afternoon: 12:00 - 17:00
        // Evening:   17:00 - 22:00
        // Night:     22:00 - 05:00 (wrap)
        var morningStart = ParseTime(configuration["Routine:TimeContext:MorningStart"], new TimeSpan(5, 0, 0));
        var afternoonStart = ParseTime(configuration["Routine:TimeContext:AfternoonStart"], new TimeSpan(12, 0, 0));
        var eveningStart = ParseTime(configuration["Routine:TimeContext:EveningStart"], new TimeSpan(17, 0, 0));
        var nightStart = ParseTime(configuration["Routine:TimeContext:NightStart"], new TimeSpan(22, 0, 0));

        var t = local.TimeOfDay;

        // Note: ranges are evaluated by comparing to start boundaries.
        // Night wraps across midnight.
        if (IsInRange(t, morningStart, afternoonStart))
            return "morning";
        if (IsInRange(t, afternoonStart, eveningStart))
            return "afternoon";
        if (IsInRange(t, eveningStart, nightStart))
            return "evening";
        return "night";
    }

    private static TimeSpan ParseTime(string? value, TimeSpan fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        return TimeSpan.TryParse(value, out var t) ? t : fallback;
    }

    private static bool IsInRange(TimeSpan t, TimeSpan startInclusive, TimeSpan endExclusive)
    {
        // Non-wrapping range
        if (startInclusive <= endExclusive)
            return t >= startInclusive && t < endExclusive;

        // Wrapping range (e.g., 22:00-05:00)
        return t >= startInclusive || t < endExclusive;
    }
}


