// DTO for action event ingestion
namespace AIPatterner.Application.DTOs;

using AIPatterner.Domain.Entities;

public class ActionEventDto
{
    public string PersonId { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public ActionContextDto Context { get; set; } = null!;
    public double? ProbabilityValue { get; set; }
    public ProbabilityAction? ProbabilityAction { get; set; }
    public Dictionary<string, string>? CustomData { get; set; }
}

public class ActionContextDto
{
    public string TimeBucket { get; set; } = string.Empty;
    public string DayType { get; set; } = string.Empty;
    public string? Location { get; set; }
    public List<string>? PresentPeople { get; set; }
    public Dictionary<string, string>? StateSignals { get; set; }
}

