// MediatR query for getting execution history
namespace AIPatterner.Application.Queries;

using AIPatterner.Application.DTOs;
using MediatR;

public class GetExecutionHistoryQuery : IRequest<ExecutionHistoryListResponse>
{
    public string? PersonId { get; set; }
    public string? ActionType { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}


