// DTO for user feedback on reminders
namespace AIPatterner.Application.DTOs;

public class FeedbackDto
{
    public Guid CandidateId { get; set; }
    public string FeedbackType { get; set; } = string.Empty; // "yes", "no", "later"
    public string? Comment { get; set; }
}

