// API controller for action event ingestion
namespace AIPatterner.Api.Controllers;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/events")]
public class EventsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<EventsController> _logger;

    public EventsController(IMediator mediator, ILogger<EventsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IngestEventResponse>> IngestEvent([FromBody] ActionEventDto eventDto)
    {
        var command = new IngestEventCommand { Event = eventDto };
        var response = await _mediator.Send(command);

        _logger.LogInformation(
            "Ingested event for {PersonId}, action: {ActionType}, scheduled {Count} candidates",
            eventDto.PersonId, eventDto.ActionType, response.ScheduledCandidateIds.Count);

        return Accepted(response);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteEvent(Guid id)
    {
        var command = new DeleteEventCommand { EventId = id };
        var result = await _mediator.Send(command);

        if (!result)
        {
            return NotFound(new { message = "Event not found" });
        }

        _logger.LogInformation("Event deleted: {Id}", id);
        return NoContent();
    }
}

