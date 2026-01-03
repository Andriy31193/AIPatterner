// Domain entity representing a scheduled reminder check candidate
namespace AIPatterner.Domain.Entities;

public enum ReminderStyle
{
    Ask,
    Suggest,
    Silent
}

public enum ReminderCandidateStatus
{
    Scheduled,
    Executed,
    Skipped,
    Expired
}

/// <summary>
/// Represents the inferred occurrence pattern status based on accumulated evidence.
/// Patterns are inferred gradually, not immediately.
/// </summary>
public enum PatternInferenceStatus
{
    /// <summary>
    /// No pattern inferred yet - too little evidence
    /// </summary>
    Unknown,
    
    /// <summary>
    /// Evidence suggests flexible timing - not clearly daily or weekly
    /// </summary>
    Flexible,
    
    /// <summary>
    /// Strong evidence for daily occurrence pattern
    /// </summary>
    Daily,
    
    /// <summary>
    /// Strong evidence for weekly occurrence on a specific weekday
    /// </summary>
    Weekly
}

public class ReminderCandidate
{
    public Guid Id { get; private set; }
    public string PersonId { get; private set; }
    public Guid? UserId { get; private set; } // Nullable for backward compatibility
    public string SuggestedAction { get; private set; }
    public DateTime CheckAtUtc { get; private set; }
    public Guid? TransitionId { get; private set; }
    public ReminderStyle Style { get; private set; }
    public ReminderCandidateStatus Status { get; private set; }
    public ReminderDecision? Decision { get; private set; }
    public double Confidence { get; private set; } // Probability/confidence level (0.0 to 1.0)
    public string? Occurrence { get; private set; } // Occurrence pattern (e.g., "daily", "weekly", "every 3 days", "weekdays")
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ExecutedAtUtc { get; private set; }
    public Guid? SourceEventId { get; private set; } // Event ID that created this reminder
    public Dictionary<string, string>? CustomData { get; private set; } // Custom data from source event
    
    // Evidence tracking fields for gradual pattern learning
    /// <summary>
    /// The center time of day (time-only, no date) when this reminder typically occurs.
    /// Used for matching events within a time window, regardless of day.
    /// </summary>
    public TimeSpan? TimeWindowCenter { get; private set; }
    
    /// <summary>
    /// The size of the time window in minutes (e.g., 45 means ±45 minutes from TimeWindowCenter).
    /// Default is 45 minutes.
    /// </summary>
    public int TimeWindowSizeMinutes { get; private set; } = 45;
    
    /// <summary>
    /// Number of matching events that have reinforced this reminder hypothesis.
    /// </summary>
    public int EvidenceCount { get; private set; } = 0;
    
    /// <summary>
    /// List of dates (date-only, no time) when events matching this reminder were observed.
    /// Stored as JSON array of ISO date strings.
    /// </summary>
    public string? ObservedDaysJson { get; private set; }
    
    /// <summary>
    /// Histogram of observations per day of week (0=Sunday, 6=Saturday).
    /// Stored as JSON object: {"0": 2, "1": 0, "2": 0, ...}
    /// </summary>
    public string? ObservedDayOfWeekHistogramJson { get; private set; }
    
    /// <summary>
    /// The inferred pattern status based on accumulated evidence.
    /// Starts as Unknown and gradually becomes Daily, Weekly, or Flexible.
    /// </summary>
    public PatternInferenceStatus PatternInferenceStatus { get; private set; } = PatternInferenceStatus.Unknown;
    
    /// <summary>
    /// If PatternInferenceStatus is Weekly, this stores the inferred weekday (0=Sunday, 6=Saturday).
    /// </summary>
    public int? InferredWeekday { get; private set; }

    private ReminderCandidate() { } // EF Core

    public ReminderCandidate(
        string personId,
        string suggestedAction,
        DateTime checkAtUtc,
        ReminderStyle style,
        Guid? userId = null,
        Guid? transitionId = null,
        double confidence = 0.5,
        string? occurrence = null,
        Guid? sourceEventId = null,
        Dictionary<string, string>? customData = null)
    {
        if (string.IsNullOrWhiteSpace(personId))
            throw new ArgumentException("PersonId cannot be null or empty", nameof(personId));
        if (string.IsNullOrWhiteSpace(suggestedAction))
            throw new ArgumentException("SuggestedAction cannot be null or empty", nameof(suggestedAction));
        if (confidence < 0.0 || confidence > 1.0)
            throw new ArgumentException("Confidence must be between 0.0 and 1.0", nameof(confidence));

        Id = Guid.NewGuid();
        PersonId = personId;
        UserId = userId;
        SuggestedAction = suggestedAction;
        CheckAtUtc = checkAtUtc;
        Style = style;
        TransitionId = transitionId;
        Confidence = confidence;
        Occurrence = occurrence;
        SourceEventId = sourceEventId;
        CustomData = customData;
        Status = ReminderCandidateStatus.Scheduled;
        CreatedAtUtc = DateTime.UtcNow;
        
        // Initialize evidence tracking from the first event
        InitializeEvidenceTracking(checkAtUtc);
    }
    
