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
    private readonly IUserContextService _userContextService;

    public GetReminderCandidatesQueryHandler(
        IReminderCandidateRepository repository, 
        IMapper mapper,
        IUserContextService userContextService)
    {
        _repository = repository;
        _mapper = mapper;
        _userContextService = userContextService;
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

        // Apply user isolation: filter by userId if not admin
        var currentUserId = await _userContextService.GetCurrentUserIdAsync();
        var isAdmin = _userContextService.IsAdmin();

        if (!isAdmin && currentUserId.HasValue)
        {
            candidates = candidates.Where(c => c.UserId == currentUserId.Value).ToList();
        }

        // Filter by action type if provided
        var filteredCandidates = candidates;
        if (!string.IsNullOrWhiteSpace(request.ActionType))
        {
            filteredCandidates = candidates.Where(c => c.SuggestedAction.Contains(request.ActionType!, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var totalCount = await _repository.GetCountAsync(
            request.PersonId,
            request.Status,
            request.FromUtc,
            request.ToUtc,
            cancellationToken);

        // Recalculate total count after user filtering
        if (!isAdmin && currentUserId.HasValue)
        {
            totalCount = filteredCandidates.Count;
        }
        else if (!string.IsNullOrWhiteSpace(request.ActionType))
        {
            totalCount = filteredCandidates.Count;
        }

        return new ReminderCandidateListResponse
        {
            Items = _mapper.Map<List<ReminderCandidateDto>>(filteredCandidates),
            TotalCount = totalCount,
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

