// Comprehensive integration tests for Events → Reminders → ReminderCandidates middleware
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
using System.Text.Json;
using Xunit;

public class EventReminderMiddlewareTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IEventRepository _eventRepository;
    private readonly IReminderCandidateRepository _reminderRepository;
    private readonly ITransitionRepository _transitionRepository;
    private readonly IConfiguration _configuration;
    private readonly IngestEventCommandHandler _handler;
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;

    public EventReminderMiddlewareTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _eventRepository = new EventRepository(_context);
        _reminderRepository = new ReminderCandidateRepository(_context);
        _transitionRepository = new TransitionRepository(_context);

        _configuration = new ConfigurationBuilder()
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
        var contextBucketBuilder = new ContextBucketKeyBuilder(_configuration);
        var transitionLearner = new TransitionLearner(
            _eventRepository,
            _transitionRepository,
            contextBucketBuilder,
            _configuration,
            loggerFactory.CreateLogger<TransitionLearner>());

        var policyEvaluator = new ReminderPolicyEvaluator(
            _configuration,
            loggerFactory.CreateLogger<ReminderPolicyEvaluator>());

        var reminderScheduler = new ReminderScheduler(
            _context,
            _transitionRepository,
            policyEvaluator,
            _configuration,
            loggerFactory.CreateLogger<ReminderScheduler>());

        var mapper = new AutoMapper.Mapper(new AutoMapper.MapperConfiguration(cfg =>
            cfg.AddProfile<AIPatterner.Application.Mappings.MappingProfile>()));

        var mockExecutionHistoryService = new MockExecutionHistoryService();
        var configRepo = new ConfigurationRepository(_context);
        var matchingPolicyService = new MatchingPolicyService(configRepo, _configuration);
        var matchingRemindersService = new MatchingRemindersService(_eventRepository, _context, mapper);
        
        var routineRepository = new RoutineRepository(_context);
        var routineReminderRepository = new RoutineReminderRepository(_context);
        var routineLearningService = new RoutineLearningService(
            routineRepository,
            routineReminderRepository,
            _eventRepository,
            _configuration,
            loggerFactory.CreateLogger<RoutineLearningService>());

        var mockUserContextService = new MockUserContextService();
        
        _handler = new IngestEventCommandHandler(
            _eventRepository,
            transitionLearner,
            reminderScheduler,
            _reminderRepository,
            mapper,
            mockExecutionHistoryService,
            _configuration,
            matchingRemindersService,
            matchingPolicyService,
            routineLearningService,
            mockUserContextService);

        // Setup matching policies in configuration
        SetupMatchingPoliciesAsync().GetAwaiter().GetResult();

        // Setup HTTP client for API tests
        _apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:8080/api";
        _httpClient = new HttpClient { BaseAddress = new Uri(_apiBaseUrl) };
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    private async Task SetupMatchingPoliciesAsync()
    {
        var configRepo = new ConfigurationRepository(_context);
        var policies = new[]
        {
            ("MatchByActionType", "true", "Match by action type"),
            ("MatchByDayType", "true", "Match by day type"),
            ("MatchByPeoplePresent", "true", "Match by people present"),
            ("MatchByStateSignals", "true", "Match by state signals"),
            ("MatchByTimeBucket", "false", "Match by time bucket"),
            ("MatchByLocation", "false", "Match by location"),
            ("TimeOffsetMinutes", "30", "Time offset in minutes")
        };

        foreach (var (key, value, description) in policies)
        {
            var existing = await configRepo.GetByKeyAndCategoryAsync(key, "MatchingPolicy", CancellationToken.None);
            if (existing == null)
            {
                var config = new Configuration(key, value, "MatchingPolicy", description);
                await configRepo.AddAsync(config, CancellationToken.None);
            }
        }
    }

    #region Event Creation Tests

    [Fact]
    public async Task CreateEvent_WithProbability_ShouldCreateReminderCandidate()
    {
        // Arrange
        var eventDto = new ActionEventDto
        {
            PersonId = "user1",
            ActionType = "drink_water",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto
            {
                TimeBucket = "morning",
                DayType = "weekday",
                Location = "home"
            },
            ProbabilityValue = 0.3,
            ProbabilityAction = ProbabilityAction.Increase,
            CustomData = new Dictionary<string, string> { { "source", "test" } }
        };

        // Act
        var command = new IngestEventCommand { Event = eventDto };
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        response.EventId.Should().NotBeEmpty();
        response.RelatedReminderId.Should().NotBeNull();

        var reminder = await _reminderRepository.GetByIdAsync(response.RelatedReminderId!.Value, CancellationToken.None);
        reminder.Should().NotBeNull();
        reminder!.PersonId.Should().Be("user1");
        reminder.SuggestedAction.Should().Be("drink_water");
        reminder.Confidence.Should().BeApproximately(0.5, 0.01); // Default confidence
        reminder.CheckAtUtc.Should().BeCloseTo(eventDto.TimestampUtc, TimeSpan.FromSeconds(1));
        reminder.SourceEventId.Should().Be(response.EventId);
        reminder.CustomData.Should().ContainKey("source").WhoseValue.Should().Be("test");
        reminder.Occurrence.Should().NotBeNullOrEmpty();
        reminder.Occurrence.Should().Contain("Occurs every");
    }

    [Fact]
    public async Task CreateEvent_WithoutProbability_ShouldNotCreateReminder()
    {
        // Arrange
        var eventDto = new ActionEventDto
        {
            PersonId = "user1",
            ActionType = "random_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto
            {
                TimeBucket = "afternoon",
                DayType = "weekend"
            }
        };

        // Act
        var command = new IngestEventCommand { Event = eventDto };
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        response.EventId.Should().NotBeEmpty();
        response.RelatedReminderId.Should().BeNull();
    }

    [Fact]
    public async Task CreateEvent_ViaAPI_ShouldReturnAccepted()
    {
        // Skip if API is not available
        if (!await IsApiAvailableAsync())
        {
            return; // Skip test if API is not running
        }

        // Arrange
        var eventDto = new ActionEventDto
        {
            PersonId = "api_user",
            ActionType = "api_test_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday"
            },
            ProbabilityValue = 0.2,
            ProbabilityAction = ProbabilityAction.Increase
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/v1/events", eventDto);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted);
        var result = await response.Content.ReadFromJsonAsync<IngestEventResponse>();
        result.Should().NotBeNull();
        result!.EventId.Should().NotBeEmpty();
    }

    #endregion

    #region Matching Logic Tests

    [Fact]
    public async Task CreateMatchingEvents_ShouldUpdateExistingReminder()
    {
        // Arrange - Create first event with reminder
        var firstEvent = new ActionEventDto
        {
            PersonId = "user2",
            ActionType = "exercise",
            TimestampUtc = new DateTime(2026, 1, 2, 17, 0, 0, DateTimeKind.Utc), // Friday 17:00
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "gym",
                PresentPeople = new List<string> { "user2" },
                StateSignals = new Dictionary<string, string> { { "energy", "high" } }
            },
            ProbabilityValue = 0.3,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var firstCommand = new IngestEventCommand { Event = firstEvent };
        var firstResponse = await _handler.Handle(firstCommand, CancellationToken.None);
        var firstReminderId = firstResponse.RelatedReminderId!.Value;

        // Act - Create second matching event (same time, same context)
        var secondEvent = new ActionEventDto
        {
            PersonId = "user2",
            ActionType = "exercise",
            TimestampUtc = new DateTime(2026, 1, 9, 17, 5, 0, DateTimeKind.Utc), // Next Friday 17:05 (within 30 min offset)
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "gym",
                PresentPeople = new List<string> { "user2" },
                StateSignals = new Dictionary<string, string> { { "energy", "high" } }
            },
            ProbabilityValue = 0.2,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var secondCommand = new IngestEventCommand { Event = secondEvent };
        var secondResponse = await _handler.Handle(secondCommand, CancellationToken.None);

        // Assert - Should update existing reminder, not create new one
        secondResponse.RelatedReminderId.Should().Be(firstReminderId);

        var updatedReminder = await _reminderRepository.GetByIdAsync(firstReminderId, CancellationToken.None);
        updatedReminder.Should().NotBeNull();
        updatedReminder!.Confidence.Should().BeGreaterThan(0.5); // Increased from 0.5 + 0.3 + 0.2
        updatedReminder.CheckAtUtc.Should().BeCloseTo(secondEvent.TimestampUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateNonMatchingEvents_ShouldCreateNewReminder()
    {
        // Arrange - Create first event
        var firstEvent = new ActionEventDto
        {
            PersonId = "user3",
            ActionType = "read_book",
            TimestampUtc = new DateTime(2026, 1, 2, 20, 0, 0, DateTimeKind.Utc),
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday"
            },
            ProbabilityValue = 0.3,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var firstCommand = new IngestEventCommand { Event = firstEvent };
        var firstResponse = await _handler.Handle(firstCommand, CancellationToken.None);

        // Act - Create second event with different action type
        var secondEvent = new ActionEventDto
        {
            PersonId = "user3",
            ActionType = "watch_tv", // Different action
            TimestampUtc = new DateTime(2026, 1, 2, 20, 5, 0, DateTimeKind.Utc),
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday"
            },
            ProbabilityValue = 0.3,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var secondCommand = new IngestEventCommand { Event = secondEvent };
        var secondResponse = await _handler.Handle(secondCommand, CancellationToken.None);

        // Assert - Should create new reminder
        secondResponse.RelatedReminderId.Should().NotBeNull();
        firstResponse.RelatedReminderId.Should().NotBeNull();
        secondResponse.RelatedReminderId!.Value.Should().NotBe(firstResponse.RelatedReminderId!.Value);

        var reminders = await _reminderRepository.GetFilteredAsync("user3", null, null, null, 1, 100, CancellationToken.None);
        reminders.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateEvents_WithTimeOffset_ShouldMatchCorrectly()
    {
        // Arrange - Create reminder at 1 PM
        var firstEvent = new ActionEventDto
        {
            PersonId = "user4",
            ActionType = "lunch",
            TimestampUtc = new DateTime(2026, 1, 2, 13, 0, 0, DateTimeKind.Utc), // 1 PM
            Context = new ActionContextDto
            {
                TimeBucket = "afternoon",
                DayType = "weekday"
            },
            ProbabilityValue = 0.3,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var firstCommand = new IngestEventCommand { Event = firstEvent };
        var firstResponse = await _handler.Handle(firstCommand, CancellationToken.None);
        var firstReminderId = firstResponse.RelatedReminderId!.Value;

        // Act - Create event at 1:15 PM (within 30 min offset)
        var secondEvent = new ActionEventDto
        {
            PersonId = "user4",
            ActionType = "lunch",
            TimestampUtc = new DateTime(2026, 1, 9, 13, 15, 0, DateTimeKind.Utc), // Next week, 1:15 PM
            Context = new ActionContextDto
            {
                TimeBucket = "afternoon",
                DayType = "weekday"
            },
            ProbabilityValue = 0.2,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var secondCommand = new IngestEventCommand { Event = secondEvent };
        var secondResponse = await _handler.Handle(secondCommand, CancellationToken.None);

        // Assert - Should match (within 30 min offset)
        secondResponse.RelatedReminderId.Should().Be(firstReminderId);

        // Act - Create event at 5 PM (outside 30 min offset)
        var thirdEvent = new ActionEventDto
        {
            PersonId = "user4",
            ActionType = "lunch",
            TimestampUtc = new DateTime(2026, 1, 9, 17, 0, 0, DateTimeKind.Utc), // 5 PM
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday"
            },
            ProbabilityValue = 0.2,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var thirdCommand = new IngestEventCommand { Event = thirdEvent };
        var thirdResponse = await _handler.Handle(thirdCommand, CancellationToken.None);

        // Assert - Should NOT match (different time)
        thirdResponse.RelatedReminderId.Should().NotBe(firstReminderId);
        thirdResponse.RelatedReminderId.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateEvents_WithStrictStateSignals_ShouldMatchOnlyWhenAllMatch()
    {
        // Arrange
        var firstEvent = new ActionEventDto
        {
            PersonId = "user5",
            ActionType = "meditate",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto
            {
                TimeBucket = "morning",
                DayType = "weekday",
                StateSignals = new Dictionary<string, string>
                {
                    { "mood", "calm" },
                    { "stress", "low" }
                }
            },
            ProbabilityValue = 0.3,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var firstCommand = new IngestEventCommand { Event = firstEvent };
        var firstResponse = await _handler.Handle(firstCommand, CancellationToken.None);
        var firstReminderId = firstResponse.RelatedReminderId!.Value;

        // Act - Create event with matching state signals
        var matchingEvent = new ActionEventDto
        {
            PersonId = "user5",
            ActionType = "meditate",
            TimestampUtc = DateTime.UtcNow.AddDays(7),
            Context = new ActionContextDto
            {
                TimeBucket = "morning",
                DayType = "weekday",
                StateSignals = new Dictionary<string, string>
                {
                    { "mood", "calm" },
                    { "stress", "low" }
                }
            },
            ProbabilityValue = 0.2,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var matchingCommand = new IngestEventCommand { Event = matchingEvent };
        var matchingResponse = await _handler.Handle(matchingCommand, CancellationToken.None);

        // Assert - Should match
        matchingResponse.RelatedReminderId.Should().Be(firstReminderId);

        // Act - Create event with different state signals
        var nonMatchingEvent = new ActionEventDto
        {
            PersonId = "user5",
            ActionType = "meditate",
            TimestampUtc = DateTime.UtcNow.AddDays(14),
            Context = new ActionContextDto
            {
                TimeBucket = "morning",
                DayType = "weekday",
                StateSignals = new Dictionary<string, string>
                {
                    { "mood", "calm" },
                    { "stress", "high" } // Different value
                }
            },
            ProbabilityValue = 0.2,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var nonMatchingCommand = new IngestEventCommand { Event = nonMatchingEvent };
        var nonMatchingResponse = await _handler.Handle(nonMatchingCommand, CancellationToken.None);

        // Assert - Should NOT match (different state signal value)
        nonMatchingResponse.RelatedReminderId.Should().NotBe(firstReminderId);
    }

    #endregion

    #region Probability Update Tests

    [Fact]
    public async Task CreateEvents_WithIncreaseProbability_ShouldIncreaseConfidence()
    {
        // Arrange
        var increaseValue = 0.3;

        var firstEvent = new ActionEventDto
        {
            PersonId = "user6",
            ActionType = "water_plants",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "morning", DayType = "weekday" },
            ProbabilityValue = increaseValue,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var firstCommand = new IngestEventCommand { Event = firstEvent };
        var firstResponse = await _handler.Handle(firstCommand, CancellationToken.None);
        var reminderId = firstResponse.RelatedReminderId!.Value;

        var reminder = await _reminderRepository.GetByIdAsync(reminderId, CancellationToken.None);
        var initialConf = reminder!.Confidence;

        // Act - Create matching event with increase
        var secondEvent = new ActionEventDto
        {
            PersonId = "user6",
            ActionType = "water_plants",
            TimestampUtc = DateTime.UtcNow.AddDays(7),
            Context = new ActionContextDto { TimeBucket = "morning", DayType = "weekday" },
            ProbabilityValue = 0.2,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var secondCommand = new IngestEventCommand { Event = secondEvent };
        await _handler.Handle(secondCommand, CancellationToken.None);

        // Assert
        var updatedReminder = await _reminderRepository.GetByIdAsync(reminderId, CancellationToken.None);
        updatedReminder!.Confidence.Should().BeGreaterThan(initialConf);
        updatedReminder.Confidence.Should().BeLessOrEqualTo(1.0);
    }

    [Fact]
    public async Task CreateEvents_WithDecreaseProbability_ShouldDecreaseConfidence()
    {
        // Arrange
        var firstEvent = new ActionEventDto
        {
            PersonId = "user7",
            ActionType = "check_email",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "morning", DayType = "weekday" },
            ProbabilityValue = 0.3,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var firstCommand = new IngestEventCommand { Event = firstEvent };
        var firstResponse = await _handler.Handle(firstCommand, CancellationToken.None);
        var reminderId = firstResponse.RelatedReminderId!.Value;

        var reminder = await _reminderRepository.GetByIdAsync(reminderId, CancellationToken.None);
        var initialConf = reminder!.Confidence;

        // Act - Create matching event with decrease
        var secondEvent = new ActionEventDto
        {
            PersonId = "user7",
            ActionType = "check_email",
            TimestampUtc = DateTime.UtcNow.AddDays(7),
            Context = new ActionContextDto { TimeBucket = "morning", DayType = "weekday" },
            ProbabilityValue = 0.2,
            ProbabilityAction = ProbabilityAction.Decrease
        };

        var secondCommand = new IngestEventCommand { Event = secondEvent };
        await _handler.Handle(secondCommand, CancellationToken.None);

        // Assert
        var updatedReminder = await _reminderRepository.GetByIdAsync(reminderId, CancellationToken.None);
        updatedReminder!.Confidence.Should().BeLessThan(initialConf);
        updatedReminder.Confidence.Should().BeGreaterOrEqualTo(0.0);
    }

    #endregion

    #region Occurrence Generation Tests

    [Fact]
    public async Task CreateEvent_ShouldGenerateOccurrencePattern()
    {
        // Arrange
        var timestamp = new DateTime(2026, 1, 3, 17, 30, 0, DateTimeKind.Utc); // Friday 17:30

        var eventDto = new ActionEventDto
        {
            PersonId = "user8",
            ActionType = "weekly_review",
            TimestampUtc = timestamp,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            ProbabilityValue = 0.3,
            ProbabilityAction = ProbabilityAction.Increase
        };

        // Act
        var command = new IngestEventCommand { Event = eventDto };
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var reminder = await _reminderRepository.GetByIdAsync(response.RelatedReminderId!.Value, CancellationToken.None);
        reminder.Should().NotBeNull();
        reminder!.Occurrence.Should().NotBeNullOrEmpty();
        reminder.Occurrence.Should().Contain("Friday");
        reminder.Occurrence.Should().Contain("17:30");
        reminder.Occurrence.Should().MatchRegex(@"\+(\d+) minutes"); // Should have offset
    }

    [Fact]
    public async Task CreateEvent_CheckAtUtc_ShouldMatchTimestampUtc()
    {
        // Arrange
        var timestamp = new DateTime(2026, 1, 5, 9, 15, 0, DateTimeKind.Utc);

        var eventDto = new ActionEventDto
        {
            PersonId = "user9",
            ActionType = "morning_routine",
            TimestampUtc = timestamp,
            Context = new ActionContextDto { TimeBucket = "morning", DayType = "weekday" },
            ProbabilityValue = 0.3,
            ProbabilityAction = ProbabilityAction.Increase
        };

        // Act
        var command = new IngestEventCommand { Event = eventDto };
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var reminder = await _reminderRepository.GetByIdAsync(response.RelatedReminderId!.Value, CancellationToken.None);
        reminder.Should().NotBeNull();
        reminder!.CheckAtUtc.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task CreateMultipleEvents_Rapidly_ShouldHandleConcurrently()
    {
        // Arrange
        var tasks = new List<Task<IngestEventResponse>>();
        var baseTime = DateTime.UtcNow;

        // Act - Create 10 events rapidly
        for (int i = 0; i < 10; i++)
        {
            var eventDto = new ActionEventDto
            {
                PersonId = "user10",
                ActionType = $"action_{i}",
                TimestampUtc = baseTime.AddMinutes(i),
                Context = new ActionContextDto { TimeBucket = "morning", DayType = "weekday" },
                ProbabilityValue = 0.1,
                ProbabilityAction = ProbabilityAction.Increase
            };

            var command = new IngestEventCommand { Event = eventDto };
            tasks.Add(_handler.Handle(command, CancellationToken.None));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert - All should succeed
        responses.Should().AllSatisfy(r => r.EventId.Should().NotBeEmpty());
        responses.Should().HaveCount(10);
    }

    [Fact]
    public async Task CreateEvents_WithSmallTimeOffset_ShouldMatchCorrectly()
    {
        // Arrange
        var baseTime = new DateTime(2026, 1, 2, 14, 0, 0, DateTimeKind.Utc);

        var firstEvent = new ActionEventDto
        {
            PersonId = "user11",
            ActionType = "snack",
            TimestampUtc = baseTime,
            Context = new ActionContextDto { TimeBucket = "afternoon", DayType = "weekday" },
            ProbabilityValue = 0.3,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var firstCommand = new IngestEventCommand { Event = firstEvent };
        var firstResponse = await _handler.Handle(firstCommand, CancellationToken.None);
        var firstReminderId = firstResponse.RelatedReminderId!.Value;

        // Act - Create event 5 minutes later (within 30 min offset)
        var secondEvent = new ActionEventDto
        {
            PersonId = "user11",
            ActionType = "snack",
            TimestampUtc = baseTime.AddDays(7).AddMinutes(5),
            Context = new ActionContextDto { TimeBucket = "afternoon", DayType = "weekday" },
            ProbabilityValue = 0.2,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var secondCommand = new IngestEventCommand { Event = secondEvent };
        var secondResponse = await _handler.Handle(secondCommand, CancellationToken.None);

        // Assert - Should match
        secondResponse.RelatedReminderId.Should().Be(firstReminderId);
    }

    [Fact]
    public async Task CreateEvents_WithLowProbability_ShouldStillCreateReminder()
    {
        // Arrange
        var eventDto = new ActionEventDto
        {
            PersonId = "user12",
            ActionType = "low_prob_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            ProbabilityValue = 0.01, // Very low
            ProbabilityAction = ProbabilityAction.Increase
        };

        // Act
        var command = new IngestEventCommand { Event = eventDto };
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        response.RelatedReminderId.Should().NotBeNull();
        var reminder = await _reminderRepository.GetByIdAsync(response.RelatedReminderId!.Value, CancellationToken.None);
        reminder.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateEvents_WithHighProbability_ShouldCreateReminder()
    {
        // Arrange
        var eventDto = new ActionEventDto
        {
            PersonId = "user13",
            ActionType = "high_prob_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "morning", DayType = "weekday" },
            ProbabilityValue = 0.99, // Very high
            ProbabilityAction = ProbabilityAction.Increase
        };

        // Act
        var command = new IngestEventCommand { Event = eventDto };
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        response.RelatedReminderId.Should().NotBeNull();
        var reminder = await _reminderRepository.GetByIdAsync(response.RelatedReminderId!.Value, CancellationToken.None);
        reminder.Should().NotBeNull();
        reminder!.Confidence.Should().BeLessOrEqualTo(1.0);
    }

    #endregion

    #region Periodic Simulation Tests

    [Fact]
    public async Task SimulateDailyEvents_ShouldUpdateReminderCorrectly()
    {
        // Arrange
        var baseDate = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc); // Wednesday 8 AM
        var reminderId = Guid.Empty;

        // Act - Create events for 7 consecutive days
        for (int day = 0; day < 7; day++)
        {
            var eventDto = new ActionEventDto
            {
                PersonId = "daily_user",
                ActionType = "morning_coffee",
                TimestampUtc = baseDate.AddDays(day),
                Context = new ActionContextDto { TimeBucket = "morning", DayType = "weekday" },
                ProbabilityValue = 0.1,
                ProbabilityAction = ProbabilityAction.Increase
            };

            var command = new IngestEventCommand { Event = eventDto };
            var response = await _handler.Handle(command, CancellationToken.None);

            if (day == 0)
            {
                reminderId = response.RelatedReminderId!.Value;
            }
            else
            {
                // Should match the first reminder
                response.RelatedReminderId.Should().Be(reminderId);
            }
        }

        // Assert
        var reminder = await _reminderRepository.GetByIdAsync(reminderId, CancellationToken.None);
        reminder.Should().NotBeNull();
        reminder!.Confidence.Should().BeGreaterThan(0.5); // Increased 7 times
    }

    [Fact]
    public async Task SimulateWeeklyEvents_ShouldCreatePattern()
    {
        // Arrange
        var baseDate = new DateTime(2026, 1, 3, 18, 0, 0, DateTimeKind.Utc); // Friday 6 PM
        var reminderId = Guid.Empty;

        // Act - Create events for 4 consecutive Fridays
        for (int week = 0; week < 4; week++)
        {
            var eventDto = new ActionEventDto
            {
                PersonId = "weekly_user",
                ActionType = "friday_dinner",
                TimestampUtc = baseDate.AddDays(week * 7),
                Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
                ProbabilityValue = 0.15,
                ProbabilityAction = ProbabilityAction.Increase
            };

            var command = new IngestEventCommand { Event = eventDto };
            var response = await _handler.Handle(command, CancellationToken.None);

            if (week == 0)
            {
                reminderId = response.RelatedReminderId!.Value;
            }
            else
            {
                response.RelatedReminderId.Should().Be(reminderId);
            }
        }

        // Assert
        var reminder = await _reminderRepository.GetByIdAsync(reminderId, CancellationToken.None);
        reminder.Should().NotBeNull();
        reminder!.Occurrence.Should().Contain("Friday");
    }

    [Fact]
    public async Task SimulateMultipleUsers_ShouldIsolateReminders()
    {
        // Arrange
        var users = new[] { "user_a", "user_b", "user_c" };
        var reminders = new Dictionary<string, Guid>();

        // Act - Create events for each user
        foreach (var user in users)
        {
            var eventDto = new ActionEventDto
            {
                PersonId = user,
                ActionType = "personal_task",
                TimestampUtc = DateTime.UtcNow,
                Context = new ActionContextDto { TimeBucket = "morning", DayType = "weekday" },
                ProbabilityValue = 0.3,
                ProbabilityAction = ProbabilityAction.Increase
            };

            var command = new IngestEventCommand { Event = eventDto };
            var response = await _handler.Handle(command, CancellationToken.None);
            reminders[user] = response.RelatedReminderId!.Value;
        }

        // Assert - Each user should have their own reminder
        reminders.Should().HaveCount(3);
        reminders.Values.Should().OnlyHaveUniqueItems();

        foreach (var (user, reminderId) in reminders)
        {
            var reminder = await _reminderRepository.GetByIdAsync(reminderId, CancellationToken.None);
            reminder.Should().NotBeNull();
            reminder!.PersonId.Should().Be(user);
        }
    }

    #endregion

    #region API Integration Tests

    [Fact]
    public async Task GetReminderCandidates_ViaAPI_ShouldReturnList()
    {
        // Skip if API is not available
        if (!await IsApiAvailableAsync())
        {
            return;
        }

        // Arrange - Create reminder via handler first
        var eventDto = new ActionEventDto
        {
            PersonId = "api_test_user",
            ActionType = "api_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "morning", DayType = "weekday" },
            ProbabilityValue = 0.3,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var command = new IngestEventCommand { Event = eventDto };
        await _handler.Handle(command, CancellationToken.None);

        // Act
        var response = await _httpClient.GetAsync("/v1/reminder-candidates?personId=api_test_user");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ReminderCandidateListResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetRelatedReminders_ViaAPI_ShouldReturnReminders()
    {
        // Skip if API is not available
        if (!await IsApiAvailableAsync())
        {
            return;
        }

        // Arrange - Create event with reminder
        var eventDto = new ActionEventDto
        {
            PersonId = "api_related_user",
            ActionType = "api_related_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "afternoon", DayType = "weekday" },
            ProbabilityValue = 0.3,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var command = new IngestEventCommand { Event = eventDto };
        var response = await _handler.Handle(command, CancellationToken.None);
        var eventId = response.EventId;

        // Act
        var apiResponse = await _httpClient.GetAsync($"/v1/events/{eventId}/related-reminders");

        // Assert
        apiResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var result = await apiResponse.Content.ReadFromJsonAsync<ReminderCandidateListResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.Items[0].SourceEventId.Should().Be(eventId);
    }

    #endregion

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

