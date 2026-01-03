// MediatR handler for querying events
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.DTOs;
using AIPatterner.Application.Queries;
using AIPatterner.Application.Services;
using AIPatterner.Domain.Entities;
using AutoMapper;
using MediatR;

public class GetEventsQueryHandler : IRequestHandler<GetEventsQuery, ActionEventListResponse>
{
    private readonly IEventRepository _repository;
    private readonly IMapper _mapper;
    private readonly IUserContextService _userContextService;

    public GetEventsQueryHandler(IEventRepository repository, IMapper mapper, IUserContextService userContextService)
    {
        _repository = repository;
        _mapper = mapper;
        _userContextService = userContextService;
    }

    public async Task<ActionEventListResponse> Handle(GetEventsQuery request, CancellationToken cancellationToken)
    {
        var events = await _repository.GetFilteredAsync(
            request.PersonId,
            request.ActionType,
            request.FromUtc,
            request.ToUtc,
            request.Page,
            request.PageSize,
            cancellationToken);

        var totalCount = await _repository.GetCountAsync(
            request.PersonId,
            request.ActionType,
            request.FromUtc,
            request.ToUtc,
            cancellationToken);

        // Apply user isolation: filter by userId if not admin
        var currentUserId = await _userContextService.GetCurrentUserIdAsync();
        var isAdmin = _userContextService.IsAdmin();

        if (!isAdmin && currentUserId.HasValue)
        {
            events = events.Where(e => e.UserId == currentUserId.Value).ToList();
            totalCount = events.Count; // Recalculate after filtering
        }

        return new ActionEventListResponse
        {
            Items = _mapper.Map<List<ActionEventListDto>>(events),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}


