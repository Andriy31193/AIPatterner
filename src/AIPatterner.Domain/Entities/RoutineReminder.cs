// Domain entity representing a reminder within a routine
namespace AIPatterner.Domain.Entities;

/// <summary>
/// A RoutineReminder is similar to ReminderCandidate but belongs to a Routine.
/// It represents a learned action that typically follows an intent (StateChange event).
/// </summary>
public class RoutineReminder
{
    public Guid Id { get; private set; }
    public Guid RoutineId { get; private set; }
    public string PersonId { get; private set; }
    public string SuggestedAction { get; private set; }
    
    /// <summary>
    /// Time-of-day context bucket this reminder belongs to (selected only at routine activation).
    /// Buckets must never learn or modify each other.
    /// </summary>
    public string TimeContextBucket { get; private set; } = "evening";
    public double Confidence { get; private set; } // Probability/confidence level (0.0 to 1.0)
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastObservedAtUtc { get; private set; }
    public int ObservationCount { get; private set; } = 0;
    public Dictionary<string, string>? CustomData { get; private set; }

    // --- Delay learning statistical summary (used for decisions; updated only within learning window) ---
    public double DelaySampleCount { get; private set; } = 0.0; // decayed
    public double? EmaDelaySeconds { get; private set; } // mean
    public double? EmaVarianceSeconds { get; private set; } // variance
    public string? DelayHistogramJson { get; private set; } // JSON: { "60": 1.2, "120": 0.8, ... } counts are doubles to support weighting/decay
    public double? MedianDelayApproxSeconds { get; private set; }
    public double? P90DelayApproxSeconds { get; private set; }
    public DateTime? DelayStatsLastUpdatedUtc { get; private set; }
    public DateTime? DelayStatsLastDecayUtc { get; private set; }

    // --- Bounded raw evidence (debug/explainability only; never used directly for execution timing) ---
    public string? DelayEvidenceJson { get; private set; } // JSON array of evidence objects (bounded)
    
    /// <summary>
    /// List of user prompts associated with this routine reminder.
    /// Stored as JSONB array of objects: [{ "text": string, "timestampUtc": DateTime }]
    /// </summary>
    public string? UserPromptsListJson { get; private set; }
    
    /// <summary>
    /// Whether this reminder is safe to auto-execute when confidence is high.
    /// Default is false - only reminders explicitly marked as safe can be auto-executed.
    /// </summary>
    public bool IsSafeToAutoExecute { get; private set; } = false;
    
    /// <summary>
    /// Signal profile (baseline vector) for this reminder.
    /// Stored as JSONB object mapping sensorId -> { weight: double, normalizedValue: number }.
    /// </summary>
    public string? SignalProfileJson { get; private set; }
    
    /// <summary>
    /// Timestamp when the signal profile was last updated.
    /// </summary>
    public DateTime? SignalProfileUpdatedAtUtc { get; private set; }
    
    /// <summary>
    /// Number of times the signal profile has been updated (sample count).
    /// </summary>
    public int SignalProfileSamplesCount { get; private set; } = 0;

    private RoutineReminder() { } // EF Core

