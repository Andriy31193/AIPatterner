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
                
                // Default behavior: only auto-process high-confidence + safe candidates.
                // Routine-triggered candidates are an exception: they are contextual (user intent) and should be processed
                // even when confidence is low, so we can Ask/Suggest during/after activation.
                var minProbability = configuration.GetValue<double>("Policy:MinimumProbabilityForExecution", 0.7);
                var candidatesToProcess = dueCandidates
                    .Where(c =>
                        IsRoutineCandidate(c) ||
                        (c.Confidence >= minProbability))
                    .OrderByDescending(c => IsRoutineCandidate(c) ? 1 : 0)
                    .ThenByDescending(c => c.Confidence)
                    .ToList();

                _logger.LogInformation(
                    "Found {TotalDue} due candidates, processing {ToProcess} candidates (threshold: {Threshold}, routine exceptions enabled)",
                    dueCandidates.Count, candidatesToProcess.Count, minProbability);

                foreach (var candidate in candidatesToProcess)
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

                if (candidatesToProcess.Any())
                {
                    _logger.LogInformation("Processed {Count} reminder candidates", candidatesToProcess.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in candidate scheduler worker");
            }
        }
    }

    private static bool IsRoutineCandidate(AIPatterner.Domain.Entities.ReminderCandidate candidate)
    {
        return candidate.CustomData != null &&
               candidate.CustomData.TryGetValue("source", out var source) &&
               string.Equals(source, "routine", StringComparison.OrdinalIgnoreCase);
    }
}