    /// <summary>
    /// Initializes evidence tracking fields from the first observed event timestamp.
    /// </summary>
    private void InitializeEvidenceTracking(DateTime firstEventTimestamp)
    {
        TimeWindowCenter = firstEventTimestamp.TimeOfDay;
        EvidenceCount = 1;
        
        // Record the first observed day
        var firstDate = firstEventTimestamp.Date;
        var observedDays = new List<DateTime> { firstDate };
        ObservedDaysJson = System.Text.Json.JsonSerializer.Serialize(observedDays.Select(d => d.ToString("yyyy-MM-dd")));
        
        // Initialize day-of-week histogram
        var histogram = new Dictionary<int, int>();
        for (int i = 0; i < 7; i++)
        {
            histogram[i] = 0;
        }
        histogram[(int)firstEventTimestamp.DayOfWeek] = 1;
        ObservedDayOfWeekHistogramJson = System.Text.Json.JsonSerializer.Serialize(histogram);
        
        PatternInferenceStatus = PatternInferenceStatus.Unknown;
    }

    public void IncreaseConfidence(double stepValue)
    {
        if (stepValue < 0.0)
            throw new ArgumentException("Step value must be non-negative", nameof(stepValue));
        
        Confidence = Math.Min(1.0, Confidence + stepValue);
    }

    public void DecreaseConfidence(double stepValue)
    {
        if (stepValue < 0.0)
            throw new ArgumentException("Step value must be non-negative", nameof(stepValue));
        
        Confidence = Math.Max(0.0, Confidence - stepValue);
    }

    public void UpdateConfidence(double value, ProbabilityAction action)
    {
        if (value < 0.0)
            throw new ArgumentException("Value must be non-negative", nameof(value));

        if (action == ProbabilityAction.Increase)
        {
            IncreaseConfidence(value);
        }
        else
        {
            DecreaseConfidence(value);
        }
    }

    public void SetOccurrence(string? occurrence)
    {
        Occurrence = occurrence;
    }

    public void UpdateCheckAtUtc(DateTime checkAtUtc)
    {
        CheckAtUtc = checkAtUtc;
    }

    public void UpdateCustomData(Dictionary<string, string>? customData)
    {
        CustomData = customData;
    }

