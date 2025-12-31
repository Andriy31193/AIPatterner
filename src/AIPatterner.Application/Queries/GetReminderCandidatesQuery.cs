// MediatR query for retrieving reminder candidates
namespace AIPatterner.Application.Queries;

using AIPatterner.Application.DTOs;
using MediatR;

public class GetReminderCandidatesQuery : IRequest<ReminderCandidateListResponse>
{
    public string? PersonId { get; set; }
    public string? Status { get; set; }
    public string? ActionType { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

