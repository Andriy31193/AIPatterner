// MediatR command for processing a reminder candidate
namespace AIPatterner.Application.Commands;

using MediatR;

public class ProcessReminderCandidateCommand : IRequest<ProcessReminderCandidateResponse>
{
    public Guid CandidateId { get; set; }
    public bool BypassDateCheck { get; set; } = false;
}

public class ProcessReminderCandidateResponse
{
    public bool Executed { get; set; }
    public bool ShouldSpeak { get; set; }
    public string? NaturalLanguagePhrase { get; set; }
    public string Reason { get; set; } = string.Empty;
}

