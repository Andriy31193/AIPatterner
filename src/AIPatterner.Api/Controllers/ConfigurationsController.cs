// API controller for configuration management
namespace AIPatterner.Api.Controllers;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/configurations")]
[Authorize]
public class ConfigurationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ConfigurationsController> _logger;

    public ConfigurationsController(IMediator mediator, ILogger<ConfigurationsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ConfigurationDto>>> GetConfigurations([FromQuery] string? category)
    {
        var query = new GetConfigurationsQuery { Category = category };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConfigurationDto>> CreateConfiguration([FromBody] CreateConfigurationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Category))
        {
            return BadRequest(new { message = "Key and Category are required" });
        }

        var command = new CreateConfigurationCommand
        {
            Key = request.Key,
            Value = request.Value ?? string.Empty,
            Category = request.Category,
            Description = request.Description
        };

        var result = await _mediator.Send(command);
        
        _logger.LogInformation("Configuration created: {Key} in category {Category}", request.Key, request.Category);
        
        return CreatedAtAction(nameof(GetConfigurations), new { key = result.Key, category = result.Category }, result);
    }

    [HttpPut("{category}/{key}")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConfigurationDto>> UpdateConfiguration(
        string category, 
        string key, 
        [FromBody] UpdateConfigurationRequest request)
    {
        var command = new UpdateConfigurationCommand
        {
            Key = key,
            Category = category,
            Value = request.Value ?? string.Empty,
            Description = request.Description
        };

        try
        {
            var result = await _mediator.Send(command);
            _logger.LogInformation("Configuration updated: {Key} in category {Category}", key, category);
            return Ok(result);
        }
        catch (InvalidOperationException)
        {
            return NotFound(new { message = "Configuration not found" });
        }
    }
}

