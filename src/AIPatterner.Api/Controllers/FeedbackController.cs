// API controller for user feedback
namespace AIPatterner.Api.Controllers;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/feedback")]
public class FeedbackController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<FeedbackController> _logger;

    public FeedbackController(IMediator mediator, ILogger<FeedbackController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SubmitFeedback([FromBody] FeedbackDto feedback)
    {
        var command = new SubmitFeedbackCommand { Feedback = feedback };
        await _mediator.Send(command);

        _logger.LogInformation(
            "Received feedback {FeedbackType} for candidate {CandidateId}",
            feedback.FeedbackType, feedback.CandidateId);

        return NoContent();
    }
}

