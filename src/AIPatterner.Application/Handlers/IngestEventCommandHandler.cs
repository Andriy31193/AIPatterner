// MediatR handler for ingesting action events
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Application.Mappings;
using AIPatterner.Domain.Entities;
using AIPatterner.Domain.Services;
using AutoMapper;
using MediatR;
using Microsoft.Extensions.Configuration;

public class IngestEventCommandHandler : IRequestHandler<IngestEventCommand, IngestEventResponse>
{
    private readonly IEventRepository _eventRepository;
    private readonly ITransitionLearner _transitionLearner;
    private readonly IReminderScheduler _reminderScheduler;
    private readonly IReminderCandidateRepository _reminderRepository;
    private readonly IMapper _mapper;
    private readonly IExecutionHistoryService _executionHistoryService;
    private readonly IConfiguration _configuration;

    public IngestEventCommandHandler(
        IEventRepository eventRepository,
        ITransitionLearner transitionLearner,
        IReminderScheduler reminderScheduler,
        IReminderCandidateRepository reminderRepository,
        IMapper mapper,
        IExecutionHistoryService executionHistoryService,
        IConfiguration configuration)
    {
        _eventRepository = eventRepository;
        _transitionLearner = transitionLearner;
        _reminderScheduler = reminderScheduler;
        _reminderRepository = reminderRepository;
        _mapper = mapper;
        _executionHistoryService = executionHistoryService;
        _configuration = configuration;
    }

    public async Task<IngestEventResponse> Handle(IngestEventCommand request, CancellationToken cancellationToken)
    {
        var actionEvent = _mapper.Map<ActionEvent>(request.Event);
        await _eventRepository.AddAsync(actionEvent, cancellationToken);

        await _transitionLearner.UpdateTransitionsAsync(actionEvent, cancellationToken);

        // Immediate reminder creation/update logic
        Guid? relatedReminderId = null;
        if (request.Event.ProbabilityValue.HasValue && request.Event.ProbabilityAction.HasValue)
        {
            // Check for existing reminder for same person and action type
            var existingReminders = await _reminderRepository.GetByPersonAndActionAsync(
                actionEvent.PersonId,
                actionEvent.ActionType,
                cancellationToken);

            var matchingReminder = existingReminders
                .Where(r => r.Status == ReminderCandidateStatus.Scheduled)
                .OrderByDescending(r => r.CreatedAtUtc)
                .FirstOrDefault();

            if (matchingReminder != null)
            {
                // Update existing reminder probability
                matchingReminder.UpdateConfidence(
                    request.Event.ProbabilityValue.Value,
                    request.Event.ProbabilityAction.Value);
                await _reminderRepository.UpdateAsync(matchingReminder, cancellationToken);
                relatedReminderId = matchingReminder.Id;
                actionEvent.SetRelatedReminder(matchingReminder.Id);
                await _eventRepository.UpdateAsync(actionEvent, cancellationToken);
            }
            else
            {
                // Create new reminder with default confidence
                var defaultConfidence = _configuration.GetValue<double>("Policy:DefaultReminderConfidence", 0.5);
                var defaultOccurrence = _configuration.GetValue<string?>("Policy:DefaultOccurrence", null);
                
                // Calculate next check time (default: 24 hours from now)
                var nextCheckAt = DateTime.UtcNow.AddHours(24);
                
                var newReminder = new ReminderCandidate(
                    actionEvent.PersonId,
                    actionEvent.ActionType,
                    nextCheckAt,
                    ReminderStyle.Suggest,
                    null,
                    defaultConfidence,
                    defaultOccurrence);

                await _reminderRepository.AddAsync(newReminder, cancellationToken);
                relatedReminderId = newReminder.Id;
                actionEvent.SetRelatedReminder(newReminder.Id);
                await _eventRepository.UpdateAsync(actionEvent, cancellationToken);
            }
        }

        // Continue with existing scheduler logic (for transition-based reminders)
        var scheduledCandidates = await _reminderScheduler.ScheduleCandidatesForEventAsync(
            actionEvent, cancellationToken);

        // Record execution history for event ingestion
        var requestJson = System.Text.Json.JsonSerializer.Serialize(request.Event);
        var responseJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            eventId = actionEvent.Id,
            scheduledCandidateIds = scheduledCandidates.Select(c => c.Id).ToList(),
            relatedReminderId = relatedReminderId
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
            ScheduledCandidateIds = scheduledCandidates.Select(c => c.Id).ToList(),
            RelatedReminderId = relatedReminderId
        };
    }
}

// Interface for event repository (to be implemented in Infrastructure)
public interface IEventRepository
{
    Task AddAsync(ActionEvent actionEvent, CancellationToken cancellationToken);
    Task UpdateAsync(ActionEvent actionEvent, CancellationToken cancellationToken);
    Task<ActionEvent?> GetLastEventForPersonAsync(string personId, DateTime beforeUtc, CancellationToken cancellationToken);
    Task<ActionEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task DeleteAsync(ActionEvent actionEvent, CancellationToken cancellationToken);
    Task<List<ActionEvent>> GetFilteredAsync(
        string? personId,
        string? actionType,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<int> GetCountAsync(
        string? personId,
        string? actionType,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken);
}

// Interface for reminder scheduler (to be implemented in Infrastructure)
public interface IReminderScheduler
{
    Task<List<ReminderCandidate>> ScheduleCandidatesForEventAsync(ActionEvent actionEvent, CancellationToken cancellationToken);
}

