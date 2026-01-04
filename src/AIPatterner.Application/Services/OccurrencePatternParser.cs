// Service for parsing occurrence patterns and calculating next execution times
namespace AIPatterner.Application.Services;

using System;
using System.Globalization;
using System.Text.RegularExpressions;

public class OccurrencePatternParser : IOccurrencePatternParser
{
    /// <summary>
    /// Calculates the next execution time based on the occurrence pattern.
    /// Returns null if the pattern cannot be parsed.
    /// </summary>
    public DateTime? CalculateNextExecutionTime(string? occurrencePattern, DateTime currentTime, DateTime? lastExecutionTime = null)
    {
        if (string.IsNullOrWhiteSpace(occurrencePattern))
        {
            return null;
        }

        var pattern = occurrencePattern.Trim().ToLowerInvariant();
        
        // Extract time from pattern (HH:mm or H:mm format)
        var timeMatch = Regex.Match(occurrencePattern, @"\b(\d{1,2}):(\d{2})\b");
        if (!timeMatch.Success)
        {
            return null; // Can't parse without time
        }

        var hours = int.Parse(timeMatch.Groups[1].Value);
        var minutes = int.Parse(timeMatch.Groups[2].Value);
        if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59)
        {
            return null;
        }

        var timeOfDay = new TimeSpan(hours, minutes, 0);
        var baseDate = lastExecutionTime?.Date ?? currentTime.Date;
        var nextTime = baseDate.Add(timeOfDay);

        // Parse pattern type
        if (pattern.Contains("daily") || pattern.Contains("every day"))
        {
            // Daily: next occurrence is today if time hasn't passed, otherwise tomorrow
            if (nextTime <= currentTime)
            {
                nextTime = nextTime.AddDays(1);
            }
            return nextTime;
        }

        if (pattern.Contains("weekdays"))
        {
            // Weekdays: Monday-Friday
            var dayOfWeek = nextTime.DayOfWeek;
            // If it's a weekday and time hasn't passed, use today
            if (dayOfWeek >= DayOfWeek.Monday && dayOfWeek <= DayOfWeek.Friday && nextTime > currentTime)
            {
                return nextTime;
            }
            // Otherwise find next weekday
            while (nextTime.DayOfWeek == DayOfWeek.Saturday || nextTime.DayOfWeek == DayOfWeek.Sunday || nextTime <= currentTime)
            {
                nextTime = nextTime.AddDays(1);
            }
            return nextTime;
        }

        if (pattern.Contains("weekends"))
        {
            // Weekends: Saturday-Sunday
            var dayOfWeek = nextTime.DayOfWeek;
            // If it's a weekend and time hasn't passed, use today
            if ((dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday) && nextTime > currentTime)
            {
                return nextTime;
            }
            // Otherwise find next weekend
            while (nextTime.DayOfWeek != DayOfWeek.Saturday && nextTime.DayOfWeek != DayOfWeek.Sunday || nextTime <= currentTime)
            {
                nextTime = nextTime.AddDays(1);
            }
            return nextTime;
        }

        // Parse "every X days"
        var everyXDaysMatch = Regex.Match(pattern, @"every\s+(\d+)\s+day");
        if (everyXDaysMatch.Success)
        {
            var days = int.Parse(everyXDaysMatch.Groups[1].Value);
            if (days > 0 && days <= 365)
            {
                // If time hasn't passed today, use today
                if (nextTime > currentTime)
                {
                    return nextTime;
                }
                // Otherwise add the interval
                nextTime = nextTime.AddDays(days);
                return nextTime;
            }
        }

        // Parse "Occurs every [DayName] at [time]" or "every [DayName]" (weekly)
        var dayNames = new[] { "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday" };
        for (int i = 0; i < dayNames.Length; i++)
        {
            // Match patterns like "Occurs every Monday at 14:30" or "every Monday at 09:00" or just "Monday"
            if (pattern.Contains($"every {dayNames[i]}") || 
                (pattern.Contains($"occurs every {dayNames[i]}") && pattern.Contains("at")) ||
                (i == (int)nextTime.DayOfWeek && pattern.Contains(dayNames[i]) && !pattern.Contains("daily") && !pattern.Contains("weekdays") && !pattern.Contains("weekends")))
            {
                var targetDayOfWeek = (DayOfWeek)i;
                var currentDayOfWeek = nextTime.DayOfWeek;
                var daysUntilTarget = ((int)targetDayOfWeek - (int)currentDayOfWeek + 7) % 7;
                
                if (daysUntilTarget == 0 && nextTime > currentTime)
                {
                    // Same day, time hasn't passed
                    return nextTime;
                }
                
                if (daysUntilTarget == 0)
                {
                    // Same day but time has passed, go to next week
                    daysUntilTarget = 7;
                }
                
                nextTime = nextTime.AddDays(daysUntilTarget);
                return nextTime;
            }
        }

        // Parse multiple days like "every Monday, Wednesday, Friday"
        var multipleDaysMatch = Regex.Match(pattern, @"every\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday)(?:,\s*(monday|tuesday|wednesday|thursday|friday|saturday|sunday))*", RegexOptions.IgnoreCase);
        if (multipleDaysMatch.Success)
        {
            var dayMatches = Regex.Matches(pattern, @"(monday|tuesday|wednesday|thursday|friday|saturday|sunday)", RegexOptions.IgnoreCase);
            var targetDays = dayMatches.Cast<Match>().Select(m => 
            {
                var dayName = m.Value.ToLowerInvariant();
                return Array.IndexOf(dayNames, dayName);
            }).Where(i => i >= 0).ToList();

            if (targetDays.Any())
            {
                // Find next matching day
                for (int i = 0; i < 14; i++) // Check up to 2 weeks ahead
                {
                    var checkDate = nextTime.AddDays(i);
                    var checkDayOfWeek = (int)checkDate.DayOfWeek;
                    if (targetDays.Contains(checkDayOfWeek) && checkDate > currentTime)
                    {
                        return checkDate;
                    }
                }
            }
        }

        // If no specific pattern matches, treat as daily
        if (nextTime <= currentTime)
        {
            nextTime = nextTime.AddDays(1);
        }
        return nextTime;
    }

    /// <summary>
    /// Checks if a reminder is due based on its occurrence pattern.
    /// </summary>
    public bool IsDue(string? occurrencePattern, DateTime checkAtUtc, DateTime currentTime)
    {
        if (string.IsNullOrWhiteSpace(occurrencePattern))
        {
            // No pattern: use CheckAtUtc as-is
            return checkAtUtc <= currentTime;
        }

        // For pattern-based reminders, CheckAtUtc represents the next scheduled time
        return checkAtUtc <= currentTime;
    }
}

