// API controller for action event ingestion
namespace AIPatterner.Api.Controllers;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Application.Queries;
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

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ActionEventListResponse>> GetEvents(
        [FromQuery] string? personId,
        [FromQuery] string? actionType,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetEventsQuery
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

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IngestEventResponse>> IngestEvent([FromBody] ActionEventDto eventDto)
    {
        var command = new IngestEventCommand { Event = eventDto };
        var response = await _mediator.Send(command);

        _logger.LogInformation(
            "Ingested event for {PersonId}, action: {ActionType}, scheduled {Count} candidates, related reminder: {ReminderId}",
            eventDto.PersonId, eventDto.ActionType, response.ScheduledCandidateIds.Count, response.RelatedReminderId);

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

    [HttpGet("{id}/matching-reminders")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetMatchingReminders(
        Guid id,
        [FromQuery] bool matchByActionType = true,
        [FromQuery] bool matchByDayType = true,
        [FromQuery] bool matchByPeoplePresent = true,
        [FromQuery] bool matchByStateSignals = true,
        [FromQuery] bool matchByTimeBucket = false,
        [FromQuery] bool matchByLocation = false,
        [FromQuery] int timeOffsetMinutes = 30)
    {
        var query = new GetMatchingRemindersQuery
        {
            EventId = id,
            Criteria = new MatchingCriteria
            {
                MatchByActionType = matchByActionType,
                MatchByDayType = matchByDayType,
                MatchByPeoplePresent = matchByPeoplePresent,
                MatchByStateSignals = matchByStateSignals,
                MatchByTimeBucket = matchByTimeBucket,
                MatchByLocation = matchByLocation,
                TimeOffsetMinutes = timeOffsetMinutes
            }
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("{id}/related-reminders")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReminderCandidateListResponse>> GetRelatedReminders(Guid id)
    {
        var query = new GetRemindersByEventIdQuery { EventId = id };
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}