    public RoutineReminder(
        Guid routineId,
        string personId,
        string suggestedAction,
        double confidence,
        Dictionary<string, string>? customData = null,
        string timeContextBucket = "evening")
    {
        if (string.IsNullOrWhiteSpace(personId))
            throw new ArgumentException("PersonId cannot be null or empty", nameof(personId));
        if (string.IsNullOrWhiteSpace(suggestedAction))
            throw new ArgumentException("SuggestedAction cannot be null or empty", nameof(suggestedAction));
        if (confidence < 0.0 || confidence > 1.0)
            throw new ArgumentException("Confidence must be between 0.0 and 1.0", nameof(confidence));

        Id = Guid.NewGuid();
        RoutineId = routineId;
        PersonId = personId;
        SuggestedAction = suggestedAction;
        TimeContextBucket = string.IsNullOrWhiteSpace(timeContextBucket) ? "evening" : timeContextBucket;
        Confidence = confidence;
        CreatedAtUtc = DateTime.UtcNow;
        CustomData = customData;
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

    /// <summary>
    /// Records an observation of this action occurring within the routine window.
    /// </summary>
    public void RecordObservation(DateTime observedAtUtc)
    {
        ObservationCount++;
        LastObservedAtUtc = observedAtUtc;
    }

    public List<RoutineDelayEvidenceItem> GetDelayEvidence()
    {
        if (string.IsNullOrWhiteSpace(DelayEvidenceJson))
        {
            return new List<RoutineDelayEvidenceItem>();
        }

        try
        {
            var items = System.Text.Json.JsonSerializer.Deserialize<List<RoutineDelayEvidenceItem>>(DelayEvidenceJson);
            return items ?? new List<RoutineDelayEvidenceItem>();
        }
        catch
        {
            return new List<RoutineDelayEvidenceItem>();
        }
    }

    public Dictionary<int, double> GetDelayHistogram()
    {
        if (string.IsNullOrWhiteSpace(DelayHistogramJson))
        {
            return new Dictionary<int, double>();
        }

        try
        {
            var raw = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(DelayHistogramJson);
            if (raw == null) return new Dictionary<int, double>();
            return raw
                .Select(kvp => new { Key = int.TryParse(kvp.Key, out var k) ? k : -1, kvp.Value })
                .Where(x => x.Key >= 0)
                .ToDictionary(x => x.Key, x => x.Value);
        }
        catch
        {
            return new Dictionary<int, double>();
        }
    }

    public void ApplyDelayDecay(DateTime nowUtc, double halfLifeDays)
    {
        if (halfLifeDays <= 0)
        {
            return;
        }

        if (!DelayStatsLastDecayUtc.HasValue)
        {
            DelayStatsLastDecayUtc = nowUtc;
            return;
        }

        var dt = nowUtc - DelayStatsLastDecayUtc.Value;
        if (dt <= TimeSpan.Zero)
        {
            return;
        }

        var days = dt.TotalDays;
        var decayFactor = Math.Pow(0.5, days / halfLifeDays);

        DelaySampleCount *= decayFactor;

        var histogram = GetDelayHistogram();
        if (histogram.Count > 0)
        {
            var decayed = histogram.ToDictionary(kvp => kvp.Key, kvp => kvp.Value * decayFactor);
            DelayHistogramJson = System.Text.Json.JsonSerializer.Serialize(
                decayed.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value));
        }

        if (EmaVarianceSeconds.HasValue)
        {
            EmaVarianceSeconds = Math.Max(1.0, EmaVarianceSeconds.Value * decayFactor);
        }

        DelayStatsLastDecayUtc = nowUtc;
    }

    public RoutineDelayStatsUpdateResult RecordDelayObservation(
        DateTime routineActivationTimestampUtc,
        DateTime eventTimestampUtc,
        double observedDelaySeconds,
        Guid sourceEventId,
        double baseAlpha,
        double halfLifeDays,
        int maxEvidenceItems)
    {
        // Apply decay first so old data fades naturally
        ApplyDelayDecay(eventTimestampUtc, halfLifeDays);

        // Outlier detection based on current EMA + variance
        var isOutlier = false;
        var weight = 1.0;
        if (EmaDelaySeconds.HasValue && EmaVarianceSeconds.HasValue && EmaVarianceSeconds.Value > 1e-6)
        {
            var z = Math.Abs(observedDelaySeconds - EmaDelaySeconds.Value) / Math.Sqrt(EmaVarianceSeconds.Value);
            if (z > 3.0)
            {
                isOutlier = true;
                weight = 0.1;
            }
        }

        var alpha = Math.Clamp(baseAlpha * weight, 0.01, 0.5);

        // EMA mean/variance update
        if (!EmaDelaySeconds.HasValue)
        {
            EmaDelaySeconds = observedDelaySeconds;
            EmaVarianceSeconds = 60.0; // start with ~1 minute variance
        }
        else
        {
            var oldMean = EmaDelaySeconds.Value;
            var newMean = oldMean + alpha * (observedDelaySeconds - oldMean);
            var err = observedDelaySeconds - newMean;
            var oldVar = EmaVarianceSeconds ?? 60.0;
            var newVar = (1 - alpha) * oldVar + alpha * (err * err);

            EmaDelaySeconds = newMean;
            EmaVarianceSeconds = Math.Max(1.0, newVar);
        }

        // Update histogram (weighted)
        var bins = GetDefaultDelayHistogramBinsSeconds();
        var binUpper = bins.FirstOrDefault(b => observedDelaySeconds <= b);
        if (binUpper == 0)
        {
            binUpper = bins.Last();
        }

        var histogram = GetDelayHistogram();
        histogram[binUpper] = histogram.GetValueOrDefault(binUpper, 0.0) + weight;
        DelayHistogramJson = System.Text.Json.JsonSerializer.Serialize(
            histogram.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value));

