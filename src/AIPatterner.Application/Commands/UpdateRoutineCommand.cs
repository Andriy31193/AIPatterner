// MediatR command for updating routines
namespace AIPatterner.Application.Commands;

using AIPatterner.Application.DTOs;
using MediatR;

public class UpdateRoutineCommand : IRequest<RoutineDto>
{
    public Guid RoutineId { get; set; }
    public int? ObservationWindowMinutes { get; set; }
}

