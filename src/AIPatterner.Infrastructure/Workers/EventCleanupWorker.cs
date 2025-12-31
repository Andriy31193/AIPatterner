// Background worker for cleaning up old action events
namespace AIPatterner.Infrastructure.Workers;

using AIPatterner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class EventCleanupWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EventCleanupWorker> _logger;

    public EventCleanupWorker(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<EventCleanupWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(
            _configuration.GetValue<int>("Cleanup:EventCleanupIntervalHours", 24));
        var retentionDays = _configuration.GetValue<int>("Cleanup:EventRetentionDays", 30);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
                var deletedCount = await context.ActionEvents
                    .Where(e => e.CreatedAtUtc < cutoffDate)
                    .ExecuteDeleteAsync(stoppingToken);

                if (deletedCount > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} old action events older than {CutoffDate}", deletedCount, cutoffDate);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during event cleanup");
            }
        }
    }
}

