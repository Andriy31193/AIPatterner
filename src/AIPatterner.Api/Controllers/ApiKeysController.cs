// API controller for API key management
namespace AIPatterner.Api.Controllers;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/api-keys")]
[Authorize(Roles = "admin")]
public class ApiKeysController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ApiKeysController> _logger;

    public ApiKeysController(IMediator mediator, ILogger<ApiKeysController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ApiKeyDto>>> GetApiKeys([FromQuery] Guid? userId)
    {
        var query = new GetApiKeysQuery { UserId = userId };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateApiKeyResponse>> CreateApiKey([FromBody] CreateApiKeyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Name is required" });
        }

        var command = new CreateApiKeyCommand
        {
            Name = request.Name,
            Role = request.Role ?? "user",
            ExpiresAtUtc = request.ExpiresAtUtc
        };

        var result = await _mediator.Send(command);
        
        _logger.LogInformation("API key created: {Name}, Prefix: {Prefix}", request.Name, result.ApiKey.KeyPrefix);
        
        return CreatedAtAction(nameof(GetApiKeys), new { id = result.ApiKey.Id }, result);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteApiKey(Guid id)
    {
        var command = new DeleteApiKeyCommand { ApiKeyId = id };
        var result = await _mediator.Send(command);

        if (!result)
        {
            return NotFound(new { message = "API key not found" });
        }

        _logger.LogInformation("API key deleted: {Id}", id);
        return NoContent();
    }
}

