// Domain entity representing a raw action event from external systems
namespace AIPatterner.Domain.Entities;

public class ActionEvent
{
    public Guid Id { get; private set; }
    public string PersonId { get; private set; }
    public string ActionType { get; private set; }
    public DateTime TimestampUtc { get; private set; }
    public ActionContext Context { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private ActionEvent() { } // EF Core

    public ActionEvent(string personId, string actionType, DateTime timestampUtc, ActionContext context)
    {
        if (string.IsNullOrWhiteSpace(personId))
            throw new ArgumentException("PersonId cannot be null or empty", nameof(personId));
        if (string.IsNullOrWhiteSpace(actionType))
            throw new ArgumentException("ActionType cannot be null or empty", nameof(actionType));

        Id = Guid.NewGuid();
        PersonId = personId;
        ActionType = actionType;
        TimestampUtc = timestampUtc;
        Context = context ?? throw new ArgumentNullException(nameof(context));
        CreatedAtUtc = DateTime.UtcNow;
    }
}

