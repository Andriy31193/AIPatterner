// MediatR handler for updating reminder occurrence
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.Commands;
using MediatR;

public class UpdateReminderOccurrenceCommandHandler : IRequestHandler<UpdateReminderOccurrenceCommand, bool>
{
    private readonly IReminderCandidateRepository _repository;

    public UpdateReminderOccurrenceCommandHandler(IReminderCandidateRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(UpdateReminderOccurrenceCommand request, CancellationToken cancellationToken)
    {
        var candidate = await _repository.GetByIdAsync(request.ReminderCandidateId, cancellationToken);
        if (candidate == null)
        {
            return false;
        }

        candidate.SetOccurrence(request.Occurrence);
        await _repository.UpdateAsync(candidate, cancellationToken);
        return true;
    }
}


