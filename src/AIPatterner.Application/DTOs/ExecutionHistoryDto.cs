// DTOs for execution history
namespace AIPatterner.Application.DTOs;

public class ExecutionHistoryDto
{
    public Guid Id { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string RequestPayload { get; set; } = string.Empty;
    public string ResponsePayload { get; set; } = string.Empty;
    public DateTime ExecutedAtUtc { get; set; }
    public string? PersonId { get; set; }
    public string? UserId { get; set; }
    public string? ActionType { get; set; }
    public Guid? ReminderCandidateId { get; set; }
    public Guid? EventId { get; set; }
}

public class ExecutionHistoryListResponse
{
    public List<ExecutionHistoryDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}


