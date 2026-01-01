// API controller for user reminder preferences
namespace AIPatterner.Api.Controllers;

using AIPatterner.Application.Commands;
using AIPatterner.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Xml;

[ApiController]
[Route("api/v1/user-preferences")]
public class UserPreferencesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<UserPreferencesController> _logger;

    public UserPreferencesController(IMediator mediator, ILogger<UserPreferencesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet("{personId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetUserPreferences(string personId)
    {
        var query = new GetUserPreferencesQuery { PersonId = personId };
        var result = await _mediator.Send(query);
        
        if (result == null)
        {
            return NotFound(new { message = "User preferences not found" });
        }

        return Ok(result);
    }

    [HttpPut("{personId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateUserPreferences(string personId, [FromBody] UpdateUserPreferencesRequest request)
    {
        TimeSpan? minimumInterval = null;
        if (!string.IsNullOrWhiteSpace(request.MinimumInterval))
        {
            try
            {
                minimumInterval = System.Xml.XmlConvert.ToTimeSpan(request.MinimumInterval);
            }
            catch
            {
                return BadRequest(new { message = "Invalid MinimumInterval format. Use ISO 8601 duration format (e.g., PT15M)" });
            }
        }

        Domain.Entities.ReminderStyle? defaultStyle = null;
        if (!string.IsNullOrWhiteSpace(request.DefaultStyle))
        {
            if (Enum.TryParse<Domain.Entities.ReminderStyle>(request.DefaultStyle, true, out var parsedStyle))
            {
                defaultStyle = parsedStyle;
            }
            else
            {
                return BadRequest(new { message = $"Invalid DefaultStyle. Must be one of: {string.Join(", ", Enum.GetNames<Domain.Entities.ReminderStyle>())}" });
            }
        }

        var command = new UpdateUserPreferencesCommand
        {
            PersonId = personId,
            DefaultStyle = defaultStyle,
            DailyLimit = request.DailyLimit,
            MinimumInterval = minimumInterval,
            Enabled = request.Enabled
        };

        var result = await _mediator.Send(command);
        
        if (!result)
        {
            return BadRequest(new { message = "Failed to update user preferences" });
        }

        _logger.LogInformation("User preferences updated for PersonId: {PersonId}", personId);
        return Ok(new { message = "Preferences updated successfully" });
    }
}

public class UpdateUserPreferencesRequest
{
    public string? DefaultStyle { get; set; }
    public int? DailyLimit { get; set; }
    public string? MinimumInterval { get; set; } // ISO 8601 duration format (e.g., "PT15M" for 15 minutes)
    public bool? Enabled { get; set; }
}

