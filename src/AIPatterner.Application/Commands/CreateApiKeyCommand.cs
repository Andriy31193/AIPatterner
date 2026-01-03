// MediatR command for creating API keys
namespace AIPatterner.Application.Commands;

using AIPatterner.Application.DTOs;
using MediatR;

public class CreateApiKeyCommand : IRequest<CreateApiKeyResponse>
{
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public Guid? UserId { get; set; }
    public string? PersonId { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}


