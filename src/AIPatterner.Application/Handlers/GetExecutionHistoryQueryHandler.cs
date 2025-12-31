// MediatR handler for getting execution history
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.DTOs;
using AIPatterner.Application.Queries;
using AutoMapper;
using MediatR;

public class GetExecutionHistoryQueryHandler : IRequestHandler<GetExecutionHistoryQuery, ExecutionHistoryListResponse>
{
    private readonly IExecutionHistoryRepository _repository;
    private readonly IMapper _mapper;

    public GetExecutionHistoryQueryHandler(IExecutionHistoryRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<ExecutionHistoryListResponse> Handle(GetExecutionHistoryQuery request, CancellationToken cancellationToken)
    {
        var historyEntries = await _repository.GetFilteredAsync(
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

        return new ExecutionHistoryListResponse
        {
            Items = _mapper.Map<List<ExecutionHistoryDto>>(historyEntries),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}

// Interface for execution history repository (to be implemented in Infrastructure)
public interface IExecutionHistoryRepository
{
    Task<List<Domain.Entities.ExecutionHistory>> GetFilteredAsync(
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

    Task<Domain.Entities.ExecutionHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task DeleteAsync(Domain.Entities.ExecutionHistory history, CancellationToken cancellationToken);
}

