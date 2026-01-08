namespace AIPatterner.Infrastructure.Workers;

using AIPatterner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Closes expired routine learning windows so they don't remain "open" indefinitely in storage.
/// Learning itself is still strictly enforced in-code, but this keeps state accurate and debuggable.
/// </summary>
public class RoutineLearningWindowCloserWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RoutineLearningWindowCloserWorker> _logger;

    public RoutineLearningWindowCloserWorker(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<RoutineLearningWindowCloserWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = _configuration.GetValue<int?>("Routine:LearningWindowCloser:PollIntervalSeconds") ?? 30;
        var interval = TimeSpan.FromSeconds(Math.Max(5, intervalSeconds));

        // Wait a bit on startup to ensure migrations have completed
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var now = DateTime.UtcNow;

                var expired = await db.Routines
                    .Where(r => r.ObservationWindowStartUtc.HasValue &&
                                r.ObservationWindowEndsAtUtc.HasValue &&
                                r.ObservationWindowEndsAtUtc.Value <= now)
                    .ToListAsync(stoppingToken);

                if (expired.Count == 0)
                {
                    continue;
                }

                foreach (var r in expired)
                {
                    r.CloseObservationWindow();
                }

                await db.SaveChangesAsync(stoppingToken);

                _logger.LogInformation("Closed {Count} expired routine learning windows", expired.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in routine learning window closer worker");
            }
        }
    }
}


