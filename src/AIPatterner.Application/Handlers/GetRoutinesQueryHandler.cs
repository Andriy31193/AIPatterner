// MediatR handler for querying routines
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.DTOs;
using AIPatterner.Application.Queries;
using MediatR;

public class GetRoutinesQueryHandler : IRequestHandler<GetRoutinesQuery, RoutineListResponse>
{
    private readonly IRoutineRepository _routineRepository;

    public GetRoutinesQueryHandler(
        IRoutineRepository routineRepository)
    {
        _routineRepository = routineRepository;
    }

    public async Task<RoutineListResponse> Handle(GetRoutinesQuery request, CancellationToken cancellationToken)
    {
        var routines = await _routineRepository.GetFilteredAsync(
            request.PersonId,
            request.Page,
            request.PageSize,
            cancellationToken);

        var totalCount = await _routineRepository.GetCountAsync(
            request.PersonId,
            cancellationToken);

        var items = routines.Select(r => new RoutineDto
        {
            Id = r.Id,
            PersonId = r.PersonId,
            IntentType = r.IntentType,
            CreatedAtUtc = r.CreatedAtUtc,
            LastActivatedUtc = r.LastIntentOccurredAtUtc,
            ObservationWindowEndsUtc = r.ObservationWindowEndsAtUtc,
            ObservationWindowMinutes = r.ObservationWindowMinutes,
        }).ToList();

        return new RoutineListResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}

