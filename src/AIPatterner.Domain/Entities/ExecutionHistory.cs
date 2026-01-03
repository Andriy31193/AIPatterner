// Domain entity representing execution history of actions and reminders
namespace AIPatterner.Domain.Entities;

public class ExecutionHistory
{
    public Guid Id { get; private set; }
    public string Endpoint { get; private set; }
    public string RequestPayload { get; private set; }
    public string ResponsePayload { get; private set; }
    public DateTime ExecutedAtUtc { get; private set; }
    public string? PersonId { get; private set; }
    public string? UserId { get; private set; }
    public string? ActionType { get; private set; }
    public Guid? ReminderCandidateId { get; private set; }
    public Guid? EventId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private ExecutionHistory() { } // EF Core

    public ExecutionHistory(
        string endpoint,
        string requestPayload,
        string responsePayload,
        DateTime executedAtUtc,
        string? personId = null,
        string? userId = null,
        string? actionType = null,
        Guid? reminderCandidateId = null,
        Guid? eventId = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(requestPayload))
            throw new ArgumentException("RequestPayload cannot be null or empty", nameof(requestPayload));
        if (string.IsNullOrWhiteSpace(responsePayload))
            throw new ArgumentException("ResponsePayload cannot be null or empty", nameof(responsePayload));

        Id = Guid.NewGuid();
        Endpoint = endpoint;
        RequestPayload = requestPayload;
        ResponsePayload = responsePayload;
        ExecutedAtUtc = executedAtUtc;
        PersonId = personId;
        UserId = userId;
        ActionType = actionType;
        ReminderCandidateId = reminderCandidateId;
        EventId = eventId;
        CreatedAtUtc = DateTime.UtcNow;
    }
}


