// Service implementation for managing reminder cooldowns
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Application.Handlers;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class CooldownService : ICooldownService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CooldownService> _logger;

    public CooldownService(ApplicationDbContext context, ILogger<CooldownService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task AddCooldownAsync(
        string personId,
        string actionType,
        TimeSpan duration,
        string? reason,
        CancellationToken cancellationToken)
    {
        var cooldown = new ReminderCooldown(
            personId,
            actionType,
            DateTime.UtcNow.Add(duration),
            reason);

        await _context.ReminderCooldowns.AddAsync(cooldown, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Added cooldown for {PersonId}, action: {ActionType}, until: {UntilUtc}",
            personId, actionType, cooldown.SuppressedUntilUtc);
    }

    public async Task<bool> IsCooldownActiveAsync(
        string personId,
        string actionType,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var activeCooldown = await _context.ReminderCooldowns
            .AnyAsync(c =>
                c.PersonId == personId &&
                c.ActionType == actionType &&
                c.SuppressedUntilUtc > now,
                cancellationToken);

        return activeCooldown;
    }
}

