// DTO for user reminder preferences
namespace AIPatterner.Application.DTOs;

using AIPatterner.Domain.Entities;

public class UserReminderPreferencesDto
{
    public string PersonId { get; set; } = string.Empty;
    public ReminderStyle DefaultStyle { get; set; }
    public int DailyLimit { get; set; }
    public string MinimumInterval { get; set; } = string.Empty; // ISO 8601 duration format
    public bool Enabled { get; set; }
}

