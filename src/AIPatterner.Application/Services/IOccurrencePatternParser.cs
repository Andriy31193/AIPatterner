// Interface for parsing occurrence patterns and calculating next execution times
namespace AIPatterner.Application.Services;

using System;

public interface IOccurrencePatternParser
{
    /// <summary>
    /// Calculates the next execution time based on the occurrence pattern.
    /// Returns null if the pattern cannot be parsed.
    /// </summary>
    DateTime? CalculateNextExecutionTime(string? occurrencePattern, DateTime currentTime, DateTime? lastExecutionTime = null);

    /// <summary>
    /// Checks if a reminder is due based on its occurrence pattern.
    /// </summary>
    bool IsDue(string? occurrencePattern, DateTime checkAtUtc, DateTime currentTime);
}

