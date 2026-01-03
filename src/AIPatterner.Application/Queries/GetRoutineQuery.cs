// MediatR query for retrieving a single routine with reminders
namespace AIPatterner.Application.Queries;

using AIPatterner.Application.DTOs;
using MediatR;

public class GetRoutineQuery : IRequest<RoutineDetailDto>
{
    public Guid RoutineId { get; set; }
}

