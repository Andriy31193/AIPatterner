// Value object for strongly-typed action type
namespace AIPatterner.Domain.ValueObjects;

public record ActionType(string Value)
{
    public static ActionType From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ActionType value cannot be null or empty", nameof(value));
        return new ActionType(value);
    }
}

