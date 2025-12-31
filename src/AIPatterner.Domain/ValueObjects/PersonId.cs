// Value object for strongly-typed person identifier
namespace AIPatterner.Domain.ValueObjects;

public record PersonId(string Value)
{
    public static PersonId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("PersonId value cannot be null or empty", nameof(value));
        return new PersonId(value);
    }
}

