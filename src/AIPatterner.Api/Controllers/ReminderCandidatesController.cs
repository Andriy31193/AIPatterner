// API controller for reminder candidates
namespace AIPatterner.Api.Controllers;

using AIPatterner.Api.Extensions;
using AIPatterner.Application.Commands;
using AIPatterner.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/reminder-candidates")]
public class ReminderCandidatesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ReminderCandidatesController> _logger;

    public ReminderCandidatesController(IMediator mediator, ILogger<ReminderCandidatesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetReminderCandidates(
        [FromQuery] string? personId,
        [FromQuery] string? status,
        [FromQuery] string? actionType,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // Validate personId access
        var apiKeyPersonId = HttpContext.GetApiKeyPersonId();
        var isAdmin = HttpContext.IsAdmin();

        // For user role: personId is required and must match their own
        if (!isAdmin)
        {
            if (string.IsNullOrWhiteSpace(personId))
            {
                // If no personId provided, use the API key's personId
                personId = apiKeyPersonId;
            }
            else if (personId != apiKeyPersonId)
            {
                // User trying to access different personId - forbidden
                return StatusCode(403, new { message = "Access denied: personId does not match your API key" });
            }
        }
        // For admin: any personId is allowed

        var query = new GetReminderCandidatesQuery
        {
            PersonId = personId,
            Status = status,
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
    [Authorize(Roles = "admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteReminderCandidate(Guid id)
    {
        var command = new DeleteReminderCandidateCommand { ReminderCandidateId = id };
        var result = await _mediator.Send(command);

        if (!result)
        {
            return NotFound(new { message = "Reminder candidate not found" });
        }

        _logger.LogInformation("Reminder candidate deleted: {Id}", id);
        return NoContent();
    }

    [HttpPut("{id}/occurrence")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateReminderOccurrence(Guid id, [FromBody] UpdateReminderOccurrenceRequest request)
    {
        var command = new UpdateReminderOccurrenceCommand
        {
            ReminderCandidateId = id,
            Occurrence = request.Occurrence
        };
        var result = await _mediator.Send(command);

        if (!result)
        {
            return NotFound(new { message = "Reminder candidate not found" });
        }

        _logger.LogInformation("Reminder candidate occurrence updated: {Id}", id);
        return Ok(new { message = "Occurrence updated successfully" });
    }
}

public class UpdateReminderOccurrenceRequest
{
    public string? Occurrence { get; set; }
}

