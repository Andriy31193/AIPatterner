// API controller for webhook endpoints
namespace AIPatterner.Api.Controllers;

using AIPatterner.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(IMediator mediator, ILogger<WebhooksController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("check/{candidateId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> CheckCandidate(Guid candidateId)
    {
        var command = new ProcessReminderCandidateCommand { CandidateId = candidateId };
        var result = await _mediator.Send(command);
        return Ok(result);
    }
}

