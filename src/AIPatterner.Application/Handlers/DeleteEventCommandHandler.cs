// MediatR handler for deleting events
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.Commands;
using MediatR;

public class DeleteEventCommandHandler : IRequestHandler<DeleteEventCommand, bool>
{
    private readonly IEventRepository _eventRepository;

    public DeleteEventCommandHandler(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public async Task<bool> Handle(DeleteEventCommand request, CancellationToken cancellationToken)
    {
        var eventEntity = await _eventRepository.GetByIdAsync(request.EventId, cancellationToken);

        if (eventEntity == null)
            return false;

        await _eventRepository.DeleteAsync(eventEntity, cancellationToken);

        return true;
    }
}

