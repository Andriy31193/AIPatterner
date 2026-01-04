// MediatR command for setting execution action on a reminder candidate
namespace AIPatterner.Application.Commands;

using AIPatterner.Application.Services;
using MediatR;

public class SetExecutionActionCommand : IRequest<SetExecutionActionResponse>
{
    public Guid ReminderCandidateId { get; set; }
    public ExecutionAction ExecutionAction { get; set; }
}

public class SetExecutionActionResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

