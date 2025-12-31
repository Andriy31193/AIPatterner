// Domain entity representing a cooldown period for reminder suggestions
namespace AIPatterner.Domain.Entities;

public class ReminderCooldown
{
    public Guid Id { get; private set; }
    public string PersonId { get; private set; }
    public string ActionType { get; private set; }
    public DateTime SuppressedUntilUtc { get; private set; }
    public string? Reason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private ReminderCooldown() { } // EF Core

    public ReminderCooldown(
        string personId,
        string actionType,
        DateTime suppressedUntilUtc,
        string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(personId))
            throw new ArgumentException("PersonId cannot be null or empty", nameof(personId));
        if (string.IsNullOrWhiteSpace(actionType))
            throw new ArgumentException("ActionType cannot be null or empty", nameof(actionType));

        Id = Guid.NewGuid();
        PersonId = personId;
        ActionType = actionType;
        SuppressedUntilUtc = suppressedUntilUtc;
        Reason = reason;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public bool IsActive(DateTime now)
    {
        return SuppressedUntilUtc > now;
    }
}

