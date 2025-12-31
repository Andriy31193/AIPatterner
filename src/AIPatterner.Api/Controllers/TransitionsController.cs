// API controller for transitions
namespace AIPatterner.Api.Controllers;

using AIPatterner.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/transitions")]
public class TransitionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<TransitionsController> _logger;

    public TransitionsController(IMediator mediator, ILogger<TransitionsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet("{personId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetTransitions(string personId)
    {
        var query = new GetTransitionsQuery { PersonId = personId };
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}

