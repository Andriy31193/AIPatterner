// Integration tests for Intent-Anchored Learned Routines
namespace AIPatterner.Tests.Integration;

using Xunit;
using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Application.Handlers;
using AIPatterner.Application.Services;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using AIPatterner.Infrastructure.Persistence.Repositories;
using AIPatterner.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class RoutineLearningTests : RealDatabaseTestBase
{
    private readonly IRoutineRepository _routineRepository;
    private readonly IRoutineReminderRepository _routineReminderRepository;
    private readonly IRoutineLearningService _routineLearningService;

    public RoutineLearningTests()
    {
        _routineRepository = new RoutineRepository(Context);
        _routineReminderRepository = new RoutineReminderRepository(Context);

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Routine:ObservationWindowMinutes", "45" },
                { "Routine:DefaultRoutineProbability", "0.5" },
                { "Routine:ProbabilityIncreaseStep", "0.1" },
                { "Routine:ProbabilityDecreaseStep", "0.1" },
                { "Routine:AutoExecuteThreshold", "0.7" }
            })
            .Build();

        _routineLearningService = new RoutineLearningService(
            _routineRepository,
            _routineReminderRepository,
            EventRepository,
            config,
            loggerFactory.CreateLogger<RoutineLearningService>());
    }

    [Fact]
    public async Task Scenario1_FirstIntentCreatesEmptyRoutine()
    {
        // Arrange
        var personId = "routine_test_user_1";
        var intentType = "ArrivalHome";
        var now = DateTime.UtcNow;

        var intentEvent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = intentType,
            TimestampUtc = now,
            EventType = EventType.StateChange,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>()
            }
        };

        // Act
        var command = new IngestEventCommand { Event = intentEvent };
        await EventHandler.Handle(command, CancellationToken.None);

        // Assert
        var routine = await _routineRepository.GetByPersonAndIntentAsync(personId, intentType, CancellationToken.None);
        Assert.NotNull(routine);
        Assert.Equal(personId, routine.PersonId);
        Assert.Equal(intentType, routine.IntentType);
        Assert.NotNull(routine.LastIntentOccurredAtUtc);

        var reminders = await _routineReminderRepository.GetByRoutineAsync(routine.Id, CancellationToken.None);
        Assert.Empty(reminders);

        // Verify no general reminders were created
        var generalReminders = await ReminderRepository.GetFilteredAsync(
            personId, null, null, null, 1, 100, CancellationToken.None);
        Assert.Empty(generalReminders);
    }

    [Fact]
    public async Task Scenario2_ActionsAfterStateChangeCreateRoutineReminders()
    {
        // Arrange
        var personId = "routine_test_user_2";
        var intentType = "ArrivalHome";
        var now = DateTime.UtcNow;

        // Step 1: Send StateChange intent
        var intentEvent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = intentType,
            TimestampUtc = now,
            EventType = EventType.StateChange,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>()
            }
        };

        await EventHandler.Handle(new IngestEventCommand { Event = intentEvent }, CancellationToken.None);

        // Step 2: Within observation window, send observed actions
        var playMusicEvent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = "PlayMusic",
            TimestampUtc = now.AddMinutes(2),
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>()
            }
        };

        var turnOnLightsEvent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = "TurnOnLights",
            TimestampUtc = now.AddMinutes(5),
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>()
            }
        };

        await EventHandler.Handle(new IngestEventCommand { Event = playMusicEvent }, CancellationToken.None);
        await EventHandler.Handle(new IngestEventCommand { Event = turnOnLightsEvent }, CancellationToken.None);

        // Assert
        var routine = await _routineRepository.GetByPersonAndIntentAsync(personId, intentType, CancellationToken.None);
        Assert.NotNull(routine);

        var reminders = await _routineReminderRepository.GetByRoutineAsync(routine.Id, CancellationToken.None);
        Assert.Equal(2, reminders.Count);

        var playMusicReminder = reminders.FirstOrDefault(r => r.SuggestedAction == "PlayMusic");
        Assert.NotNull(playMusicReminder);
        Assert.Equal(0.5, playMusicReminder.Confidence); // Default probability

        var turnOnLightsReminder = reminders.FirstOrDefault(r => r.SuggestedAction == "TurnOnLights");
        Assert.NotNull(turnOnLightsReminder);
        Assert.Equal(0.5, turnOnLightsReminder.Confidence); // Default probability
    }

    [Fact]
    public async Task Scenario3_RepeatedIntentIncreasesProbability()
    {
        // Arrange
        var personId = "routine_test_user_3";
        var intentType = "ArrivalHome";
        var now = DateTime.UtcNow;

        // First occurrence
        var intentEvent1 = new ActionEventDto
        {
            PersonId = personId,
            ActionType = intentType,
            TimestampUtc = now,
            EventType = EventType.StateChange,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>()
            }
        };

        await EventHandler.Handle(new IngestEventCommand { Event = intentEvent1 }, CancellationToken.None);

        var playMusicEvent1 = new ActionEventDto
        {
            PersonId = personId,
            ActionType = "PlayMusic",
            TimestampUtc = now.AddMinutes(2),
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>()
            }
        };

        await EventHandler.Handle(new IngestEventCommand { Event = playMusicEvent1 }, CancellationToken.None);

        // Second occurrence (next day)
        var intentEvent2 = new ActionEventDto
        {
            PersonId = personId,
            ActionType = intentType,
            TimestampUtc = now.AddDays(1),
            EventType = EventType.StateChange,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>()
            }
        };

        await EventHandler.Handle(new IngestEventCommand { Event = intentEvent2 }, CancellationToken.None);

        var playMusicEvent2 = new ActionEventDto
        {
            PersonId = personId,
            ActionType = "PlayMusic",
            TimestampUtc = now.AddDays(1).AddMinutes(2),
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>()
            }
        };

        await EventHandler.Handle(new IngestEventCommand { Event = playMusicEvent2 }, CancellationToken.None);

        // Assert
        var routine = await _routineRepository.GetByPersonAndIntentAsync(personId, intentType, CancellationToken.None);
        Assert.NotNull(routine);

        var reminders = await _routineReminderRepository.GetByRoutineAsync(routine.Id, CancellationToken.None);
        Assert.Single(reminders); // Same reminder reused, no duplicates

        var playMusicReminder = reminders.First();
        Assert.Equal("PlayMusic", playMusicReminder.SuggestedAction);
        Assert.True(playMusicReminder.Confidence > 0.5); // Probability increased
        Assert.Equal(2, playMusicReminder.ObservationCount);
    }

    [Fact]
    public async Task Scenario4_IntentDoesNotAffectGeneralReminders()
    {
        // Arrange
        var personId = "routine_test_user_4";
        var intentType = "ArrivalHome";
        var now = DateTime.UtcNow;

        // Create a general reminder first
        var generalReminder = new ReminderCandidate(
            personId,
            "PlayMusic",
            now,
            ReminderStyle.Suggest,
            null,
            0.6,
            null,
            null,
            null);

        await ReminderRepository.AddAsync(generalReminder, CancellationToken.None);

        // Send StateChange event
        var intentEvent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = intentType,
            TimestampUtc = now,
            EventType = EventType.StateChange,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>()
            }
        };

        // Act
        await EventHandler.Handle(new IngestEventCommand { Event = intentEvent }, CancellationToken.None);

        // Assert
        var generalReminders = await ReminderRepository.GetFilteredAsync(
            personId, null, null, null, 1, 100, CancellationToken.None);
        Assert.Single(generalReminders);

        var updatedGeneralReminder = await ReminderRepository.GetByIdAsync(generalReminder.Id, CancellationToken.None);
        Assert.NotNull(updatedGeneralReminder);
        Assert.Equal(0.6, updatedGeneralReminder.Confidence); // Unchanged
    }

    [Fact]
    public async Task Scenario5_ExecutionBehaviorRespectsSafety()
    {
        // Arrange
        var personId = "routine_test_user_5";
        var intentType = "ArrivalHome";
        var now = DateTime.UtcNow;

        // Create routine with high-probability reminder
        var intentEvent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = intentType,
            TimestampUtc = now,
            EventType = EventType.StateChange,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>()
            }
        };

        await EventHandler.Handle(new IngestEventCommand { Event = intentEvent }, CancellationToken.None);

        // Create high-probability routine reminder manually
        var routine = await _routineRepository.GetByPersonAndIntentAsync(personId, intentType, CancellationToken.None);
        var highProbReminder = new RoutineReminder(
            routine!.Id,
            personId,
            "PlayMusic",
            0.85, // High probability
            null);

        await _routineReminderRepository.AddAsync(highProbReminder, CancellationToken.None);

        // Act - Get reminders for intent
        var reminders = await _routineLearningService.GetRemindersForIntentAsync(
            personId, intentType, CancellationToken.None);

        // Assert
        Assert.Single(reminders);
        var reminder = reminders.First();
        Assert.True(reminder.Confidence >= 0.7); // High probability
        // System should ask user (not auto-execute) - this is handled by evaluation service
    }

    [Fact]
    public async Task Scenario6_NotTodayReducesProbability()
    {
        // Arrange
        var personId = "routine_test_user_6";
        var intentType = "ArrivalHome";
        var now = DateTime.UtcNow;

        // Create routine with reminder
        var intentEvent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = intentType,
            TimestampUtc = now,
            EventType = EventType.StateChange,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>()
            }
        };

        await EventHandler.Handle(new IngestEventCommand { Event = intentEvent }, CancellationToken.None);

        var playMusicEvent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = "PlayMusic",
            TimestampUtc = now.AddMinutes(2),
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>()
            }
        };

        await EventHandler.Handle(new IngestEventCommand { Event = playMusicEvent }, CancellationToken.None);

        var routine = await _routineRepository.GetByPersonAndIntentAsync(personId, intentType, CancellationToken.None);
        var reminders = await _routineReminderRepository.GetByRoutineAsync(routine!.Id, CancellationToken.None);
        var reminder = reminders.First();
        var initialConfidence = reminder.Confidence;

        // Act - Simulate "not today" feedback
        await _routineLearningService.HandleFeedbackAsync(
            reminder.Id,
            ProbabilityAction.Decrease,
            0.1,
            CancellationToken.None);

        // Assert
        var updatedReminder = await _routineReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        Assert.NotNull(updatedReminder);
        Assert.True(updatedReminder.Confidence < initialConfidence);
    }

    [Fact]
    public async Task Scenario7_DifferentIntentCreatesSeparateRoutine()
    {
        // Arrange
        var personId = "routine_test_user_7";
        var now = DateTime.UtcNow;

        // Create first routine (ArrivalHome)
        var arrivalIntent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = "ArrivalHome",
            TimestampUtc = now,
            EventType = EventType.StateChange,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>()
            }
        };

        await EventHandler.Handle(new IngestEventCommand { Event = arrivalIntent }, CancellationToken.None);

        var playMusicEvent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = "PlayMusic",
            TimestampUtc = now.AddMinutes(2),
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>()
            }
        };

        await EventHandler.Handle(new IngestEventCommand { Event = playMusicEvent }, CancellationToken.None);

        // Create second routine (GoingToSleep)
        var sleepIntent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = "GoingToSleep",
            TimestampUtc = now.AddHours(3),
            EventType = EventType.StateChange,
            Context = new ActionContextDto
            {
                TimeBucket = "night",
                DayType = "weekday",
                Location = "bedroom",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>()
            }
        };

        await EventHandler.Handle(new IngestEventCommand { Event = sleepIntent }, CancellationToken.None);

        var turnOffLightsEvent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = "TurnOffLights",
            TimestampUtc = now.AddHours(3).AddMinutes(2),
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = "night",
                DayType = "weekday",
                Location = "bedroom",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>()
            }
        };

        await EventHandler.Handle(new IngestEventCommand { Event = turnOffLightsEvent }, CancellationToken.None);

        // Assert
        var arrivalRoutine = await _routineRepository.GetByPersonAndIntentAsync(personId, "ArrivalHome", CancellationToken.None);
        var sleepRoutine = await _routineRepository.GetByPersonAndIntentAsync(personId, "GoingToSleep", CancellationToken.None);

        Assert.NotNull(arrivalRoutine);
        Assert.NotNull(sleepRoutine);
        Assert.NotEqual(arrivalRoutine.Id, sleepRoutine.Id);

        var arrivalReminders = await _routineReminderRepository.GetByRoutineAsync(arrivalRoutine.Id, CancellationToken.None);
        var sleepReminders = await _routineReminderRepository.GetByRoutineAsync(sleepRoutine.Id, CancellationToken.None);

        Assert.Single(arrivalReminders);
        Assert.Equal("PlayMusic", arrivalReminders.First().SuggestedAction);

        Assert.Single(sleepReminders);
        Assert.Equal("TurnOffLights", sleepReminders.First().SuggestedAction);

        // Verify no cross-learning
        Assert.DoesNotContain(arrivalReminders, r => r.SuggestedAction == "TurnOffLights");
        Assert.DoesNotContain(sleepReminders, r => r.SuggestedAction == "PlayMusic");
    }
}

