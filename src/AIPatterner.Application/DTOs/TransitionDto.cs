// DTO for transition responses (qualitative confidence labels, not raw probabilities)
namespace AIPatterner.Application.DTOs;

public class TransitionDto
{
    public Guid Id { get; set; }
    public string FromAction { get; set; } = string.Empty;
    public string ToAction { get; set; } = string.Empty;
    public string ContextBucket { get; set; } = string.Empty;
    public int OccurrenceCount { get; set; }
    public string ConfidenceLabel { get; set; } = string.Empty;
    public double ConfidencePercent { get; set; }
    public TimeSpan? AverageDelay { get; set; }
    public DateTime LastObservedUtc { get; set; }
}

public class TransitionListResponse
{
    public List<TransitionDto> Transitions { get; set; } = new();
}

