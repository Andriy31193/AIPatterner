// MediatR handler for ingesting action events
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Application.Mappings;
using AIPatterner.Domain.Entities;
using AIPatterner.Domain.Services;
using AutoMapper;
using MediatR;

public class IngestEventCommandHandler : IRequestHandler<IngestEventCommand, IngestEventResponse>
{
    private readonly IEventRepository _eventRepository;
    private readonly ITransitionLearner _transitionLearner;
    private readonly IReminderScheduler _reminderScheduler;
    private readonly IMapper _mapper;
    private readonly IExecutionHistoryService _executionHistoryService;

    public IngestEventCommandHandler(
        IEventRepository eventRepository,
        ITransitionLearner transitionLearner,
        IReminderScheduler reminderScheduler,
        IMapper mapper,
        IExecutionHistoryService executionHistoryService)
    {
        _eventRepository = eventRepository;
        _transitionLearner = transitionLearner;
        _reminderScheduler = reminderScheduler;
        _mapper = mapper;
        _executionHistoryService = executionHistoryService;
    }

    public async Task<IngestEventResponse> Handle(IngestEventCommand request, CancellationToken cancellationToken)
    {
        var actionEvent = _mapper.Map<ActionEvent>(request.Event);
        await _eventRepository.AddAsync(actionEvent, cancellationToken);

        await _transitionLearner.UpdateTransitionsAsync(actionEvent, cancellationToken);

        var scheduledCandidates = await _reminderScheduler.ScheduleCandidatesForEventAsync(
            actionEvent, cancellationToken);

        // Record execution history for event ingestion
        var requestJson = System.Text.Json.JsonSerializer.Serialize(request.Event);
        var responseJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            eventId = actionEvent.Id,
            scheduledCandidateIds = scheduledCandidates.Select(c => c.Id).ToList()
        });

        await _executionHistoryService.RecordExecutionAsync(
            "/api/v1/events",
            requestJson,
            responseJson,
            DateTime.UtcNow,
            actionEvent.PersonId,
            null,
            actionEvent.ActionType,
            null,
            actionEvent.Id,
            cancellationToken);

        return new IngestEventResponse
        {
            EventId = actionEvent.Id,
            ScheduledCandidateIds = scheduledCandidates.Select(c => c.Id).ToList()
        };
    }
}

// Interface for event repository (to be implemented in Infrastructure)
public interface IEventRepository
{
    Task AddAsync(ActionEvent actionEvent, CancellationToken cancellationToken);
    Task<ActionEvent?> GetLastEventForPersonAsync(string personId, DateTime beforeUtc, CancellationToken cancellationToken);
    Task<ActionEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task DeleteAsync(ActionEvent actionEvent, CancellationToken cancellationToken);
}

// Interface for reminder scheduler (to be implemented in Infrastructure)
public interface IReminderScheduler
{
    Task<List<ReminderCandidate>> ScheduleCandidatesForEventAsync(ActionEvent actionEvent, CancellationToken cancellationToken);
}

