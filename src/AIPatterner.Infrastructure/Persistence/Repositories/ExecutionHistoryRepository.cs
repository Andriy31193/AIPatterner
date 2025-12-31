// EF Core repository implementation for ExecutionHistory
namespace AIPatterner.Infrastructure.Persistence.Repositories;

using AIPatterner.Application.Handlers;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class ExecutionHistoryRepository : IExecutionHistoryRepository
{
    private readonly ApplicationDbContext _context;

    public ExecutionHistoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ExecutionHistory>> GetFilteredAsync(
        string? personId,
        string? actionType,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _context.ExecutionHistories.AsQueryable();

        if (!string.IsNullOrWhiteSpace(personId))
            query = query.Where(h => h.PersonId == personId);

        if (!string.IsNullOrWhiteSpace(actionType))
            query = query.Where(h => h.ActionType == actionType);

        if (fromUtc.HasValue)
            query = query.Where(h => h.ExecutedAtUtc >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(h => h.ExecutedAtUtc <= toUtc.Value);

        return await query
            .OrderByDescending(h => h.ExecutedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(
        string? personId,
        string? actionType,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var query = _context.ExecutionHistories.AsQueryable();

        if (!string.IsNullOrWhiteSpace(personId))
            query = query.Where(h => h.PersonId == personId);

        if (!string.IsNullOrWhiteSpace(actionType))
            query = query.Where(h => h.ActionType == actionType);

        if (fromUtc.HasValue)
            query = query.Where(h => h.ExecutedAtUtc >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(h => h.ExecutedAtUtc <= toUtc.Value);

        return await query.CountAsync(cancellationToken);
    }

    public async Task<ExecutionHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.ExecutionHistories
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
    }

    public async Task DeleteAsync(ExecutionHistory history, CancellationToken cancellationToken)
    {
        _context.ExecutionHistories.Remove(history);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

