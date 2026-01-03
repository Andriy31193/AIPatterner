// Domain entity representing system configuration
namespace AIPatterner.Domain.Entities;

public class Configuration
{
    public Guid Id { get; private set; }
    public string Key { get; private set; }
    public string Value { get; private set; }
    public string Category { get; private set; }
    public string? Description { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Configuration() { } // EF Core

    public Configuration(string key, string value, string category, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category cannot be null or empty", nameof(category));

        Id = Guid.NewGuid();
        Key = key;
        Value = value ?? string.Empty;
        Category = category;
        Description = description;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateValue(string value)
    {
        Value = value ?? string.Empty;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateDescription(string? description)
    {
        Description = description;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}


