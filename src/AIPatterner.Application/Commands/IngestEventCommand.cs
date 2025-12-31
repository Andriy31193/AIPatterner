// MediatR command for ingesting action events
namespace AIPatterner.Application.Commands;

using AIPatterner.Application.DTOs;
using MediatR;

public class IngestEventCommand : IRequest<IngestEventResponse>
{
    public ActionEventDto Event { get; set; } = null!;
}

public class IngestEventResponse
{
    public Guid EventId { get; set; }
    public List<Guid> ScheduledCandidateIds { get; set; } = new();
    public Guid? RelatedReminderId { get; set; }
}

