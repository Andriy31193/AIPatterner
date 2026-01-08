// EF Core repository implementation for RoutineReminder
namespace AIPatterner.Infrastructure.Persistence.Repositories;

using AIPatterner.Application.Handlers;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class RoutineReminderRepository : IRoutineReminderRepository
{
    private readonly ApplicationDbContext _context;

    public RoutineReminderRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RoutineReminder?> GetByRoutineBucketAndActionAsync(Guid routineId, string timeContextBucket, string actionType, CancellationToken cancellationToken)
    {
        return await _context.RoutineReminders
            .FirstOrDefaultAsync(
                r => r.RoutineId == routineId &&
                     r.TimeContextBucket == timeContextBucket &&
                     r.SuggestedAction == actionType,
                cancellationToken);
    }

    public async Task<List<RoutineReminder>> GetByRoutineAndBucketAsync(Guid routineId, string timeContextBucket, CancellationToken cancellationToken)
    {
        return await _context.RoutineReminders
            .Where(r => r.RoutineId == routineId && r.TimeContextBucket == timeContextBucket)
            .OrderByDescending(r => r.Confidence)
            .ThenByDescending(r => r.LastObservedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RoutineReminder>> GetByRoutineAsync(Guid routineId, CancellationToken cancellationToken)
    {
        return await _context.RoutineReminders
            .Where(r => r.RoutineId == routineId)
            .OrderByDescending(r => r.Confidence)
            .ThenByDescending(r => r.LastObservedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<RoutineReminder?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.RoutineReminders.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task AddAsync(RoutineReminder reminder, CancellationToken cancellationToken)
    {
        await _context.RoutineReminders.AddAsync(reminder, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(RoutineReminder reminder, CancellationToken cancellationToken)
    {
        _context.RoutineReminders.Update(reminder);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

