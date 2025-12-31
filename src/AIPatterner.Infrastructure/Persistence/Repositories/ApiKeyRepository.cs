// Repository implementation for API keys
namespace AIPatterner.Infrastructure.Persistence.Repositories;

using AIPatterner.Application.Handlers;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly ApplicationDbContext _context;

    public ApiKeyRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ApiKey apiKey, CancellationToken cancellationToken)
    {
        _context.ApiKeys.Add(apiKey);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == id, cancellationToken);
    }

    public async Task<List<ApiKey>> GetByUserIdAsync(Guid? userId, CancellationToken cancellationToken)
    {
        var query = _context.ApiKeys.AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(k => k.UserId == userId.Value);
        }

        return await query
            .OrderByDescending(k => k.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(ApiKey apiKey, CancellationToken cancellationToken)
    {
        _context.ApiKeys.Remove(apiKey);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

