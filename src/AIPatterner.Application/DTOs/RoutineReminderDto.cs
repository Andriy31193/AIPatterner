// DTO for RoutineReminder entity
namespace AIPatterner.Application.DTOs;

public class RoutineReminderDto
{
    public Guid Id { get; set; }
    public Guid RoutineId { get; set; }
    public string SuggestedAction { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastObservedAtUtc { get; set; }
    public Dictionary<string, string>? CustomData { get; set; }
    public SignalProfileDto? SignalProfile { get; set; }
    public DateTime? SignalProfileUpdatedAtUtc { get; set; }
    public int SignalProfileSamplesCount { get; set; }
}

