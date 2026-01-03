// API controller for execution history
namespace AIPatterner.Api.Controllers;

using AIPatterner.Application.Commands;
using AIPatterner.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/execution-history")]
[Authorize]
public class ExecutionHistoryController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ExecutionHistoryController> _logger;

    public ExecutionHistoryController(IMediator mediator, ILogger<ExecutionHistoryController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetExecutionHistory(
        [FromQuery] string? personId,
        [FromQuery] string? actionType,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetExecutionHistoryQuery
        {
            PersonId = personId,
            ActionType = actionType,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Page = page,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteExecutionHistory(Guid id)
    {
        var command = new DeleteExecutionHistoryCommand { HistoryId = id };
        var result = await _mediator.Send(command);

        if (!result)
        {
            return NotFound(new { message = "Execution history not found" });
        }

        _logger.LogInformation("Execution history deleted: {Id}", id);
        return NoContent();
    }
}


