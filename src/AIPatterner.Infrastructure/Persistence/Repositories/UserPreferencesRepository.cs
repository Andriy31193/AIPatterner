// EF Core repository implementation for UserReminderPreferences
namespace AIPatterner.Infrastructure.Persistence.Repositories;

using AIPatterner.Application.Handlers;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class UserPreferencesRepository : IUserPreferencesRepository
{
    private readonly ApplicationDbContext _context;

    public UserPreferencesRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserReminderPreferences?> GetByPersonIdAsync(string personId, CancellationToken cancellationToken)
    {
        return await _context.UserReminderPreferences
            .FirstOrDefaultAsync(p => p.PersonId == personId, cancellationToken);
    }

    public async Task AddAsync(UserReminderPreferences preferences, CancellationToken cancellationToken)
    {
        await _context.UserReminderPreferences.AddAsync(preferences, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(UserReminderPreferences preferences, CancellationToken cancellationToken)
    {
        _context.UserReminderPreferences.Update(preferences);
        await _context.SaveChangesAsync(cancellationToken);
    }
}


