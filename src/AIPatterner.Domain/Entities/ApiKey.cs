// Domain entity representing an API key
namespace AIPatterner.Domain.Entities;

public class ApiKey
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string KeyHash { get; private set; }
    public string KeyPrefix { get; private set; } // First 8 chars for display
    public string Role { get; private set; }
    public Guid? UserId { get; private set; }
    public string? PersonId { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public DateTime? LastUsedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public bool IsActive { get; private set; }

    private ApiKey() { } // EF Core

    public ApiKey(string name, string keyHash, string keyPrefix, string role, Guid? userId = null, string? personId = null, DateTime? expiresAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));
        if (string.IsNullOrWhiteSpace(keyHash))
            throw new ArgumentException("KeyHash cannot be null or empty", nameof(keyHash));
        if (string.IsNullOrWhiteSpace(keyPrefix))
            throw new ArgumentException("KeyPrefix cannot be null or empty", nameof(keyPrefix));
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be null or empty", nameof(role));

        Id = Guid.NewGuid();
        Name = name;
        KeyHash = keyHash;
        KeyPrefix = keyPrefix;
        Role = role;
        UserId = userId;
        PersonId = personId;
        ExpiresAtUtc = expiresAtUtc;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateLastUsed()
    {
        LastUsedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public bool IsExpired()
    {
        return ExpiresAtUtc.HasValue && ExpiresAtUtc.Value < DateTime.UtcNow;
    }

    public bool IsValid()
    {
        return IsActive && !IsExpired();
    }
}


