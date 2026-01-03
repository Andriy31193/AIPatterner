// Domain entity representing a raw action event from external systems
namespace AIPatterner.Domain.Entities;

public class ActionEvent
{
    public Guid Id { get; private set; }
    public string PersonId { get; private set; }
    public Guid? UserId { get; private set; } // Nullable for backward compatibility and system events
    public string ActionType { get; private set; }
    public DateTime TimestampUtc { get; private set; }
    public ActionContext Context { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; } // Audit: who created this event
    public DateTime? LastModifiedAtUtc { get; private set; } // Audit: when last modified
    public Guid? LastModifiedByUserId { get; private set; } // Audit: who last modified
    public double? ProbabilityValue { get; private set; }
    public ProbabilityAction? ProbabilityAction { get; private set; }
    public Guid? RelatedReminderId { get; private set; }
    public Dictionary<string, string>? CustomData { get; private set; }
    public EventType EventType { get; private set; } = EventType.Action; // Default to Action for backward compatibility

    private ActionEvent() { } // EF Core

    public ActionEvent(
        string personId, 
        string actionType, 
        DateTime timestampUtc, 
        ActionContext context,
        double? probabilityValue = null,
        ProbabilityAction? probabilityAction = null,
        Dictionary<string, string>? customData = null,
        EventType eventType = EventType.Action,
        Guid? userId = null,
        Guid? createdByUserId = null)
    {
        if (string.IsNullOrWhiteSpace(personId))
            throw new ArgumentException("PersonId cannot be null or empty", nameof(personId));
        if (string.IsNullOrWhiteSpace(actionType))
            throw new ArgumentException("ActionType cannot be null or empty", nameof(actionType));

        Id = Guid.NewGuid();
        PersonId = personId;
        UserId = userId;
        ActionType = actionType;
        TimestampUtc = timestampUtc;
        Context = context ?? throw new ArgumentNullException(nameof(context));
        CreatedAtUtc = DateTime.UtcNow;
        CreatedByUserId = createdByUserId;
        ProbabilityValue = probabilityValue;
        ProbabilityAction = probabilityAction;
        CustomData = customData;
        EventType = eventType;
    }

    public void SetRelatedReminder(Guid reminderId)
    {
        RelatedReminderId = reminderId;
    }

    public void SetUserId(Guid userId)
    {
        UserId = userId;
    }

    public void UpdateAuditInfo(Guid? modifiedByUserId)
    {
        LastModifiedAtUtc = DateTime.UtcNow;
        LastModifiedByUserId = modifiedByUserId;
    }
}

