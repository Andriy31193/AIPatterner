// EF Core repository implementation for ReminderCandidate
namespace AIPatterner.Infrastructure.Persistence.Repositories;

using AIPatterner.Application.Handlers;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class ReminderCandidateRepository : IReminderCandidateRepository
{
    private readonly ApplicationDbContext _context;

    public ReminderCandidateRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ReminderCandidate>> GetFilteredAsync(
        string? personId,
        string? status,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _context.ReminderCandidates.AsQueryable();

        if (!string.IsNullOrEmpty(personId))
            query = query.Where(c => c.PersonId == personId);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ReminderCandidateStatus>(status, out var statusEnum))
            query = query.Where(c => c.Status == statusEnum);

        if (fromUtc.HasValue)
            query = query.Where(c => c.CheckAtUtc >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(c => c.CheckAtUtc <= toUtc.Value);

        return await query
            .OrderByDescending(c => c.Confidence)
            .ThenBy(c => c.CheckAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(
        string? personId,
        string? status,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var query = _context.ReminderCandidates.AsQueryable();

        if (!string.IsNullOrEmpty(personId))
            query = query.Where(c => c.PersonId == personId);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ReminderCandidateStatus>(status, out var statusEnum))
            query = query.Where(c => c.Status == statusEnum);

        if (fromUtc.HasValue)
            query = query.Where(c => c.CheckAtUtc >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(c => c.CheckAtUtc <= toUtc.Value);

        return await query.CountAsync(cancellationToken);
    }

    public async Task<ReminderCandidate?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.ReminderCandidates.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<List<ReminderCandidate>> GetDueCandidatesAsync(DateTime now, int limit, CancellationToken cancellationToken)
    {
        return await _context.ReminderCandidates
            .Where(c => c.Status == ReminderCandidateStatus.Scheduled && c.CheckAtUtc <= now)
            .OrderBy(c => c.CheckAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ReminderCandidate>> GetByPersonAndActionAsync(string personId, string actionType, CancellationToken cancellationToken)
    {
        return await _context.ReminderCandidates
            .Where(c => c.PersonId == personId && c.SuggestedAction == actionType)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ReminderCandidate candidate, CancellationToken cancellationToken)
    {
        await _context.ReminderCandidates.AddAsync(candidate, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ReminderCandidate candidate, CancellationToken cancellationToken)
    {
        _context.ReminderCandidates.Update(candidate);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(ReminderCandidate candidate, CancellationToken cancellationToken)
    {
        _context.ReminderCandidates.Remove(candidate);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

