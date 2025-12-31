// Repository implementation for configurations
namespace AIPatterner.Infrastructure.Persistence.Repositories;

using AIPatterner.Application.Handlers;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class ConfigurationRepository : IConfigurationRepository
{
    private readonly ApplicationDbContext _context;

    public ConfigurationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Configuration configuration, CancellationToken cancellationToken)
    {
        _context.Configurations.Add(configuration);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Configuration?> GetByKeyAndCategoryAsync(string key, string category, CancellationToken cancellationToken)
    {
        return await _context.Configurations
            .FirstOrDefaultAsync(c => c.Key == key && c.Category == category, cancellationToken);
    }

    public async Task<List<Configuration>> GetByCategoryAsync(string? category, CancellationToken cancellationToken)
    {
        var query = _context.Configurations.AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(c => c.Category == category);
        }

        return await query
            .OrderBy(c => c.Category)
            .ThenBy(c => c.Key)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(Configuration configuration, CancellationToken cancellationToken)
    {
        _context.Configurations.Update(configuration);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

