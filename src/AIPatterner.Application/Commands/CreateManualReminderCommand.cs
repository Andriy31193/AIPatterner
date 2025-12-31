// MediatR command for creating manual reminders
namespace AIPatterner.Application.Commands;

using AIPatterner.Application.DTOs;
using MediatR;

public class CreateManualReminderCommand : IRequest<Guid>
{
    public string PersonId { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
    public DateTime CheckAtUtc { get; set; }
    public string Style { get; set; } = "Suggest";
    public string? Occurrence { get; set; }
}

