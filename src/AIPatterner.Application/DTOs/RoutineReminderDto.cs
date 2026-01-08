// DTO for RoutineReminder entity
namespace AIPatterner.Application.DTOs;

public class RoutineReminderDto
{
    public Guid Id { get; set; }
    public Guid RoutineId { get; set; }
    public string SuggestedAction { get; set; } = string.Empty;
    public string TimeContextBucket { get; set; } = "evening";
    public double Confidence { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastObservedAtUtc { get; set; }
    public int ObservationCount { get; set; }
    public Dictionary<string, string>? CustomData { get; set; }
    public SignalProfileDto? SignalProfile { get; set; }
    public DateTime? SignalProfileUpdatedAtUtc { get; set; }
    public int SignalProfileSamplesCount { get; set; }
    
    // Delay learning statistics
    public double DelaySampleCount { get; set; }
    public double? EmaDelaySeconds { get; set; }
    public double? EmaVarianceSeconds { get; set; }
    public double? MedianDelayApproxSeconds { get; set; }
    public double? P90DelayApproxSeconds { get; set; }
    public DateTime? DelayStatsLastUpdatedUtc { get; set; }
    public DateTime? DelayStatsLastDecayUtc { get; set; }
    public int DelayEvidenceCount { get; set; } // Count of items in DelayEvidenceJson
}

