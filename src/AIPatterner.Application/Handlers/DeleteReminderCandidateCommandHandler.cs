// MediatR handler for deleting reminder candidates
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.Commands;
using MediatR;

public class DeleteReminderCandidateCommandHandler : IRequestHandler<DeleteReminderCandidateCommand, bool>
{
    private readonly IReminderCandidateRepository _reminderCandidateRepository;

    public DeleteReminderCandidateCommandHandler(IReminderCandidateRepository reminderCandidateRepository)
    {
        _reminderCandidateRepository = reminderCandidateRepository;
    }

    public async Task<bool> Handle(DeleteReminderCandidateCommand request, CancellationToken cancellationToken)
    {
        var candidate = await _reminderCandidateRepository.GetByIdAsync(request.ReminderCandidateId, cancellationToken);

        if (candidate == null)
            return false;

        await _reminderCandidateRepository.DeleteAsync(candidate, cancellationToken);

        return true;
    }
}

