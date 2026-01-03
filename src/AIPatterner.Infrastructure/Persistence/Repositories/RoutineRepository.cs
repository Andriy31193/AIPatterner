// EF Core repository implementation for Routine
namespace AIPatterner.Infrastructure.Persistence.Repositories;

using AIPatterner.Application.Handlers;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class RoutineRepository : IRoutineRepository
{
    private readonly ApplicationDbContext _context;

    public RoutineRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Routine?> GetByPersonAndIntentAsync(string personId, string intentType, CancellationToken cancellationToken)
    {
        return await _context.Routines
            .FirstOrDefaultAsync(r => r.PersonId == personId && r.IntentType == intentType, cancellationToken);
    }

    public async Task<Routine?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Routines.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<List<Routine>> GetByPersonAsync(string personId, CancellationToken cancellationToken)
    {
        return await _context.Routines
            .Where(r => r.PersonId == personId)
            .OrderByDescending(r => r.LastIntentOccurredAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Routine routine, CancellationToken cancellationToken)
    {
        await _context.Routines.AddAsync(routine, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Routine routine, CancellationToken cancellationToken)
    {
        _context.Routines.Update(routine);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Routine>> GetFilteredAsync(string? personId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _context.Routines.AsQueryable();

        if (!string.IsNullOrWhiteSpace(personId))
        {
            query = query.Where(r => r.PersonId == personId);
        }

        return await query
            .OrderByDescending(r => r.LastIntentOccurredAtUtc ?? r.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(string? personId, CancellationToken cancellationToken)
    {
        var query = _context.Routines.AsQueryable();

        if (!string.IsNullOrWhiteSpace(personId))
        {
            query = query.Where(r => r.PersonId == personId);
        }

        return await query.CountAsync(cancellationToken);
    }

    public async Task<List<Routine>> GetActiveRoutinesAsync(string personId, DateTime currentTime, CancellationToken cancellationToken)
    {
        return await _context.Routines
            .Where(r => r.PersonId == personId)
            .Where(r => r.ObservationWindowEndsAtUtc.HasValue && r.ObservationWindowEndsAtUtc.Value > currentTime)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateMultipleAsync(List<Routine> routines, CancellationToken cancellationToken)
    {
        _context.Routines.UpdateRange(routines);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

