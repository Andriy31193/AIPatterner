// DTO for routine with reminders
namespace AIPatterner.Application.DTOs;

public class RoutineDetailDto : RoutineDto
{
    public List<RoutineReminderDto> Reminders { get; set; } = new();
}

