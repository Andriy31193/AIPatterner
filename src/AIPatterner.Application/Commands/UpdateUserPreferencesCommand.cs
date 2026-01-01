// MediatR command for updating user reminder preferences
namespace AIPatterner.Application.Commands;

using AIPatterner.Domain.Entities;
using MediatR;

public class UpdateUserPreferencesCommand : IRequest<bool>
{
    public string PersonId { get; set; } = string.Empty;
    public ReminderStyle? DefaultStyle { get; set; }
    public int? DailyLimit { get; set; }
    public TimeSpan? MinimumInterval { get; set; }
    public bool? Enabled { get; set; }
}

