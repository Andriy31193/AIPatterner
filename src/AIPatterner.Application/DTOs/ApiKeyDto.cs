// DTOs for API key management
namespace AIPatterner.Application.DTOs;

public class ApiKeyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool IsActive { get; set; }
}

public class CreateApiKeyRequest
{
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public DateTime? ExpiresAtUtc { get; set; }
}

public class CreateApiKeyResponse
{
    public ApiKeyDto ApiKey { get; set; } = null!;
    public string FullKey { get; set; } = string.Empty; // Only shown once on creation
}