        DelaySampleCount += weight;

        // Quantile approximations from histogram
        var (median, p90) = EstimateQuantilesFromHistogram(histogram, 0.5, 0.9);
        MedianDelayApproxSeconds = median;
        P90DelayApproxSeconds = p90;

        DelayStatsLastUpdatedUtc = eventTimestampUtc;

        // Evidence (bounded)
        var evidence = GetDelayEvidence();
        evidence.Add(new RoutineDelayEvidenceItem
        {
            RoutineActivationTimestampUtc = routineActivationTimestampUtc,
            EventTimestampUtc = eventTimestampUtc,
            ObservedDelaySeconds = observedDelaySeconds,
            ConfidenceWeight = weight,
            IsOutlier = isOutlier,
            SourceEventId = sourceEventId,
            CreatedAtUtc = DateTime.UtcNow
        });

        if (evidence.Count > maxEvidenceItems)
        {
            evidence = evidence
                .OrderByDescending(e => e.CreatedAtUtc)
                .Take(maxEvidenceItems)
                .OrderBy(e => e.CreatedAtUtc)
                .ToList();
        }

        DelayEvidenceJson = System.Text.Json.JsonSerializer.Serialize(evidence);

        return new RoutineDelayStatsUpdateResult
        {
            IsOutlier = isOutlier,
            ConfidenceWeight = weight,
            MedianDelayApproxSeconds = MedianDelayApproxSeconds,
            P90DelayApproxSeconds = P90DelayApproxSeconds,
            DelaySampleCount = DelaySampleCount
        };
    }

    private static int[] GetDefaultDelayHistogramBinsSeconds()
    {
        // Upper bounds in seconds (covers ~30s .. 4h, last bucket acts as overflow)
        return new[]
        {
            30, 60, 90, 120, 180, 240, 300, 420, 600, 900, 1200, 1800, 3600, 7200, 14400
        };
    }

    private static (double? q1, double? q2) EstimateQuantilesFromHistogram(
        Dictionary<int, double> histogram,
        double qA,
        double qB)
    {
        if (histogram.Count == 0)
        {
            return (null, null);
        }

        var total = histogram.Values.Sum();
        if (total <= 0)
        {
            return (null, null);
        }

        double? Find(double q)
        {
            var target = q * total;
            var cum = 0.0;
            foreach (var kvp in histogram.OrderBy(k => k.Key))
            {
                cum += kvp.Value;
                if (cum >= target)
                {
                    return kvp.Key;
                }
            }
            return histogram.Keys.Max();
        }

        return (Find(qA), Find(qB));
    }

    public void UpdateCustomData(Dictionary<string, string>? customData)
    {
        CustomData = customData;
    }

    /// <summary>
    /// Appends a user prompt to the reminder's userPromptsList.
    /// </summary>
    public void AppendUserPrompt(string promptText, DateTime timestampUtc)
    {
        if (string.IsNullOrWhiteSpace(promptText))
        {
            return;
        }

        var prompts = GetUserPrompts();
        prompts.Add(new UserPrompt { Text = promptText, TimestampUtc = timestampUtc });
        UserPromptsListJson = System.Text.Json.JsonSerializer.Serialize(prompts);
    }

    /// <summary>
    /// Gets the list of user prompts (parsed from JSON).
    /// </summary>
    public List<UserPrompt> GetUserPrompts()
    {
        if (string.IsNullOrEmpty(UserPromptsListJson))
        {
            return new List<UserPrompt>();
        }

        try
        {
            var prompts = System.Text.Json.JsonSerializer.Deserialize<List<UserPrompt>>(UserPromptsListJson);
            return prompts ?? new List<UserPrompt>();
        }
        catch
        {
            return new List<UserPrompt>();
        }
    }

    /// <summary>
    /// Sets whether this reminder is safe to auto-execute.
    /// </summary>
    public void SetIsSafeToAutoExecute(bool isSafe)
    {
        IsSafeToAutoExecute = isSafe;
    }
    
    /// <summary>
    /// Gets the signal profile (parsed from JSON).
    /// </summary>
    public AIPatterner.Domain.ValueObjects.SignalProfile? GetSignalProfile()
    {
        if (string.IsNullOrEmpty(SignalProfileJson))
        {
            return null;
        }

        try
        {
            var profile = System.Text.Json.JsonSerializer.Deserialize<AIPatterner.Domain.ValueObjects.SignalProfile>(SignalProfileJson);
            return profile;
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Updates the signal profile using exponential moving average (EMA).
    /// </summary>
    public void UpdateSignalProfile(AIPatterner.Domain.ValueObjects.SignalProfile eventProfile, double alpha)
    {
        if (eventProfile == null || eventProfile.Signals == null || eventProfile.Signals.Count == 0)
        {
            return;
        }
        
        var currentProfile = GetSignalProfile();
        
        if (currentProfile == null || currentProfile.Signals == null || currentProfile.Signals.Count == 0)
        {
            // First-time baseline creation: use event profile directly
            SignalProfileJson = System.Text.Json.JsonSerializer.Serialize(eventProfile);
            SignalProfileSamplesCount = 1;
            SignalProfileUpdatedAtUtc = DateTime.UtcNow;
            return;
        }
        
        // EMA update: B_new = (1-alpha) * B_old + alpha * E
        var updatedSignals = new Dictionary<string, AIPatterner.Domain.ValueObjects.SignalProfileEntry>();
        
        // Update signals present in event
        foreach (var eventSignal in eventProfile.Signals)
        {
            if (currentProfile.Signals.ContainsKey(eventSignal.Key))
            {
                var oldEntry = currentProfile.Signals[eventSignal.Key];
                var newEntry = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = (1 - alpha) * oldEntry.Weight + alpha * eventSignal.Value.Weight,
                    NormalizedValue = (1 - alpha) * oldEntry.NormalizedValue + alpha * eventSignal.Value.NormalizedValue
                };
                updatedSignals[eventSignal.Key] = newEntry;
            }
            else
            {
                // New signal: initialize with event value
                updatedSignals[eventSignal.Key] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = alpha * eventSignal.Value.Weight,
                    NormalizedValue = alpha * eventSignal.Value.NormalizedValue
                };
            }
        }
        
        // Decay signals not present in event (optional: allow gradual forgetting)
        foreach (var oldSignal in currentProfile.Signals)
        {
            if (!updatedSignals.ContainsKey(oldSignal.Key))
            {
                // Apply decay: multiply by (1-alpha)
                var decayedEntry = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = (1 - alpha) * oldSignal.Value.Weight,
                    NormalizedValue = (1 - alpha) * oldSignal.Value.NormalizedValue
                };
                // Only keep if weight is still significant (above threshold)
                if (decayedEntry.Weight > 0.01)
                {
                    updatedSignals[oldSignal.Key] = decayedEntry;
                }
            }
        }
        
        var updatedProfile = new AIPatterner.Domain.ValueObjects.SignalProfile
        {
            Signals = updatedSignals
        };
        
        SignalProfileJson = System.Text.Json.JsonSerializer.Serialize(updatedProfile);
        SignalProfileSamplesCount++;
        SignalProfileUpdatedAtUtc = DateTime.UtcNow;
    }
}

public class RoutineDelayEvidenceItem
{
    public DateTime RoutineActivationTimestampUtc { get; set; }
    public DateTime EventTimestampUtc { get; set; }
    public double ObservedDelaySeconds { get; set; }
    public double ConfidenceWeight { get; set; }
    public bool IsOutlier { get; set; }
    public Guid SourceEventId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class RoutineDelayStatsUpdateResult
{
    public bool IsOutlier { get; set; }
    public double ConfidenceWeight { get; set; }
    public double? MedianDelayApproxSeconds { get; set; }
    public double? P90DelayApproxSeconds { get; set; }
    public double DelaySampleCount { get; set; }
}


