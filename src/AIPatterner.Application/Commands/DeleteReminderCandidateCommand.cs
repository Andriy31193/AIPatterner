// MediatR command for deleting reminder candidates
namespace AIPatterner.Application.Commands;

using MediatR;

public class DeleteReminderCandidateCommand : IRequest<bool>
{
    public Guid ReminderCandidateId { get; set; }
}

