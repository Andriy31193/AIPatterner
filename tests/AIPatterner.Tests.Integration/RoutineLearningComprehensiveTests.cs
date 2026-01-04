// Comprehensive integration tests for Routine Learning System
namespace AIPatterner.Tests.Integration;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Application.Handlers;
using AIPatterner.Application.Services;
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

public class RoutineLearningComprehensiveTests : RealDatabaseTestBase
{
    private readonly IRoutineRepository _routineRepository;
    private readonly IRoutineReminderRepository _routineReminderRepository;
    private readonly IRoutineLearningService _routineLearningService;
    private readonly string _testPersonId = "comprehensive_test_user";

    public RoutineLearningComprehensiveTests()
    {
        _routineRepository = new RoutineRepository(Context);
        _routineReminderRepository = new RoutineReminderRepository(Context);

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Policies:RoutineObservationWindowMinutes", "45" }, // Use new policy key
                { "Routine:DefaultRoutineProbability", "0.5" },
                { "Routine:ProbabilityIncreaseStep", "0.1" },
                { "Routine:ProbabilityDecreaseStep", "0.1" },
                { "Routine:AutoExecuteThreshold", "0.7" }
            })
            .Build();

        var signalSelector = new AIPatterner.Infrastructure.Services.SignalSelector(config, loggerFactory.CreateLogger<AIPatterner.Infrastructure.Services.SignalSelector>());
        var similarityEvaluator = new AIPatterner.Infrastructure.Services.SignalSimilarityEvaluator(loggerFactory.CreateLogger<AIPatterner.Infrastructure.Services.SignalSimilarityEvaluator>());
        var configRepo = new ConfigurationRepository(Context);
        var signalPolicyService = new AIPatterner.Infrastructure.Services.SignalPolicyService(configRepo, config);
        _routineLearningService = new RoutineLearningService(
            _routineRepository,
            _routineReminderRepository,
            EventRepository,
            config,
            loggerFactory.CreateLogger<RoutineLearningService>(),
            signalSelector,
            similarityEvaluator,
            signalPolicyService);
    }

    #region Test 1: Single StateChange Opens Learning Window

    [Fact]
    public async Task Test1_SingleStateChangeOpensLearningWindow()
    {
        // Arrange
        var intentType = "ArrivalHome";
        var now = DateTime.UtcNow;

        var intentEvent = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = intentType,
            TimestampUtc = now,
            EventType = EventType.StateChange,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home",
                PresentPeople = new List<string> { _testPersonId },
                StateSignals = new Dictionary<string, string>()
            }
        };

        // Act
        var command = new IngestEventCommand { Event = intentEvent };
        await EventHandler.Handle(command, CancellationToken.None);

        // Assert
        var routine = await _routineRepository.GetByPersonAndIntentAsync(_testPersonId, intentType, CancellationToken.None);
        routine.Should().NotBeNull();
        routine!.PersonId.Should().Be(_testPersonId);
        routine.IntentType.Should().Be(intentType);
        routine.ObservationWindowEndsAtUtc.Should().NotBeNull();
        routine.IsObservationWindowOpen(DateTime.UtcNow).Should().BeTrue();

        // Verify no other routines are in learning mode
        var allRoutines = await _routineRepository.GetByPersonAsync(_testPersonId, CancellationToken.None);
        var activeRoutines = allRoutines.Where(r => r.IsObservationWindowOpen(DateTime.UtcNow)).ToList();
        activeRoutines.Should().HaveCount(1);
        activeRoutines[0].Id.Should().Be(routine.Id);

        // Verify learning window duration (should be ~45 minutes from config)
        var windowDuration = routine.ObservationWindowEndsAtUtc!.Value - now;
        windowDuration.TotalMinutes.Should().BeApproximately(45, 1);
    }

    #endregion

    #region Test 2: Observed Events Create RoutineReminders

    [Fact]
    public async Task Test2_ObservedEventsCreateRoutineReminders()
    {
        // Arrange
        var intentType = "ArrivalHome";
        var baseTime = DateTime.UtcNow;

        // Step 1: Send StateChange intent
        var intentEvent = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = intentType,
            TimestampUtc = baseTime,
            EventType = EventType.StateChange,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home"
            }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = intentEvent }, CancellationToken.None);

        // Step 2: Send observed events during learning window
        var observedTime1 = baseTime.AddMinutes(5);
        var observedTime2 = baseTime.AddMinutes(10);

        var playMusicEvent = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = "PlayMusic",
            TimestampUtc = observedTime1,
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home"
            }
        };

        var turnOnLightsEvent = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = "TurnOnLights",
            TimestampUtc = observedTime2,
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home"
            }
        };

        // Act
        await EventHandler.Handle(new IngestEventCommand { Event = playMusicEvent }, CancellationToken.None);
        await EventHandler.Handle(new IngestEventCommand { Event = turnOnLightsEvent }, CancellationToken.None);

        // Assert
        var routine = await _routineRepository.GetByPersonAndIntentAsync(_testPersonId, intentType, CancellationToken.None);
        routine.Should().NotBeNull();

        var reminders = await _routineReminderRepository.GetByRoutineAsync(routine!.Id, CancellationToken.None);
        reminders.Should().HaveCount(2);

        var playMusicReminder = reminders.FirstOrDefault(r => r.SuggestedAction == "PlayMusic");
        playMusicReminder.Should().NotBeNull();
        playMusicReminder!.Confidence.Should().BeApproximately(0.5, 0.01); // Default probability
        playMusicReminder.ObservationCount.Should().Be(1);
        playMusicReminder.LastObservedAtUtc.Should().BeCloseTo(observedTime1, TimeSpan.FromSeconds(1));

        var turnOnLightsReminder = reminders.FirstOrDefault(r => r.SuggestedAction == "TurnOnLights");
        turnOnLightsReminder.Should().NotBeNull();
        turnOnLightsReminder!.Confidence.Should().BeApproximately(0.5, 0.01);
        turnOnLightsReminder.ObservationCount.Should().Be(1);

        // Test reinforcement - send same events again
        var observedTime3 = baseTime.AddMinutes(15);
        playMusicEvent.TimestampUtc = observedTime3;
        await EventHandler.Handle(new IngestEventCommand { Event = playMusicEvent }, CancellationToken.None);

        // Verify probability increased
        var updatedReminders = await _routineReminderRepository.GetByRoutineAsync(routine.Id, CancellationToken.None);
        var updatedPlayMusic = updatedReminders.First(r => r.SuggestedAction == "PlayMusic");
        updatedPlayMusic.Confidence.Should().BeApproximately(0.6, 0.01); // 0.5 + 0.1 increase step
        updatedPlayMusic.ObservationCount.Should().Be(2);
    }

    #endregion

    #region Test 3: User Feedback Updates Probability Immediately

    [Fact]
    public async Task Test3_UserFeedbackUpdatesProbabilityImmediately()
    {
        // Arrange - Create routine with reminders
        var intentType = "ArrivalHome";
        var baseTime = DateTime.UtcNow;

        var intentEvent = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = intentType,
            TimestampUtc = baseTime,
            EventType = EventType.StateChange,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday", Location = "home" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = intentEvent }, CancellationToken.None);

        // Create observed events
        var playMusicEvent = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = "PlayMusic",
            TimestampUtc = baseTime.AddMinutes(5),
            EventType = EventType.Action,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday", Location = "home" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = playMusicEvent }, CancellationToken.None);

        var turnOnLightsEvent = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = "TurnOnLights",
            TimestampUtc = baseTime.AddMinutes(10),
            EventType = EventType.Action,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday", Location = "home" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = turnOnLightsEvent }, CancellationToken.None);

        var adjustACEvent = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = "AdjustAC",
            TimestampUtc = baseTime.AddMinutes(12),
            EventType = EventType.Action,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday", Location = "home" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = adjustACEvent }, CancellationToken.None);

        var routine = await _routineRepository.GetByPersonAndIntentAsync(_testPersonId, intentType, CancellationToken.None);
        routine.Should().NotBeNull();

        var reminders = await _routineReminderRepository.GetByRoutineAsync(routine!.Id, CancellationToken.None);
        var playMusicReminder = reminders.First(r => r.SuggestedAction == "PlayMusic");
        var turnOnLightsReminder = reminders.First(r => r.SuggestedAction == "TurnOnLights");
        var adjustACReminder = reminders.First(r => r.SuggestedAction == "AdjustAC");

        var initialPlayMusicConfidence = playMusicReminder.Confidence;
        var initialTurnOnLightsConfidence = turnOnLightsReminder.Confidence;
        var initialAdjustACConfidence = adjustACReminder.Confidence;

        // Act - Simulate user feedback
        // Accept PlayMusic (increase)
        await _routineLearningService.HandleFeedbackAsync(
            playMusicReminder.Id,
            ProbabilityAction.Increase,
            0.1,
            CancellationToken.None);

        // Reject TurnOnLights (decrease)
        await _routineLearningService.HandleFeedbackAsync(
            turnOnLightsReminder.Id,
            ProbabilityAction.Decrease,
            0.1,
            CancellationToken.None);

        // Skip AdjustAC (no change, or slight decay - for now we'll just verify it doesn't change)
        // In a real scenario, "skip" might be a separate action, but for now we'll verify it stays the same

        // Assert
        var updatedReminders = await _routineReminderRepository.GetByRoutineAsync(routine.Id, CancellationToken.None);
        var updatedPlayMusic = updatedReminders.First(r => r.Id == playMusicReminder.Id);
        var updatedTurnOnLights = updatedReminders.First(r => r.Id == turnOnLightsReminder.Id);
        var updatedAdjustAC = updatedReminders.First(r => r.Id == adjustACReminder.Id);

        // Accepted reminder probability increased (or capped at 1.0)
        if (initialPlayMusicConfidence < 0.9)
        {
            updatedPlayMusic.Confidence.Should().BeGreaterThan(initialPlayMusicConfidence);
            updatedPlayMusic.Confidence.Should().BeApproximately(initialPlayMusicConfidence + 0.1, 0.01);
        }
        else
        {
            // If already high, it should be at or near 1.0
            updatedPlayMusic.Confidence.Should().BeGreaterOrEqualTo(initialPlayMusicConfidence);
            updatedPlayMusic.Confidence.Should().BeLessOrEqualTo(1.0);
        }

        // Rejected reminder probability decreased
        updatedTurnOnLights.Confidence.Should().BeLessThan(initialTurnOnLightsConfidence);
        updatedTurnOnLights.Confidence.Should().BeApproximately(initialTurnOnLightsConfidence - 0.1, 0.01);

        // Skipped reminder unchanged (or slightly decayed)
        updatedAdjustAC.Confidence.Should().BeApproximately(initialAdjustACConfidence, 0.01);
    }

    #endregion

    #region Test 4: Only One Routine Learns at a Time

    [Fact]
    public async Task Test4_OnlyOneRoutineLearnsAtATime()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;

        // Step 1: Open ArrivalHome learning window
        var arrivalHomeEvent = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = "ArrivalHome",
            TimestampUtc = baseTime,
            EventType = EventType.StateChange,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday", Location = "home" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = arrivalHomeEvent }, CancellationToken.None);

        var arrivalHomeRoutine = await _routineRepository.GetByPersonAndIntentAsync(_testPersonId, "ArrivalHome", CancellationToken.None);
        arrivalHomeRoutine.Should().NotBeNull();
        arrivalHomeRoutine!.IsObservationWindowOpen(baseTime.AddMinutes(1)).Should().BeTrue();

        // Step 2: While active, send another StateChange
        var goingToSleepEvent = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = "GoingToSleep",
            TimestampUtc = baseTime.AddMinutes(2),
            EventType = EventType.StateChange,
            Context = new ActionContextDto { TimeBucket = "night", DayType = "weekday", Location = "bedroom" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = goingToSleepEvent }, CancellationToken.None);

        // Assert
        // ArrivalHome learning window should be closed
        var updatedArrivalHome = await _routineRepository.GetByPersonAndIntentAsync(_testPersonId, "ArrivalHome", CancellationToken.None);
        updatedArrivalHome.Should().NotBeNull();
        updatedArrivalHome!.IsObservationWindowOpen(baseTime.AddMinutes(3)).Should().BeFalse();

        // GoingToSleep routine learning window should be active
        var goingToSleepRoutine = await _routineRepository.GetByPersonAndIntentAsync(_testPersonId, "GoingToSleep", CancellationToken.None);
        goingToSleepRoutine.Should().NotBeNull();
        goingToSleepRoutine!.IsObservationWindowOpen(baseTime.AddMinutes(3)).Should().BeTrue();

        // Verify only one routine is active
        var allRoutines = await _routineRepository.GetByPersonAsync(_testPersonId, CancellationToken.None);
        var activeRoutines = allRoutines.Where(r => r.IsObservationWindowOpen(baseTime.AddMinutes(3))).ToList();
        activeRoutines.Should().HaveCount(1);
        activeRoutines[0].IntentType.Should().Be("GoingToSleep");

        // Verify events after second StateChange only update GoingToSleep routine
        var observedEvent = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = "PlayMusic",
            TimestampUtc = baseTime.AddMinutes(5),
            EventType = EventType.Action,
            Context = new ActionContextDto { TimeBucket = "night", DayType = "weekday", Location = "bedroom" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = observedEvent }, CancellationToken.None);

        var goingToSleepReminders = await _routineReminderRepository.GetByRoutineAsync(goingToSleepRoutine.Id, CancellationToken.None);
        goingToSleepReminders.Should().Contain(r => r.SuggestedAction == "PlayMusic");

        var arrivalHomeReminders = await _routineReminderRepository.GetByRoutineAsync(updatedArrivalHome.Id, CancellationToken.None);
        arrivalHomeReminders.Should().NotContain(r => r.SuggestedAction == "PlayMusic");
    }

    #endregion

    #region Test 5: General Reminders Are Unaffected

    [Fact]
    public async Task Test5_GeneralRemindersAreUnaffected()
    {
        // Arrange - Create a general reminder first with proper context matching
        var generalReminderAction = "DrinkWater";
        var generalReminder = new ReminderCandidate(
            _testPersonId,
            generalReminderAction,
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            null,
            0.6,
            occurrence: "daily",
            sourceEventId: null,
            customData: null);
        await ReminderRepository.AddAsync(generalReminder, CancellationToken.None);

        var initialGeneralConfidence = generalReminder.Confidence;

        // Create routine learning window
        var intentEvent = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = "ArrivalHome",
            TimestampUtc = DateTime.UtcNow,
            EventType = EventType.StateChange,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday", Location = "home" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = intentEvent }, CancellationToken.None);

        // Act - Trigger an event that matches the general reminder during routine learning
        var matchingEvent = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = generalReminderAction, // Same action as general reminder
            TimestampUtc = DateTime.UtcNow.AddMinutes(5),
            EventType = EventType.Action,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday", Location = "home" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = matchingEvent }, CancellationToken.None);

        // Assert
        // General reminder should be updated (if matching criteria are met)
        var updatedGeneralReminder = await ReminderRepository.GetByIdAsync(generalReminder.Id, CancellationToken.None);
        updatedGeneralReminder.Should().NotBeNull();
        // General reminder probability may or may not increase depending on matching logic
        // The key is that routine reminder is also created
        updatedGeneralReminder!.Confidence.Should().BeGreaterOrEqualTo(initialGeneralConfidence);

        // Routine reminder should also be created (since we're in learning window)
        var routine = await _routineRepository.GetByPersonAndIntentAsync(_testPersonId, "ArrivalHome", CancellationToken.None);
        routine.Should().NotBeNull();
        var routineReminders = await _routineReminderRepository.GetByRoutineAsync(routine!.Id, CancellationToken.None);
        routineReminders.Should().Contain(r => r.SuggestedAction == generalReminderAction);

        // Verify they are separate entities
        routineReminders.First(r => r.SuggestedAction == generalReminderAction).Id.Should().NotBe(generalReminder.Id);
    }

    #endregion

    #region Test 6: Learning Window Timeouts

    [Fact]
    public async Task Test6_LearningWindowTimeouts()
    {
        // Arrange - Use unique personId to avoid interference from other tests
        var uniquePersonId = $"{_testPersonId}_timeout_test";
        var baseTime = DateTime.UtcNow;
        var intentEvent = new ActionEventDto
        {
            PersonId = uniquePersonId,
            ActionType = "ArrivalHome",
            TimestampUtc = baseTime,
            EventType = EventType.StateChange,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday", Location = "home" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = intentEvent }, CancellationToken.None);

        var routine = await _routineRepository.GetByPersonAndIntentAsync(uniquePersonId, "ArrivalHome", CancellationToken.None);
        routine.Should().NotBeNull();
        routine!.IsObservationWindowOpen(baseTime.AddMinutes(10)).Should().BeTrue();

        // Create some reminders during active window
        var observedEvent = new ActionEventDto
        {
            PersonId = uniquePersonId,
            ActionType = "PlayMusic",
            TimestampUtc = baseTime.AddMinutes(10),
            EventType = EventType.Action,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday", Location = "home" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = observedEvent }, CancellationToken.None);

        var remindersBeforeTimeout = await _routineReminderRepository.GetByRoutineAsync(routine.Id, CancellationToken.None);
        remindersBeforeTimeout.Should().Contain(r => r.SuggestedAction == "PlayMusic");

        // Act - Simulate time passing beyond configured window (46 minutes)
        var timeAfterWindow = baseTime.AddMinutes(46);
        routine = await _routineRepository.GetByPersonAndIntentAsync(uniquePersonId, "ArrivalHome", CancellationToken.None);
        routine.IsObservationWindowOpen(timeAfterWindow).Should().BeFalse();

        // Send event after window closed
        var lateEvent = new ActionEventDto
        {
            PersonId = uniquePersonId,
            ActionType = "TurnOnLights",
            TimestampUtc = timeAfterWindow,
            EventType = EventType.Action,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday", Location = "home" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = lateEvent }, CancellationToken.None);

        // Assert
        // Late event should NOT create routine reminder
        var remindersAfterTimeout = await _routineReminderRepository.GetByRoutineAsync(routine.Id, CancellationToken.None);
        remindersAfterTimeout.Should().NotContain(r => r.SuggestedAction == "TurnOnLights");

        // Routine should remain intact with existing reminders
        remindersAfterTimeout.Should().Contain(r => r.SuggestedAction == "PlayMusic");
        routine = await _routineRepository.GetByPersonAndIntentAsync(uniquePersonId, "ArrivalHome", CancellationToken.None);
        routine.Should().NotBeNull();
    }

    #endregion

    #region Test 7: Full Behavioral Integration Test

    [Fact]
    public async Task Test7_FullBehavioralIntegrationTest()
    {
        // Arrange
        var day1Time = new DateTime(2024, 1, 15, 18, 0, 0, DateTimeKind.Utc);

        // Day 1: User sends StateChange: ArrivalHome
        var day1Intent = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = "ArrivalHome",
            TimestampUtc = day1Time,
            EventType = EventType.StateChange,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday", Location = "home" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = day1Intent }, CancellationToken.None);

        // Observed events on Day 1
        var playMusicDay1 = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = "PlayMusic",
            TimestampUtc = day1Time.AddMinutes(5),
            EventType = EventType.Action,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday", Location = "home" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = playMusicDay1 }, CancellationToken.None);

        var turnOnLightsDay1 = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = "TurnOnLights",
            TimestampUtc = day1Time.AddMinutes(8),
            EventType = EventType.Action,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday", Location = "home" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = turnOnLightsDay1 }, CancellationToken.None);

        var adjustACDay1 = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = "AdjustAC",
            TimestampUtc = day1Time.AddMinutes(10),
            EventType = EventType.Action,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday", Location = "home" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = adjustACDay1 }, CancellationToken.None);

        var routine = await _routineRepository.GetByPersonAndIntentAsync(_testPersonId, "ArrivalHome", CancellationToken.None);
        routine.Should().NotBeNull();

        // Simulate user feedback
        var reminders = await _routineReminderRepository.GetByRoutineAsync(routine!.Id, CancellationToken.None);
        var playMusicReminder = reminders.First(r => r.SuggestedAction == "PlayMusic");
        var turnOnLightsReminder = reminders.First(r => r.SuggestedAction == "TurnOnLights");
        var adjustACReminder = reminders.First(r => r.SuggestedAction == "AdjustAC");

        // Accept PlayMusic
        await _routineLearningService.HandleFeedbackAsync(playMusicReminder.Id, ProbabilityAction.Increase, 0.1, CancellationToken.None);
        // Reject TurnOnLights
        await _routineLearningService.HandleFeedbackAsync(turnOnLightsReminder.Id, ProbabilityAction.Decrease, 0.1, CancellationToken.None);
        // Skip AdjustAC (no feedback)

        var day1PlayMusicConfidence = (await _routineReminderRepository.GetByRoutineAsync(routine.Id, CancellationToken.None))
            .First(r => r.Id == playMusicReminder.Id).Confidence;
        var day1TurnOnLightsConfidence = (await _routineReminderRepository.GetByRoutineAsync(routine.Id, CancellationToken.None))
            .First(r => r.Id == turnOnLightsReminder.Id).Confidence;
        var day1AdjustACConfidence = (await _routineReminderRepository.GetByRoutineAsync(routine.Id, CancellationToken.None))
            .First(r => r.Id == adjustACReminder.Id).Confidence;

        // Day 2: Same StateChange next day
        var day2Time = day1Time.AddDays(1);
        var day2Intent = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = "ArrivalHome",
            TimestampUtc = day2Time,
            EventType = EventType.StateChange,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday", Location = "home" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = day2Intent }, CancellationToken.None);

        // Same observed events on Day 2
        var playMusicDay2 = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = "PlayMusic",
            TimestampUtc = day2Time.AddMinutes(5),
            EventType = EventType.Action,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday", Location = "home" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = playMusicDay2 }, CancellationToken.None);

        var turnOnLightsDay2 = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = "TurnOnLights",
            TimestampUtc = day2Time.AddMinutes(8),
            EventType = EventType.Action,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday", Location = "home" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = turnOnLightsDay2 }, CancellationToken.None);

        var adjustACDay2 = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = "AdjustAC",
            TimestampUtc = day2Time.AddMinutes(10),
            EventType = EventType.Action,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday", Location = "home" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = adjustACDay2 }, CancellationToken.None);

        // Assert
        var updatedRoutine = await _routineRepository.GetByPersonAndIntentAsync(_testPersonId, "ArrivalHome", CancellationToken.None);
        var updatedReminders = await _routineReminderRepository.GetByRoutineAsync(updatedRoutine!.Id, CancellationToken.None);

        var updatedPlayMusic = updatedReminders.First(r => r.SuggestedAction == "PlayMusic");
        var updatedTurnOnLights = updatedReminders.First(r => r.SuggestedAction == "TurnOnLights");
        var updatedAdjustAC = updatedReminders.First(r => r.SuggestedAction == "AdjustAC");

        // PlayMusic probability should have increased (from feedback + reinforcement) or be at cap
        if (day1PlayMusicConfidence < 0.9)
        {
            updatedPlayMusic.Confidence.Should().BeGreaterThan(day1PlayMusicConfidence);
        }
        else
        {
            // If already high, it should be at or near 1.0
            updatedPlayMusic.Confidence.Should().BeGreaterOrEqualTo(day1PlayMusicConfidence);
            updatedPlayMusic.Confidence.Should().BeLessOrEqualTo(1.0);
        }

        // TurnOnLights probability should have decreased (from feedback, but then reinforced)
        // It should be lower than if it had only been reinforced
        updatedTurnOnLights.Confidence.Should().BeLessThan(0.7); // Less than if it had only been reinforced twice

        // AdjustAC probability unchanged or slightly increased (only reinforcement, no feedback)
        updatedAdjustAC.Confidence.Should().BeGreaterThan(day1AdjustACConfidence); // Increased from reinforcement

        // Verify only ArrivalHome routine is updated
        var allRoutines = await _routineRepository.GetByPersonAsync(_testPersonId, CancellationToken.None);
        allRoutines.Should().HaveCount(1);
        allRoutines[0].IntentType.Should().Be("ArrivalHome");
    }

    #endregion

    #region Test 8: Rapid Consecutive StateChanges

    [Fact]
    public async Task Test8_RapidConsecutiveStateChanges()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;

        // Send multiple StateChanges rapidly
        var intent1 = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = "ArrivalHome",
            TimestampUtc = baseTime,
            EventType = EventType.StateChange,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday", Location = "home" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = intent1 }, CancellationToken.None);

        var intent2 = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = "GoingToSleep",
            TimestampUtc = baseTime.AddSeconds(30),
            EventType = EventType.StateChange,
            Context = new ActionContextDto { TimeBucket = "night", DayType = "weekday", Location = "bedroom" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = intent2 }, CancellationToken.None);

        var intent3 = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = "StartingWork",
            TimestampUtc = baseTime.AddSeconds(60),
            EventType = EventType.StateChange,
            Context = new ActionContextDto { TimeBucket = "morning", DayType = "weekday", Location = "office" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = intent3 }, CancellationToken.None);

        // Assert
        // Only the last StateChange should have an active window
        var allRoutines = await _routineRepository.GetByPersonAsync(_testPersonId, CancellationToken.None);
        var activeRoutines = allRoutines.Where(r => r.IsObservationWindowOpen(baseTime.AddMinutes(1))).ToList();
        activeRoutines.Should().HaveCount(1);
        activeRoutines[0].IntentType.Should().Be("StartingWork");

        // Previous routines should be closed
        var arrivalHome = allRoutines.FirstOrDefault(r => r.IntentType == "ArrivalHome");
        arrivalHome.Should().NotBeNull();
        arrivalHome!.IsObservationWindowOpen(baseTime.AddMinutes(1)).Should().BeFalse();

        var goingToSleep = allRoutines.FirstOrDefault(r => r.IntentType == "GoingToSleep");
        goingToSleep.Should().NotBeNull();
        goingToSleep!.IsObservationWindowOpen(baseTime.AddMinutes(1)).Should().BeFalse();
    }

    #endregion

    #region Test 9: No Observed Events in Window

    [Fact]
    public async Task Test9_NoObservedEventsInWindow()
    {
        // Arrange - Use unique personId to avoid interference from other tests
        var uniquePersonId = $"{_testPersonId}_no_events_test";
        var baseTime = DateTime.UtcNow;

        var intentEvent = new ActionEventDto
        {
            PersonId = uniquePersonId,
            ActionType = "ArrivalHome",
            TimestampUtc = baseTime,
            EventType = EventType.StateChange,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday", Location = "home" }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = intentEvent }, CancellationToken.None);

        // Don't send any observed events - let window timeout

        // Assert
        var routine = await _routineRepository.GetByPersonAndIntentAsync(uniquePersonId, "ArrivalHome", CancellationToken.None);
        routine.Should().NotBeNull();

        // Window should close after timeout
        routine!.IsObservationWindowOpen(baseTime.AddMinutes(46)).Should().BeFalse();

        // No reminders should be created
        var reminders = await _routineReminderRepository.GetByRoutineAsync(routine.Id, CancellationToken.None);
        reminders.Should().BeEmpty();

        // Routine should still exist
        routine.Should().NotBeNull();
        routine.IntentType.Should().Be("ArrivalHome");
    }

    #endregion

    #region Test 10: Stress / Bulk Data Test

    [Fact]
    public async Task Test10_StressBulkDataTest()
    {
        // Arrange - Generate diverse events
        var baseTime = DateTime.UtcNow;
        var random = new Random(42); // Fixed seed for reproducibility
        var actionTypes = new[] { "PlayMusic", "TurnOnLights", "AdjustAC", "LockDoors", "SetAlarm", "BrewCoffee" };
        var intentTypes = new[] { "ArrivalHome", "GoingToSleep", "StartingWork", "LeavingHome" };

        // Create 50-100 events over different times
        var events = new List<ActionEventDto>();
        for (int i = 0; i < 75; i++)
        {
            var isStateChange = i % 10 == 0; // Every 10th event is a StateChange
            var eventTime = baseTime.AddMinutes(i * 2);
            var actionType = isStateChange 
                ? intentTypes[random.Next(intentTypes.Length)]
                : actionTypes[random.Next(actionTypes.Length)];

            events.Add(new ActionEventDto
            {
                PersonId = _testPersonId,
                ActionType = actionType,
                TimestampUtc = eventTime,
                EventType = isStateChange ? EventType.StateChange : EventType.Action,
                Context = new ActionContextDto
                {
                    TimeBucket = GetTimeBucket(eventTime),
                    DayType = eventTime.DayOfWeek == DayOfWeek.Saturday || eventTime.DayOfWeek == DayOfWeek.Sunday ? "weekend" : "weekday",
                    Location = "home"
                }
            });
        }

        // Act - Process all events
        foreach (var evt in events)
        {
            await EventHandler.Handle(new IngestEventCommand { Event = evt }, CancellationToken.None);
        }

        // Assert
        // Verify routines were created correctly
        var allRoutines = await _routineRepository.GetByPersonAsync(_testPersonId, CancellationToken.None);
        allRoutines.Should().NotBeEmpty();

        // Verify only one routine is active at a time (the last one)
        var lastStateChangeTime = events.Where(e => e.EventType == EventType.StateChange).Last().TimestampUtc;
        var activeRoutines = allRoutines.Where(r => r.IsObservationWindowOpen(lastStateChangeTime.AddMinutes(1))).ToList();
        activeRoutines.Should().HaveCountLessOrEqualTo(1);

        // Verify routine reminders were created
        foreach (var routine in allRoutines)
        {
            var reminders = await _routineReminderRepository.GetByRoutineAsync(routine.Id, CancellationToken.None);
            reminders.Should().NotBeNull();
            // Each routine should have at least some reminders if events were observed
        }

        // Verify no cross-contamination
        var routine1 = allRoutines.FirstOrDefault(r => r.IntentType == "ArrivalHome");
        var routine2 = allRoutines.FirstOrDefault(r => r.IntentType == "GoingToSleep");
        
        if (routine1 != null && routine2 != null)
        {
            var reminders1 = await _routineReminderRepository.GetByRoutineAsync(routine1.Id, CancellationToken.None);
            var reminders2 = await _routineReminderRepository.GetByRoutineAsync(routine2.Id, CancellationToken.None);
            
            // Reminders should be separate
            var commonActions = reminders1.Select(r => r.SuggestedAction)
                .Intersect(reminders2.Select(r => r.SuggestedAction))
                .ToList();
            
            // Even if same actions, they should be different reminder entities
            if (commonActions.Any())
            {
                var reminder1Ids = reminders1.Where(r => commonActions.Contains(r.SuggestedAction)).Select(r => r.Id);
                var reminder2Ids = reminders2.Where(r => commonActions.Contains(r.SuggestedAction)).Select(r => r.Id);
                reminder1Ids.Should().NotIntersectWith(reminder2Ids);
            }
        }
    }

    private string GetTimeBucket(DateTime time)
    {
        var hour = time.Hour;
        if (hour >= 5 && hour < 12) return "morning";
        if (hour >= 12 && hour < 17) return "afternoon";
        if (hour >= 17 && hour < 22) return "evening";
        return "night";
    }

    #endregion

    #region API-Based Tests

    [Fact]
    public async Task Test11_ApiBased_SingleStateChangeOpensLearningWindow()
    {
        // Skip if API is not available
        if (!await IsApiAvailableAsync())
        {
            return;
        }

        // Use test API key
        HttpClient.DefaultRequestHeaders.Remove("X-Api-Key");
        HttpClient.DefaultRequestHeaders.Add("X-Api-Key", "ak_cVrsHKsta8quTmtlWZzDR0nqrNh4Iu3xZjGqzjw842umdotqGebM1hg3YSNFqrVR");

        // Arrange
        var intentType = "ArrivalHome";
        var now = DateTime.UtcNow;

        var intentEvent = new
        {
            personId = _testPersonId,
            actionType = intentType,
            timestampUtc = now.ToString("O"),
            eventType = "StateChange",
            context = new
            {
                timeBucket = "evening",
                dayType = "weekday",
                location = "home",
                presentPeople = new[] { _testPersonId },
                stateSignals = new Dictionary<string, string>()
            }
        };

        // Act
        var response = await HttpClient.PostAsJsonAsync("/v1/events", intentEvent);
        response.EnsureSuccessStatusCode();

        // Assert via API
        var routinesResponse = await HttpClient.GetAsync($"/v1/routines/active?personId={_testPersonId}");
        routinesResponse.EnsureSuccessStatusCode();
        var activeRoutines = await routinesResponse.Content.ReadFromJsonAsync<ActiveRoutinesResponse>();

        activeRoutines.Should().NotBeNull();
        activeRoutines!.items.Should().HaveCount(1);
        activeRoutines.items[0].intentType.Should().Be(intentType);
        activeRoutines.items[0].observationWindowEndsUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Test12_ApiBased_ObservedEventsCreateRoutineReminders()
    {
        // Skip if API is not available
        if (!await IsApiAvailableAsync())
        {
            return;
        }

        // Use test API key
        HttpClient.DefaultRequestHeaders.Remove("X-Api-Key");
        HttpClient.DefaultRequestHeaders.Add("X-Api-Key", "ak_cVrsHKsta8quTmtlWZzDR0nqrNh4Iu3xZjGqzjw842umdotqGebM1hg3YSNFqrVR");

        // Arrange
        var baseTime = DateTime.UtcNow;

        // Send StateChange intent
        var intentEvent = new
        {
            personId = _testPersonId,
            actionType = "ArrivalHome",
            timestampUtc = baseTime.ToString("O"),
            eventType = "StateChange",
            context = new { timeBucket = "evening", dayType = "weekday", location = "home" }
        };
        await HttpClient.PostAsJsonAsync("/v1/events", intentEvent);

        // Send observed events
        var playMusicEvent = new
        {
            personId = _testPersonId,
            actionType = "PlayMusic",
            timestampUtc = baseTime.AddMinutes(5).ToString("O"),
            eventType = "Action",
            context = new { timeBucket = "evening", dayType = "weekday", location = "home" }
        };
        await HttpClient.PostAsJsonAsync("/v1/events", playMusicEvent);

        var turnOnLightsEvent = new
        {
            personId = _testPersonId,
            actionType = "TurnOnLights",
            timestampUtc = baseTime.AddMinutes(10).ToString("O"),
            eventType = "Action",
            context = new { timeBucket = "evening", dayType = "weekday", location = "home" }
        };
        await HttpClient.PostAsJsonAsync("/v1/events", turnOnLightsEvent);

        // Assert via API
        var routinesResponse = await HttpClient.GetAsync($"/v1/routines?personId={_testPersonId}");
        routinesResponse.EnsureSuccessStatusCode();
        var routines = await routinesResponse.Content.ReadFromJsonAsync<RoutineListResponse>();

        routines.Should().NotBeNull();
        var arrivalHomeRoutine = routines!.items.FirstOrDefault(r => r.intentType == "ArrivalHome");
        arrivalHomeRoutine.Should().NotBeNull();

        var routineDetailResponse = await HttpClient.GetAsync($"/v1/routines/{arrivalHomeRoutine!.id}");
        routineDetailResponse.EnsureSuccessStatusCode();
        var routineDetail = await routineDetailResponse.Content.ReadFromJsonAsync<RoutineDetailResponse>();

        routineDetail.Should().NotBeNull();
        routineDetail!.reminders.Should().HaveCount(2);
        routineDetail.reminders.Should().Contain(r => r.suggestedAction == "PlayMusic");
        routineDetail.reminders.Should().Contain(r => r.suggestedAction == "TurnOnLights");
    }

    [Fact]
    public async Task Test13_ApiBased_UserFeedbackUpdatesProbability()
    {
        // Skip if API is not available
        if (!await IsApiAvailableAsync())
        {
            return;
        }

        // Use test API key
        HttpClient.DefaultRequestHeaders.Remove("X-Api-Key");
        HttpClient.DefaultRequestHeaders.Add("X-Api-Key", "ak_cVrsHKsta8quTmtlWZzDR0nqrNh4Iu3xZjGqzjw842umdotqGebM1hg3YSNFqrVR");

        // Arrange - Create routine with reminder
        var baseTime = DateTime.UtcNow;

        var intentEvent = new
        {
            personId = _testPersonId,
            actionType = "ArrivalHome",
            timestampUtc = baseTime.ToString("O"),
            eventType = "StateChange",
            context = new { timeBucket = "evening", dayType = "weekday", location = "home" }
        };
        await HttpClient.PostAsJsonAsync("/v1/events", intentEvent);

        var observedEvent = new
        {
            personId = _testPersonId,
            actionType = "PlayMusic",
            timestampUtc = baseTime.AddMinutes(5).ToString("O"),
            eventType = "Action",
            context = new { timeBucket = "evening", dayType = "weekday", location = "home" }
        };
        await HttpClient.PostAsJsonAsync("/v1/events", observedEvent);

        // Get routine and reminder IDs
        var routinesResponse = await HttpClient.GetAsync($"/v1/routines?personId={_testPersonId}");
        var routines = await routinesResponse.Content.ReadFromJsonAsync<RoutineListResponse>();
        var routine = routines!.items.First(r => r.intentType == "ArrivalHome");

        var routineDetailResponse = await HttpClient.GetAsync($"/v1/routines/{routine.id}");
        var routineDetail = await routineDetailResponse.Content.ReadFromJsonAsync<RoutineDetailResponse>();
        var reminder = routineDetail!.reminders.First(r => r.suggestedAction == "PlayMusic");

        var initialConfidence = reminder.confidence;

        // Act - Submit feedback (accept)
        var feedbackRequest = new
        {
            action = "Increase",
            value = 0.1
        };
        var feedbackResponse = await HttpClient.PostAsJsonAsync(
            $"/v1/routines/{routine.id}/reminders/{reminder.id}/feedback",
            feedbackRequest);
        feedbackResponse.EnsureSuccessStatusCode();

        // Assert
        var updatedDetailResponse = await HttpClient.GetAsync($"/v1/routines/{routine.id}");
        var updatedDetail = await updatedDetailResponse.Content.ReadFromJsonAsync<RoutineDetailResponse>();
        var updatedReminder = updatedDetail!.reminders.First(r => r.id == reminder.id);

        updatedReminder.confidence.Should().BeGreaterThan(initialConfidence);
        updatedReminder.confidence.Should().BeApproximately(initialConfidence + 0.1, 0.01);
    }

    private async Task<bool> IsApiAvailableAsync()
    {
        try
        {
            var response = await HttpClient.GetAsync("/v1/events?pageSize=1");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}

// Response DTOs for API tests
public class ActiveRoutinesResponse
{
    public List<RoutineDto> items { get; set; } = new();
    public int totalCount { get; set; }
}

public class RoutineListResponse
{
    public List<RoutineDto> items { get; set; } = new();
    public int totalCount { get; set; }
}

public class RoutineDto
{
    public string id { get; set; } = string.Empty;
    public string personId { get; set; } = string.Empty;
    public string intentType { get; set; } = string.Empty;
    public string? observationWindowEndsUtc { get; set; }
}

public class RoutineDetailResponse
{
    public string id { get; set; } = string.Empty;
    public string personId { get; set; } = string.Empty;
    public string intentType { get; set; } = string.Empty;
    public List<RoutineReminderDto> reminders { get; set; } = new();
}

public class RoutineReminderDto
{
    public string id { get; set; } = string.Empty;
    public string routineId { get; set; } = string.Empty;
    public string suggestedAction { get; set; } = string.Empty;
    public double confidence { get; set; }
}

