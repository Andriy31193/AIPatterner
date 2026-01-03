// MediatR handler for querying routines
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.DTOs;
using AIPatterner.Application.Queries;
using MediatR;
using Microsoft.Extensions.Configuration;

public class GetRoutinesQueryHandler : IRequestHandler<GetRoutinesQuery, RoutineListResponse>
{
    private readonly IRoutineRepository _routineRepository;
    private readonly IConfiguration _configuration;

    public GetRoutinesQueryHandler(
        IRoutineRepository routineRepository,
        IConfiguration configuration)
    {
        _routineRepository = routineRepository;
        _configuration = configuration;
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

        // Get observation window minutes from configuration or use default
        var observationWindowMinutes = _configuration.GetValue<int>("Routine:ObservationWindowMinutes", 45);

        var items = routines.Select(r => new RoutineDto
        {
            Id = r.Id,
            PersonId = r.PersonId,
            IntentType = r.IntentType,
            CreatedAtUtc = r.CreatedAtUtc,
            LastActivatedUtc = r.LastIntentOccurredAtUtc,
            ObservationWindowEndsUtc = r.ObservationWindowEndsAtUtc,
            ObservationWindowMinutes = observationWindowMinutes,
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

