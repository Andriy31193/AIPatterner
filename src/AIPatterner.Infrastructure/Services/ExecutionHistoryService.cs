// Service implementation for recording execution history
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Application.Handlers;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public class ExecutionHistoryService : IExecutionHistoryService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ExecutionHistoryService> _logger;

    public ExecutionHistoryService(
        ApplicationDbContext context,
        ILogger<ExecutionHistoryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RecordExecutionAsync(
        string endpoint,
        string requestPayload,
        string responsePayload,
        DateTime executedAtUtc,
        string? personId = null,
        string? userId = null,
        string? actionType = null,
        Guid? reminderCandidateId = null,
        Guid? eventId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var history = new ExecutionHistory(
                endpoint,
                requestPayload,
                responsePayload,
                executedAtUtc,
                personId,
                userId,
                actionType,
                reminderCandidateId,
                eventId);

            _context.ExecutionHistories.Add(history);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Recorded execution history: {Endpoint} at {ExecutedAt}", endpoint, executedAtUtc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record execution history for endpoint {Endpoint}", endpoint);
            // Don't throw - execution history is not critical
        }
    }
}

