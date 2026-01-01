// Helper class for generating occurrence patterns from DateTime
namespace AIPatterner.Application.Helpers;

using System;

public static class OccurrenceGenerator
{
    private static readonly string[] _dayNames = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

    /// <summary>
    /// Generates an occurrence pattern from a DateTime.
    /// Format: "Occurs every [DayOfWeek] at [HH:mm]"
    /// </summary>
    /// <param name="timestampUtc">The base timestamp</param>
    /// <returns>The occurrence string</returns>
    public static string GenerateOccurrence(DateTime timestampUtc)
    {
        var dayOfWeek = timestampUtc.DayOfWeek;
        var dayName = _dayNames[(int)dayOfWeek];
        var time = timestampUtc.ToString("HH:mm");
        
        return $"Occurs every {dayName} at {time}";
    }
}

