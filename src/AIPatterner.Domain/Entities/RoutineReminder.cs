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
    public double Confidence { get; private set; } // Probability/confidence level (0.0 to 1.0)
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastObservedAtUtc { get; private set; }
    public int ObservationCount { get; private set; } = 0;
    public Dictionary<string, string>? CustomData { get; private set; }
    
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
        Dictionary<string, string>? customData = null)
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

