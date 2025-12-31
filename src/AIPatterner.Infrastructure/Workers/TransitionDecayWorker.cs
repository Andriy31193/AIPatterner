// Background worker for decaying old transitions
namespace AIPatterner.Infrastructure.Workers;

using AIPatterner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class TransitionDecayWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TransitionDecayWorker> _logger;

    public TransitionDecayWorker(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<TransitionDecayWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromDays(1);
        var decayRate = _configuration.GetValue<double>("Learning:DecayRate", 0.01);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var transitions = await context.ActionTransitions
                    .Where(t => t.Confidence > 0)
                    .ToListAsync(stoppingToken);

                foreach (var transition in transitions)
                {
                    transition.ApplyDecay(decayRate);
                }

                if (transitions.Any())
                {
                    await context.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Applied decay to {Count} transitions", transitions.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during transition decay");
            }
        }
    }
}