    public void MarkAsExecuted(ReminderDecision decision)
    {
        if (decision == null)
            throw new ArgumentNullException(nameof(decision));

        Status = ReminderCandidateStatus.Executed;
        Decision = decision;
        ExecutedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsSkipped()
    {
        Status = ReminderCandidateStatus.Skipped;
        ExecutedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsExpired()
    {
        Status = ReminderCandidateStatus.Expired;
    }

    public bool IsDue(DateTime now)
    {
        return Status == ReminderCandidateStatus.Scheduled && CheckAtUtc <= now;
    }
    
    /// <summary>
    /// Records a new matching event observation, updating evidence tracking.
    /// This is called when an event matches this reminder's ActionType and TimeWindow.
    /// </summary>
    public void RecordEvidence(DateTime eventTimestamp)
    {
        if (!TimeWindowCenter.HasValue)
        {
            // First evidence - initialize tracking
            InitializeEvidenceTracking(eventTimestamp);
            return;
        }
        
        EvidenceCount++;
        
        // Update observed days
        var eventDate = eventTimestamp.Date;
        var observedDays = GetObservedDays();
        if (!observedDays.Contains(eventDate))
        {
            observedDays.Add(eventDate);
            ObservedDaysJson = System.Text.Json.JsonSerializer.Serialize(observedDays.Select(d => d.ToString("yyyy-MM-dd")));
        }
        
        // Update day-of-week histogram
        var histogram = GetDayOfWeekHistogram();
        var dayOfWeek = (int)eventTimestamp.DayOfWeek;
        histogram[dayOfWeek] = histogram.GetValueOrDefault(dayOfWeek, 0) + 1;
        ObservedDayOfWeekHistogramJson = System.Text.Json.JsonSerializer.Serialize(histogram);
        
        // Update time window center using exponential moving average (EMA) for gradual adaptation
        // Alpha = 0.1 means new observations have 10% weight, existing center has 90%
        const double alpha = 0.1;
        var newTimeOfDay = eventTimestamp.TimeOfDay;
        var currentCenter = TimeWindowCenter.Value;
        var timeDiff = (newTimeOfDay - currentCenter).TotalMinutes;
        
        // Handle midnight wraparound
        if (timeDiff > 12 * 60) timeDiff -= 24 * 60;
        if (timeDiff < -12 * 60) timeDiff += 24 * 60;
        
        var adjustedMinutes = currentCenter.TotalMinutes + (alpha * timeDiff);
        if (adjustedMinutes < 0) adjustedMinutes += 24 * 60;
        if (adjustedMinutes >= 24 * 60) adjustedMinutes -= 24 * 60;
        
        TimeWindowCenter = TimeSpan.FromMinutes(adjustedMinutes);
    }
    
    /// <summary>
    /// Gets the list of observed dates (parsed from JSON).
    /// </summary>
    public List<DateTime> GetObservedDays()
    {
        if (string.IsNullOrEmpty(ObservedDaysJson))
        {
            return new List<DateTime>();
        }
        
        try
        {
            var dateStrings = System.Text.Json.JsonSerializer.Deserialize<List<string>>(ObservedDaysJson);
            return dateStrings?.Select(d => DateTime.Parse(d)).ToList() ?? new List<DateTime>();
        }
        catch
        {
            return new List<DateTime>();
        }
    }
    
    /// <summary>
    /// Gets the day-of-week histogram (parsed from JSON).
    /// </summary>
    public Dictionary<int, int> GetDayOfWeekHistogram()
    {
        if (string.IsNullOrEmpty(ObservedDayOfWeekHistogramJson))
        {
            var histogram = new Dictionary<int, int>();
            for (int i = 0; i < 7; i++)
            {
                histogram[i] = 0;
            }
            return histogram;
        }
        
        try
        {
            var histogram = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, int>>(ObservedDayOfWeekHistogramJson);
            return histogram ?? new Dictionary<int, int>();
        }
        catch
        {
            var histogram = new Dictionary<int, int>();
            for (int i = 0; i < 7; i++)
            {
                histogram[i] = 0;
            }
            return histogram;
        }
    }
    
    /// <summary>
    /// Updates the inferred pattern status and occurrence string based on accumulated evidence.
    /// This should be called periodically or after significant evidence accumulation.
    /// </summary>
    public void UpdateInferredPattern(int minDailyEvidence = 3, int minWeeklyEvidence = 3)
    {
        if (EvidenceCount < minDailyEvidence)
        {
            PatternInferenceStatus = PatternInferenceStatus.Unknown;
            InferredWeekday = null;
            Occurrence = null;
            return;
        }
        
        var histogram = GetDayOfWeekHistogram();
        var observedDays = GetObservedDays();
        
        if (observedDays.Count == 0)
        {
            PatternInferenceStatus = PatternInferenceStatus.Unknown;
            return;
        }
        
        // Check for weekly pattern: same weekday across multiple weeks
        var weekdayCounts = histogram.Values.Where(c => c > 0).ToList();
        var maxWeekdayCount = histogram.Values.Max();
        var maxWeekday = histogram.FirstOrDefault(kvp => kvp.Value == maxWeekdayCount).Key;
        
        // Weekly pattern: one weekday dominates, and we have multiple weeks of evidence
        if (maxWeekdayCount >= minWeeklyEvidence && weekdayCounts.Count == 1)
        {
            // Verify it's across multiple weeks, not just consecutive days
            var datesForWeekday = observedDays.Where(d => (int)d.DayOfWeek == maxWeekday).OrderBy(d => d).ToList();
            if (datesForWeekday.Count >= minWeeklyEvidence)
            {
                // Check if dates span multiple weeks (at least 7 days apart for first and last)
                var firstDate = datesForWeekday.First();
                var lastDate = datesForWeekday.Last();
                var daysSpan = (lastDate - firstDate).TotalDays;
                
                if (daysSpan >= 7) // At least one week apart
                {
                    PatternInferenceStatus = PatternInferenceStatus.Weekly;
                    InferredWeekday = maxWeekday;
                    var dayName = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" }[maxWeekday];
                    var timeStr = TimeWindowCenter?.ToString(@"hh\:mm") ?? "00:00";
                    Occurrence = $"Occurs every {dayName} at {timeStr}";
                    return;
                }
            }
        }
        
        // Check for daily pattern: observed on multiple consecutive or near-consecutive days
        var sortedDays = observedDays.OrderBy(d => d).ToList();
        var consecutiveCount = 1;
        var maxConsecutive = 1;
        
        for (int i = 1; i < sortedDays.Count; i++)
        {
            var daysDiff = (sortedDays[i] - sortedDays[i - 1]).TotalDays;
            if (daysDiff <= 2) // Allow 1-2 day gaps
            {
                consecutiveCount++;
                maxConsecutive = Math.Max(maxConsecutive, consecutiveCount);
            }
            else
            {
                consecutiveCount = 1;
            }
        }
        
        if (maxConsecutive >= minDailyEvidence)
        {
            PatternInferenceStatus = PatternInferenceStatus.Daily;
            InferredWeekday = null;
            var timeStr = TimeWindowCenter?.ToString(@"hh\:mm") ?? "00:00";
            Occurrence = $"Occurs daily at {timeStr}";
            return;
        }
        
        // Otherwise, pattern is flexible
        PatternInferenceStatus = PatternInferenceStatus.Flexible;
        InferredWeekday = null;
        var flexibleTimeStr = TimeWindowCenter?.ToString(@"hh\:mm") ?? "00:00";
        Occurrence = $"Occurs around {flexibleTimeStr} (flexible timing)";
    }
}

