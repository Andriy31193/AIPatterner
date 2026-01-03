// API controller for admin operations
namespace AIPatterner.Api.Controllers;

using AIPatterner.Api.Extensions;
using AIPatterner.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/admin")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IMediator mediator, ILogger<AdminController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("force-check/{candidateId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ForceCheck(Guid candidateId, [FromQuery] bool bypassDateCheck = false)
    {
        var command = new ProcessReminderCandidateCommand 
        { 
            CandidateId = candidateId,
            BypassDateCheck = bypassDateCheck
        };
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpPost("reminders")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> CreateManualReminder([FromBody] CreateManualReminderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PersonId) || string.IsNullOrWhiteSpace(request.SuggestedAction))
        {
            return BadRequest(new { message = "PersonId and SuggestedAction are required" });
        }

        // Validate personId access
        var apiKeyPersonId = HttpContext.GetApiKeyPersonId();
        var isAdmin = HttpContext.IsAdmin();

        // For user role: can only create for their own personId
        if (!isAdmin && request.PersonId != apiKeyPersonId)
        {
            return StatusCode(403, new { message = "Access denied: personId does not match your API key" });
        }
        // For admin: any personId is allowed

        var command = new CreateManualReminderCommand
        {
            PersonId = request.PersonId,
            SuggestedAction = request.SuggestedAction,
            CheckAtUtc = request.CheckAtUtc,
            Style = request.Style ?? "Suggest",
            Occurrence = request.Occurrence
        };

        var reminderId = await _mediator.Send(command);
        
        _logger.LogInformation("Manual reminder created: {ReminderId} for {PersonId}", reminderId, request.PersonId);
        
        return CreatedAtAction(nameof(ForceCheck), new { candidateId = reminderId }, new { id = reminderId });
    }
}

public class CreateManualReminderRequest
{
    public string PersonId { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
    public DateTime CheckAtUtc { get; set; }
    public string? Style { get; set; }
    public string? Occurrence { get; set; }
}

