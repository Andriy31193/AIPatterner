// MediatR handler for updating routines
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using MediatR;

public class UpdateRoutineCommandHandler : IRequestHandler<UpdateRoutineCommand, RoutineDto>
{
    private readonly IRoutineRepository _routineRepository;

    public UpdateRoutineCommandHandler(IRoutineRepository routineRepository)
    {
        _routineRepository = routineRepository;
    }

    public async Task<RoutineDto> Handle(UpdateRoutineCommand request, CancellationToken cancellationToken)
    {
        var routine = await _routineRepository.GetByIdAsync(request.RoutineId, cancellationToken);

        if (routine == null)
        {
            throw new KeyNotFoundException($"Routine with ID {request.RoutineId} not found");
        }

        // Update only provided fields
        if (request.ObservationWindowMinutes.HasValue)
        {
            routine.UpdateObservationWindowMinutes(request.ObservationWindowMinutes.Value);
        }

        await _routineRepository.UpdateAsync(routine, cancellationToken);

        return new RoutineDto
        {
            Id = routine.Id,
            PersonId = routine.PersonId,
            IntentType = routine.IntentType,
            CreatedAtUtc = routine.CreatedAtUtc,
            LastActivatedUtc = routine.LastIntentOccurredAtUtc,
            ObservationWindowEndsUtc = routine.ObservationWindowEndsAtUtc,
            ObservationWindowMinutes = routine.ObservationWindowMinutes,
        };
    }
}

