// MediatR command for updating reminder occurrence
namespace AIPatterner.Application.Commands;

using MediatR;

public class UpdateReminderOccurrenceCommand : IRequest<bool>
{
    public Guid ReminderCandidateId { get; set; }
    public string? Occurrence { get; set; }
}

