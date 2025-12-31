// MediatR handler for querying events
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.DTOs;
using AIPatterner.Application.Queries;
using AIPatterner.Domain.Entities;
using AutoMapper;
using MediatR;

public class GetEventsQueryHandler : IRequestHandler<GetEventsQuery, ActionEventListResponse>
{
    private readonly IEventRepository _repository;
    private readonly IMapper _mapper;

    public GetEventsQueryHandler(IEventRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
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

        return new ActionEventListResponse
        {
            Items = _mapper.Map<List<ActionEventListDto>>(events),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}

