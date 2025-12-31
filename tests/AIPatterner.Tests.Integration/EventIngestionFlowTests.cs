// Integration tests for event ingestion flow
namespace AIPatterner.Tests.Integration;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Application.Handlers;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using AIPatterner.Infrastructure.Persistence.Repositories;
using AIPatterner.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
//using Moq;
using Xunit;

public class EventIngestionFlowTests
{
    [Fact]
    public async Task IngestEvent_ShouldCreateTransitionAndScheduleCandidate()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options);
        var eventRepo = new EventRepository(context);
        var transitionRepo = new TransitionRepository(context);
        var candidateRepo = new ReminderCandidateRepository(context);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Learning:SessionWindowMinutes", "30" },
                { "Learning:ConfidenceAlpha", "0.1" },
                { "Learning:DelayBeta", "0.2" },
                { "ContextBucket:Format", "{dayType}*{timeBucket}*{location}" },
                { "Policy:MinimumOccurrences", "1" },
                { "Policy:MinimumConfidence", "0.05" }
            })
            .Build();

        var contextBucketBuilder = new ContextBucketKeyBuilder(config);
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var transitionLearner = new TransitionLearner(
            eventRepo,
            transitionRepo,
            contextBucketBuilder,
            config,
            loggerFactory.CreateLogger<TransitionLearner>());

        var policyEvaluator = new ReminderPolicyEvaluator(
            config,
            loggerFactory.CreateLogger<ReminderPolicyEvaluator>());

        var reminderScheduler = new ReminderScheduler(
            context,
            transitionRepo,
            policyEvaluator,
            config,
            loggerFactory.CreateLogger<ReminderScheduler>());

        var mapper = new AutoMapper.Mapper(new AutoMapper.MapperConfiguration(cfg =>
            cfg.AddProfile<AIPatterner.Application.Mappings.MappingProfile>()));

        // Mock execution history service for tests
        var mockExecutionHistoryService = new MockExecutionHistoryService();
        
        var handler = new IngestEventCommandHandler(
            eventRepo,
            transitionLearner,
            reminderScheduler,
            candidateRepo,
            mapper,
            mockExecutionHistoryService,
            config);

        var firstEvent = new ActionEventDto
        {
            PersonId = "alex",
            ActionType = "sit_on_couch",
            TimestampUtc = DateTime.UtcNow.AddMinutes(-10),
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "living_room"
            }
        };

        var firstCommand = new IngestEventCommand { Event = firstEvent };
        await handler.Handle(firstCommand, CancellationToken.None);

        var secondEvent = new ActionEventDto
        {
            PersonId = "alex",
            ActionType = "play_music",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "living_room"
            }
        };

        var secondCommand = new IngestEventCommand { Event = secondEvent };
        var response = await handler.Handle(secondCommand, CancellationToken.None);

        response.EventId.Should().NotBeEmpty();

        var transitions = await transitionRepo.GetByPersonIdAsync("alex", CancellationToken.None);
        transitions.Should().HaveCount(1);
        transitions[0].FromAction.Should().Be("sit_on_couch");
        transitions[0].ToAction.Should().Be("play_music");
    }
}

// Mock implementation of IExecutionHistoryService for tests
public class MockExecutionHistoryService : AIPatterner.Application.Handlers.IExecutionHistoryService
{
    public Task RecordExecutionAsync(
        string endpoint,
        string requestPayload,
        string responsePayload,
        DateTime executedAtUtc,
        string? personId = null,
        string? userId = null,
        string? actionType = null,
        Guid? reminderCandidateId = null,
        Guid? eventId = null,
        CancellationToken cancellationToken = default)
    {
        // No-op for tests
        return Task.CompletedTask;
    }
}

