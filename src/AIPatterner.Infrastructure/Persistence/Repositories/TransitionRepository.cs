// EF Core repository implementation for ActionTransition
namespace AIPatterner.Infrastructure.Persistence.Repositories;

using AIPatterner.Application.Handlers;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class TransitionRepository : AIPatterner.Application.Handlers.ITransitionRepository
{
    private readonly ApplicationDbContext _context;

    public TransitionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ActionTransition>> GetByPersonIdAsync(string personId, CancellationToken cancellationToken)
    {
        return await _context.ActionTransitions
            .Where(t => t.PersonId == personId)
            .OrderByDescending(t => t.Confidence)
            .ToListAsync(cancellationToken);
    }

    public async Task<ActionTransition?> GetByKeyAsync(
        string personId,
        string fromAction,
        string toAction,
        string contextBucket,
        CancellationToken cancellationToken)
    {
        return await _context.ActionTransitions
            .FirstOrDefaultAsync(t =>
                t.PersonId == personId &&
                t.FromAction == fromAction &&
                t.ToAction == toAction &&
                t.ContextBucket == contextBucket,
                cancellationToken);
    }

    public async Task AddAsync(ActionTransition transition, CancellationToken cancellationToken)
    {
        await _context.ActionTransitions.AddAsync(transition, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ActionTransition transition, CancellationToken cancellationToken)
    {
        _context.ActionTransitions.Update(transition);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ActionTransition>> GetRecentTransitionsForPersonAsync(
        string personId,
        string fromAction,
        CancellationToken cancellationToken)
    {
        return await _context.ActionTransitions
            .Where(t => t.PersonId == personId && t.FromAction == fromAction)
            .OrderByDescending(t => t.LastObservedUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<ActionTransition?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.ActionTransitions.FindAsync(new object[] { id }, cancellationToken);
    }
}

