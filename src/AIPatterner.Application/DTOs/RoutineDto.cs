// DTO for Routine entity
namespace AIPatterner.Application.DTOs;

public class RoutineDto
{
    public Guid Id { get; set; }
    public string PersonId { get; set; } = string.Empty;
    public string IntentType { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastActivatedUtc { get; set; }
    public DateTime? ObservationWindowEndsUtc { get; set; }
    public int ObservationWindowMinutes { get; set; }
}

