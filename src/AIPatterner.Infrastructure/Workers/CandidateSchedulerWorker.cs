// Background worker for processing due reminder candidates
namespace AIPatterner.Infrastructure.Workers;

using AIPatterner.Application.Commands;
using AIPatterner.Application.Handlers;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class CandidateSchedulerWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CandidateSchedulerWorker> _logger;

    public CandidateSchedulerWorker(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<CandidateSchedulerWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(
            _configuration.GetValue<int>("Scheduler:PollIntervalSeconds", 30));
        var batchSize = _configuration.GetValue<int>("Scheduler:BatchSize", 10);

        // Wait a bit on startup to ensure migrations have completed
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IReminderCandidateRepository>();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                var dueCandidates = await repository.GetDueCandidatesAsync(DateTime.UtcNow, batchSize, stoppingToken);
                
                // Prioritize high-confidence candidates for auto-execution
                var minProbability = configuration.GetValue<double>("Policy:MinimumProbabilityForExecution", 0.7);
                var sortedCandidates = dueCandidates
                    .OrderByDescending(c => c.Confidence >= minProbability)
                    .ThenByDescending(c => c.Confidence)
                    .ToList();

                foreach (var candidate in sortedCandidates)
                {
                    try
                    {
                        var command = new ProcessReminderCandidateCommand { CandidateId = candidate.Id };
                        await mediator.Send(command, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing candidate {CandidateId}", candidate.Id);
                    }
                }

                if (dueCandidates.Any())
                {
                    _logger.LogInformation("Processed {Count} due reminder candidates", dueCandidates.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in candidate scheduler worker");
            }
        }
    }
}

