// MediatR command for deleting events
namespace AIPatterner.Application.Commands;

using MediatR;

public class DeleteEventCommand : IRequest<bool>
{
    public Guid EventId { get; set; }
}


