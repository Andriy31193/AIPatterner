// API controller for routines
namespace AIPatterner.Api.Controllers;

using AIPatterner.Api.Extensions;
using AIPatterner.Application.Commands;
using AIPatterner.Application.Queries;
using AIPatterner.Application.Services;
using AIPatterner.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/routines")]
public class RoutinesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IRoutineLearningService _routineLearningService;
    private readonly ILogger<RoutinesController> _logger;

    public RoutinesController(
        IMediator mediator,
        IRoutineLearningService routineLearningService,
        ILogger<RoutinesController> logger)
    {
        _mediator = mediator;
        _routineLearningService = routineLearningService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetRoutines(
        [FromQuery] string? personId,
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

        var query = new GetRoutinesQuery
        {
            PersonId = personId,
            Page = page,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetRoutine(Guid id)
    {
        var query = new GetRoutineQuery { RoutineId = id };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("active")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetActiveRoutines(
        [FromQuery] string personId)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            return BadRequest(new { message = "personId is required" });
        }

        // Validate personId access
        var apiKeyPersonId = HttpContext.GetApiKeyPersonId();
        var isAdmin = HttpContext.IsAdmin();

        // For user role: personId must match their own
        if (!isAdmin && personId != apiKeyPersonId)
        {
            // User trying to access different personId - forbidden
            return StatusCode(403, new { message = "Access denied: personId does not match your API key" });
        }
        // For admin: any personId is allowed

        var query = new GetRoutinesQuery
        {
            PersonId = personId,
            Page = 1,
            PageSize = 100
        };

        var result = await _mediator.Send(query);
        
        // Filter to only active routines (with open observation windows)
        var activeRoutines = result.Items
            .Where(r => r.ObservationWindowEndsUtc.HasValue && 
                       r.ObservationWindowEndsUtc.Value > DateTime.UtcNow)
            .ToList();

        return Ok(new { items = activeRoutines, totalCount = activeRoutines.Count });
    }

    [HttpPost("{routineId}/reminders/{reminderId}/feedback")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SubmitRoutineReminderFeedback(
        Guid routineId,
        Guid reminderId,
        [FromBody] RoutineReminderFeedbackRequest request)
    {
        await _routineLearningService.HandleFeedbackAsync(
            reminderId,
            request.Action,
            request.Value,
            CancellationToken.None);

        _logger.LogInformation(
            "Received feedback {Action} with value {Value} for routine reminder {ReminderId} in routine {RoutineId}",
            request.Action, request.Value, reminderId, routineId);

        return NoContent();
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> UpdateRoutine(Guid id, [FromBody] UpdateRoutineRequest request)
    {
        // Validate access
        var apiKeyPersonId = HttpContext.GetApiKeyPersonId();
        var isAdmin = HttpContext.IsAdmin();

        // First get the routine to check personId
        var getRoutineQuery = new GetRoutineQuery { RoutineId = id };
        var routineResult = await _mediator.Send(getRoutineQuery);

        if (routineResult == null)
        {
            return NotFound(new { message = $"Routine with ID {id} not found" });
        }

        // Check access permissions
        if (!isAdmin && routineResult.PersonId != apiKeyPersonId)
        {
            return StatusCode(403, new { message = "Access denied: personId does not match your API key" });
        }

        var command = new UpdateRoutineCommand
        {
            RoutineId = id,
            ObservationWindowMinutes = request.ObservationWindowMinutes,
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }
}

public class RoutineReminderFeedbackRequest
{
    public ProbabilityAction Action { get; set; }
    public double Value { get; set; }
}

public class UpdateRoutineRequest
{
    public int? ObservationWindowMinutes { get; set; }
}

