// Interface for execution history service
namespace AIPatterner.Application.Handlers;

using AIPatterner.Domain.Entities;

public interface IExecutionHistoryService
{
    Task RecordExecutionAsync(
        string endpoint,
        string requestPayload,
        string responsePayload,
        DateTime executedAtUtc,
        string? personId = null,
        string? userId = null,
        string? actionType = null,
        Guid? reminderCandidateId = null,
        Guid? eventId = null,
        CancellationToken cancellationToken = default);
}

