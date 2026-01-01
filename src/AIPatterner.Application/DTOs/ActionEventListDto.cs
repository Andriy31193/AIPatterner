// DTO for action event listing
namespace AIPatterner.Application.DTOs;

using AIPatterner.Domain.Entities;

public class ActionEventListDto
{
    public Guid Id { get; set; }
    public string PersonId { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public ActionContextDto Context { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public double? ProbabilityValue { get; set; }
    public ProbabilityAction? ProbabilityAction { get; set; }
    public Guid? RelatedReminderId { get; set; }
    public Dictionary<string, string>? CustomData { get; set; }
}

public class ActionEventListResponse
{
    public List<ActionEventListDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

