// MediatR handler for querying reminder candidates
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.DTOs;
using AIPatterner.Application.Mappings;
using AIPatterner.Application.Queries;
using AIPatterner.Application.Services;
using AIPatterner.Domain.Entities;
using AutoMapper;
using MediatR;
using System.Linq;

public class GetReminderCandidatesQueryHandler : IRequestHandler<GetReminderCandidatesQuery, ReminderCandidateListResponse>
{
    private readonly IReminderCandidateRepository _repository;
    private readonly IMapper _mapper;
    private readonly IRoutineLearningService _routineLearningService;
    private readonly IEventRepository _eventRepository;

    public GetReminderCandidatesQueryHandler(
        IReminderCandidateRepository repository, 
        IMapper mapper,
        IRoutineLearningService routineLearningService,
        IEventRepository eventRepository)
    {
        _repository = repository;
        _mapper = mapper;
        _routineLearningService = routineLearningService;
        _eventRepository = eventRepository;
    }

    public async Task<ReminderCandidateListResponse> Handle(GetReminderCandidatesQuery request, CancellationToken cancellationToken)
    {
        var candidates = await _repository.GetFilteredAsync(
            request.PersonId,
            request.Status,
            request.FromUtc,
            request.ToUtc,
            request.Page,
            request.PageSize,
            cancellationToken);

        // Filter by action type if provided
        var filteredCandidates = candidates;
        if (!string.IsNullOrWhiteSpace(request.ActionType))
        {
            filteredCandidates = candidates.Where(c => c.SuggestedAction.Contains(request.ActionType!, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // CRITICAL: Filter out routine reminders - they should NEVER appear in general reminder lists
        // Routine reminders are identified by CustomData["source"] == "routine"
        // They should ONLY be visible in routine detail views, not in general reminder lists
        var filteredList = new List<ReminderCandidate>();
        foreach (var candidate in filteredCandidates)
        {
            bool shouldExclude = false;

            // FIRST: Check if this is a routine reminder (marked with source="routine" in CustomData)
            if (candidate.CustomData != null && 
                candidate.CustomData.TryGetValue("source", out var source) && 
                string.Equals(source, "routine", StringComparison.OrdinalIgnoreCase))
            {
                // This is a routine reminder - exclude it from general reminder lists
                shouldExclude = true;
            }
            else if (!string.IsNullOrWhiteSpace(request.PersonId))
            {
                // SECOND: Check if reminder was created during a routine learning window
                // This catches general reminders that were incorrectly created during learning windows
                if (candidate.SourceEventId.HasValue)
                {
                    var sourceEvent = await _eventRepository.GetByIdAsync(candidate.SourceEventId.Value, cancellationToken);
                    if (sourceEvent != null)
                    {
                        // Check if the source event was within a routine learning window at that time
                        shouldExclude = await _routineLearningService.IsEventWithinRoutineLearningWindowAsync(
                            request.PersonId,
                            sourceEvent.TimestampUtc,
                            cancellationToken);
                    }
                }
                else
                {
                    // If no SourceEventId, check if reminder was created during a learning window
                    shouldExclude = await _routineLearningService.IsEventWithinRoutineLearningWindowAsync(
                        request.PersonId,
                        candidate.CreatedAtUtc,
                        cancellationToken);
                }
            }

            if (!shouldExclude)
            {
                filteredList.Add(candidate);
            }
        }
        filteredCandidates = filteredList;

        var totalCount = await _repository.GetCountAsync(
            request.PersonId,
            request.Status,
            request.FromUtc,
            request.ToUtc,
            cancellationToken);

        return new ReminderCandidateListResponse
        {
            Items = _mapper.Map<List<ReminderCandidateDto>>(filteredCandidates),
            TotalCount = !string.IsNullOrWhiteSpace(request.ActionType) ? filteredCandidates.Count : totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}

// Interface for reminder candidate repository (to be implemented in Infrastructure)
public interface IReminderCandidateRepository
{
    Task<List<ReminderCandidate>> GetFilteredAsync(
        string? personId,
        string? status,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<int> GetCountAsync(
        string? personId,
        string? status,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken);

    Task<ReminderCandidate?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<List<ReminderCandidate>> GetDueCandidatesAsync(DateTime now, int limit, CancellationToken cancellationToken);
    Task<List<ReminderCandidate>> GetByPersonAndActionAsync(string personId, string actionType, CancellationToken cancellationToken);
    Task<List<ReminderCandidate>> GetBySourceEventIdAsync(Guid eventId, CancellationToken cancellationToken);
    Task AddAsync(ReminderCandidate candidate, CancellationToken cancellationToken);
    Task UpdateAsync(ReminderCandidate candidate, CancellationToken cancellationToken);
    Task DeleteAsync(ReminderCandidate candidate, CancellationToken cancellationToken);
}

