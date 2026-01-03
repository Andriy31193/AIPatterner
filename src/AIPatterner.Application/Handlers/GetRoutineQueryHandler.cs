// MediatR handler for querying a single routine with reminders
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.DTOs;
using AIPatterner.Application.Queries;
using AIPatterner.Application.Services;
using MediatR;
using Microsoft.Extensions.Configuration;

public class GetRoutineQueryHandler : IRequestHandler<GetRoutineQuery, RoutineDetailDto>
{
    private readonly IRoutineRepository _routineRepository;
    private readonly IRoutineReminderRepository _routineReminderRepository;
    private readonly IConfiguration _configuration;
    private readonly IUserContextService _userContextService;

    public GetRoutineQueryHandler(
        IRoutineRepository routineRepository,
        IRoutineReminderRepository routineReminderRepository,
        IConfiguration configuration,
        IUserContextService userContextService)
    {
        _routineRepository = routineRepository;
        _routineReminderRepository = routineReminderRepository;
        _configuration = configuration;
        _userContextService = userContextService;
    }

    public async Task<RoutineDetailDto> Handle(GetRoutineQuery request, CancellationToken cancellationToken)
    {
        var routine = await _routineRepository.GetByIdAsync(request.RoutineId, cancellationToken);

        if (routine == null)
        {
            throw new KeyNotFoundException($"Routine with ID {request.RoutineId} not found");
        }

        // Apply user isolation: check if user has access to this routine
        var currentUserId = await _userContextService.GetCurrentUserIdAsync();
        var isAdmin = _userContextService.IsAdmin();

        if (!isAdmin && currentUserId.HasValue && routine.UserId != currentUserId.Value)
        {
            throw new UnauthorizedAccessException("You do not have access to this routine");
        }

        var reminders = await _routineReminderRepository.GetByRoutineAsync(request.RoutineId, cancellationToken);

        // Get observation window minutes from configuration or use default
        var observationWindowMinutes = _configuration.GetValue<int>("Routine:ObservationWindowMinutes", 45);

        return new RoutineDetailDto
        {
            Id = routine.Id,
            PersonId = routine.PersonId,
            IntentType = routine.IntentType,
            CreatedAtUtc = routine.CreatedAtUtc,
            LastActivatedUtc = routine.LastIntentOccurredAtUtc,
            ObservationWindowEndsUtc = routine.ObservationWindowEndsAtUtc,
            ObservationWindowMinutes = observationWindowMinutes,
            Reminders = reminders
                .OrderByDescending(rr => rr.Confidence)
                .ThenByDescending(rr => rr.LastObservedAtUtc)
                .Select(rr => new RoutineReminderDto
                {
                    Id = rr.Id,
                    RoutineId = rr.RoutineId,
                    SuggestedAction = rr.SuggestedAction,
                    Confidence = rr.Confidence,
                    CreatedAtUtc = rr.CreatedAtUtc,
                    LastObservedAtUtc = rr.LastObservedAtUtc,
                    CustomData = rr.CustomData,
                }).ToList()
        };
    }
}

