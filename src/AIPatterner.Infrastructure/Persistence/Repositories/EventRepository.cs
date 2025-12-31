// EF Core repository implementation for ActionEvent
namespace AIPatterner.Infrastructure.Persistence.Repositories;

using AIPatterner.Application.Handlers;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class EventRepository : IEventRepository
{
    private readonly ApplicationDbContext _context;

    public EventRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ActionEvent actionEvent, CancellationToken cancellationToken)
    {
        await _context.ActionEvents.AddAsync(actionEvent, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ActionEvent?> GetLastEventForPersonAsync(string personId, DateTime beforeUtc, CancellationToken cancellationToken)
    {
        return await _context.ActionEvents
            .Where(e => e.PersonId == personId && e.TimestampUtc < beforeUtc)
            .OrderByDescending(e => e.TimestampUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ActionEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.ActionEvents
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task DeleteAsync(ActionEvent actionEvent, CancellationToken cancellationToken)
    {
        _context.ActionEvents.Remove(actionEvent);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

