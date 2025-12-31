// DTO for reminder candidate responses
namespace AIPatterner.Application.DTOs;

using AIPatterner.Domain.Entities;

public class ReminderCandidateDto
{
    public Guid Id { get; set; }
    public string PersonId { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
    public DateTime CheckAtUtc { get; set; }
    public ReminderStyle Style { get; set; }
    public ReminderCandidateStatus Status { get; set; }
    public Guid? TransitionId { get; set; }
    public double Confidence { get; set; }
}

public class ReminderCandidateListResponse
{
    public List<ReminderCandidateDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

