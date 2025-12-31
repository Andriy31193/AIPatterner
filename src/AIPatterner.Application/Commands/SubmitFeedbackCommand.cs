// MediatR command for submitting user feedback
namespace AIPatterner.Application.Commands;

using AIPatterner.Application.DTOs;
using MediatR;

public class SubmitFeedbackCommand : IRequest<Unit>
{
    public FeedbackDto Feedback { get; set; } = null!;
}

