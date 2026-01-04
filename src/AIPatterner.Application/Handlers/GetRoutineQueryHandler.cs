// MediatR handler for querying a single routine with reminders
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.DTOs;
using AIPatterner.Application.Queries;
using MediatR;

public class GetRoutineQueryHandler : IRequestHandler<GetRoutineQuery, RoutineDetailDto>
{
    private readonly IRoutineRepository _routineRepository;
    private readonly IRoutineReminderRepository _routineReminderRepository;

    public GetRoutineQueryHandler(
        IRoutineRepository routineRepository,
        IRoutineReminderRepository routineReminderRepository)
    {
        _routineRepository = routineRepository;
        _routineReminderRepository = routineReminderRepository;
    }

    public async Task<RoutineDetailDto> Handle(GetRoutineQuery request, CancellationToken cancellationToken)
    {
        var routine = await _routineRepository.GetByIdAsync(request.RoutineId, cancellationToken);

        if (routine == null)
        {
            throw new KeyNotFoundException($"Routine with ID {request.RoutineId} not found");
        }

        var reminders = await _routineReminderRepository.GetByRoutineAsync(request.RoutineId, cancellationToken);

        return new RoutineDetailDto
        {
            Id = routine.Id,
            PersonId = routine.PersonId,
            IntentType = routine.IntentType,
            CreatedAtUtc = routine.CreatedAtUtc,
            LastActivatedUtc = routine.LastIntentOccurredAtUtc,
            ObservationWindowEndsUtc = routine.ObservationWindowEndsAtUtc,
            ObservationWindowMinutes = routine.ObservationWindowMinutes,
            Reminders = reminders
                .OrderByDescending(rr => rr.Confidence)
                .ThenByDescending(rr => rr.LastObservedAtUtc)
                .Select(rr => 
                {
                    var profile = rr.GetSignalProfile();
                    return new RoutineReminderDto
                    {
                        Id = rr.Id,
                        RoutineId = rr.RoutineId,
                        SuggestedAction = rr.SuggestedAction,
                        Confidence = rr.Confidence,
                        CreatedAtUtc = rr.CreatedAtUtc,
                        LastObservedAtUtc = rr.LastObservedAtUtc,
                        CustomData = rr.CustomData,
                        SignalProfile = profile != null ? new SignalProfileDto
                        {
                            Signals = profile.Signals.ToDictionary(
                                kvp => kvp.Key,
                                kvp => new SignalProfileEntryDto
                                {
                                    Weight = kvp.Value.Weight,
                                    NormalizedValue = kvp.Value.NormalizedValue
                                })
                        } : null,
                        SignalProfileUpdatedAtUtc = rr.SignalProfileUpdatedAtUtc,
                        SignalProfileSamplesCount = rr.SignalProfileSamplesCount,
                    };
                }).ToList()
        };
    }
}

