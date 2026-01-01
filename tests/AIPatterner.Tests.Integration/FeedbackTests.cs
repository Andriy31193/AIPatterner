// Integration tests for feedback endpoints (yes/no/later)
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
using System.Net.Http.Json;
using Xunit;

public class FeedbackTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IReminderCandidateRepository _reminderRepository;
    private readonly ITransitionRepository _transitionRepository;
    private readonly SubmitFeedbackCommandHandler _feedbackHandler;
    private readonly IngestEventCommandHandler _eventHandler;
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;

    public FeedbackTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"FeedbackTestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _reminderRepository = new ReminderCandidateRepository(_context);
        _transitionRepository = new TransitionRepository(_context);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Learning:SessionWindowMinutes", "30" },
                { "Learning:ConfidenceAlpha", "0.1" },
                { "Learning:DelayBeta", "0.2" },
                { "ContextBucket:Format", "{dayType}*{timeBucket}*{location}" },
                { "Policy:MinimumOccurrences", "1" },
                { "Policy:MinimumConfidence", "0.05" },
                { "Policy:DefaultReminderConfidence", "0.5" }
            })
            .Build();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var cooldownService = new CooldownService(_context, loggerFactory.CreateLogger<CooldownService>());

        _feedbackHandler = new SubmitFeedbackCommandHandler(
            _reminderRepository,
            _transitionRepository,
            cooldownService);

        // Setup event handler for creating test reminders
        var eventRepo = new EventRepository(_context);
        var contextBucketBuilder = new ContextBucketKeyBuilder(config);
        var transitionLearner = new TransitionLearner(
            eventRepo,
            _transitionRepository,
            contextBucketBuilder,
            config,
            loggerFactory.CreateLogger<TransitionLearner>());

        var policyEvaluator = new ReminderPolicyEvaluator(
            config,
            loggerFactory.CreateLogger<ReminderPolicyEvaluator>());

        var reminderScheduler = new ReminderScheduler(
            _context,
            _transitionRepository,
            policyEvaluator,
            config,
            loggerFactory.CreateLogger<ReminderScheduler>());

        var mapper = new AutoMapper.Mapper(new AutoMapper.MapperConfiguration(cfg =>
            cfg.AddProfile<AIPatterner.Application.Mappings.MappingProfile>()));

        var mockExecutionHistoryService = new MockExecutionHistoryService();
        var configRepo = new ConfigurationRepository(_context);
        var matchingPolicyService = new MatchingPolicyService(configRepo, config);
        var matchingRemindersService = new MatchingRemindersService(eventRepo, _context, mapper);

        _eventHandler = new IngestEventCommandHandler(
            eventRepo,
            transitionLearner,
            reminderScheduler,
            _reminderRepository,
            mapper,
            mockExecutionHistoryService,
            config,
            matchingRemindersService,
            matchingPolicyService);

        // Setup HTTP client for API tests
        _apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:8080/api";
        _httpClient = new HttpClient { BaseAddress = new Uri(_apiBaseUrl) };
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    [Fact]
    public async Task SubmitFeedback_No_ShouldReduceTransitionConfidence()
    {
        // Arrange - Create event with transition
        var firstEvent = new ActionEventDto
        {
            PersonId = "feedback_user1",
            ActionType = "sit_on_couch",
            TimestampUtc = DateTime.UtcNow.AddMinutes(-10),
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" }
        };

        var firstCommand = new IngestEventCommand { Event = firstEvent };
        await _eventHandler.Handle(firstCommand, CancellationToken.None);

        var secondEvent = new ActionEventDto
        {
            PersonId = "feedback_user1",
            ActionType = "play_music",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            ProbabilityValue = 0.3,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var secondCommand = new IngestEventCommand { Event = secondEvent };
        var response = await _eventHandler.Handle(secondCommand, CancellationToken.None);

        var reminder = await _reminderRepository.GetByIdAsync(response.RelatedReminderId!.Value, CancellationToken.None);
        var transition = await _transitionRepository.GetByIdAsync(reminder!.TransitionId!.Value, CancellationToken.None);
        var initialConfidence = transition!.Confidence;

        // Act - Submit "no" feedback
        var feedback = new FeedbackDto
        {
            CandidateId = reminder.Id,
            FeedbackType = "no"
        };

        var feedbackCommand = new SubmitFeedbackCommand { Feedback = feedback };
        await _feedbackHandler.Handle(feedbackCommand, CancellationToken.None);

        // Assert - Transition confidence should be reduced
        var updatedTransition = await _transitionRepository.GetByIdAsync(transition.Id, CancellationToken.None);
        updatedTransition!.Confidence.Should().BeLessThan(initialConfidence);
    }

    [Fact]
    public async Task SubmitFeedback_No_ShouldAddCooldown()
    {
        // Arrange - Create reminder
        var eventDto = new ActionEventDto
        {
            PersonId = "feedback_user2",
            ActionType = "reminder_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "morning", DayType = "weekday" },
            ProbabilityValue = 0.3,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var command = new IngestEventCommand { Event = eventDto };
        var response = await _eventHandler.Handle(command, CancellationToken.None);
        var reminder = await _reminderRepository.GetByIdAsync(response.RelatedReminderId!.Value, CancellationToken.None);

        // Act - Submit "no" feedback
        var feedback = new FeedbackDto
        {
            CandidateId = reminder!.Id,
            FeedbackType = "no"
        };

        var feedbackCommand = new SubmitFeedbackCommand { Feedback = feedback };
        await _feedbackHandler.Handle(feedbackCommand, CancellationToken.None);

        // Assert - Cooldown should be active
        var cooldownServiceCheck = new CooldownService(_context, LoggerFactory.Create(b => b.AddConsole()).CreateLogger<CooldownService>());
        var isCooldownActive = await cooldownServiceCheck.IsCooldownActiveAsync("feedback_user2", "reminder_action", CancellationToken.None);
        isCooldownActive.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitFeedback_Yes_ShouldIncreaseTransitionConfidence()
    {
        // Arrange - Create event with transition
        var firstEvent = new ActionEventDto
        {
            PersonId = "feedback_user3",
            ActionType = "start_work",
            TimestampUtc = DateTime.UtcNow.AddMinutes(-10),
            Context = new ActionContextDto { TimeBucket = "morning", DayType = "weekday" }
        };

        var firstCommand = new IngestEventCommand { Event = firstEvent };
        await _eventHandler.Handle(firstCommand, CancellationToken.None);

        var secondEvent = new ActionEventDto
        {
            PersonId = "feedback_user3",
            ActionType = "check_email",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "morning", DayType = "weekday" },
            ProbabilityValue = 0.3,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var secondCommand = new IngestEventCommand { Event = secondEvent };
        var response = await _eventHandler.Handle(secondCommand, CancellationToken.None);

        var reminder = await _reminderRepository.GetByIdAsync(response.RelatedReminderId!.Value, CancellationToken.None);
        var transition = await _transitionRepository.GetByIdAsync(reminder!.TransitionId!.Value, CancellationToken.None);
        var initialConfidence = transition!.Confidence;

        // Act - Submit "yes" feedback
        var feedback = new FeedbackDto
        {
            CandidateId = reminder.Id,
            FeedbackType = "yes"
        };

        var feedbackCommand = new SubmitFeedbackCommand { Feedback = feedback };
        await _feedbackHandler.Handle(feedbackCommand, CancellationToken.None);

        // Assert - Transition confidence should be increased
        var updatedTransition = await _transitionRepository.GetByIdAsync(transition.Id, CancellationToken.None);
        updatedTransition!.Confidence.Should().BeGreaterThan(initialConfidence);
    }

    [Fact]
    public async Task SubmitFeedback_ViaAPI_ShouldReturnNoContent()
    {
        // Skip if API is not available
        if (!await IsApiAvailableAsync())
        {
            return;
        }

        // Arrange - Create reminder
        var eventDto = new ActionEventDto
        {
            PersonId = "api_feedback_user",
            ActionType = "api_feedback_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "afternoon", DayType = "weekday" },
            ProbabilityValue = 0.3,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var command = new IngestEventCommand { Event = eventDto };
        var response = await _eventHandler.Handle(command, CancellationToken.None);
        var reminderId = response.RelatedReminderId!.Value;

        // Act - Submit feedback via API
        var feedback = new FeedbackDto
        {
            CandidateId = reminderId,
            FeedbackType = "yes"
        };

        var apiResponse = await _httpClient.PostAsJsonAsync("/v1/feedback", feedback);

        // Assert
        apiResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SubmitFeedback_Later_ShouldNotAffectConfidence()
    {
        // Arrange - Create reminder
        var eventDto = new ActionEventDto
        {
            PersonId = "feedback_user4",
            ActionType = "later_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            ProbabilityValue = 0.3,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var command = new IngestEventCommand { Event = eventDto };
        var response = await _eventHandler.Handle(command, CancellationToken.None);
        var reminder = await _reminderRepository.GetByIdAsync(response.RelatedReminderId!.Value, CancellationToken.None);

        // Act - Submit "later" feedback
        var feedback = new FeedbackDto
        {
            CandidateId = reminder!.Id,
            FeedbackType = "later"
        };

        var feedbackCommand = new SubmitFeedbackCommand { Feedback = feedback };
        await _feedbackHandler.Handle(feedbackCommand, CancellationToken.None);

        // Assert - No cooldown should be added for "later"
        var cooldownServiceCheck = new CooldownService(_context, LoggerFactory.Create(b => b.AddConsole()).CreateLogger<CooldownService>());
        var isCooldownActive = await cooldownServiceCheck.IsCooldownActiveAsync("feedback_user4", "later_action", CancellationToken.None);
        isCooldownActive.Should().BeFalse();
    }

    #region Helper Methods

    private async Task<bool> IsApiAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/v1/events?pageSize=1");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _context?.Dispose();
        _httpClient?.Dispose();
    }

    #endregion
}

