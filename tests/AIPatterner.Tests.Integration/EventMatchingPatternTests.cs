// Comprehensive tests for event creation and reminder matching patterns
namespace AIPatterner.Tests.Integration;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class EventMatchingPatternTests : RealDatabaseTestBase
{
    [Fact]
    public async Task CreateEvent_WithCustomData_ShouldCreateReminderWithCustomData()
    {
        // Arrange
        var personId = "test_user_customdata";
        var actionType = "play_music";
        var timestamp = DateTime.UtcNow;
        var customData = new Dictionary<string, string>
        {
            { "playlist", "chill_vibes" },
            { "volume", "75" }
        };

        var eventDto = new ActionEventDto
        {
            PersonId = personId,
            ActionType = actionType,
            TimestampUtc = timestamp,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "living_room"
            },
            ProbabilityValue = 0.1,
            ProbabilityAction = ProbabilityAction.Increase,
            CustomData = customData
        };

        // Act
        var command = new IngestEventCommand { Event = eventDto };
        var response = await EventHandler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.RelatedReminderId);
        Assert.True(response.RelatedReminderId.HasValue);

        var reminder = await ReminderRepository.GetByIdAsync(response.RelatedReminderId!.Value, CancellationToken.None);
        Assert.NotNull(reminder);
        Assert.NotNull(reminder.CustomData);
        Assert.Equal(customData["playlist"], reminder.CustomData["playlist"]);
        Assert.Equal(customData["volume"], reminder.CustomData["volume"]);
    }

    [Fact]
    public async Task CreateMultipleEvents_WithSameTimeAndAction_ShouldMatchExistingReminder()
    {
        // Arrange
        var personId = "test_user_match";
        var actionType = "play_music";
        var baseTime = DateTime.UtcNow.Date.AddHours(14); // 2 PM

        // Create first event
        var firstEvent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = actionType,
            TimestampUtc = baseTime,
            Context = new ActionContextDto
            {
                TimeBucket = "afternoon",
                DayType = "weekday"
            },
            ProbabilityValue = 0.1,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var firstCommand = new IngestEventCommand { Event = firstEvent };
        var firstResponse = await EventHandler.Handle(firstCommand, CancellationToken.None);
        var firstReminderId = firstResponse.RelatedReminderId;

        // Act - Create second event 5 minutes later (should match)
        var secondEvent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = actionType,
            TimestampUtc = baseTime.AddMinutes(5),
            Context = new ActionContextDto
            {
                TimeBucket = "afternoon",
                DayType = "weekday"
            },
            ProbabilityValue = 0.1,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var secondCommand = new IngestEventCommand { Event = secondEvent };
        var secondResponse = await EventHandler.Handle(secondCommand, CancellationToken.None);

        // Assert
        Assert.NotNull(secondResponse.RelatedReminderId);
        Assert.Equal(firstReminderId, secondResponse.RelatedReminderId); // Should match existing reminder

        var reminder = await ReminderRepository.GetByIdAsync(secondResponse.RelatedReminderId.Value, CancellationToken.None);
        Assert.NotNull(reminder);
        Assert.True(reminder.Confidence > 0.5); // Confidence should be increased
    }

    [Fact]
    public async Task CreateEvents_WithDifferentActions_ShouldCreateSeparateReminders()
    {
        // Arrange
        var personId = "test_user_different";
        var baseTime = DateTime.UtcNow.Date.AddHours(15);

        // Create event for action1
        var event1 = new ActionEventDto
        {
            PersonId = personId,
            ActionType = "play_music",
            TimestampUtc = baseTime,
            Context = new ActionContextDto
            {
                TimeBucket = "afternoon",
                DayType = "weekday"
            },
            ProbabilityValue = 0.1,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var command1 = new IngestEventCommand { Event = event1 };
        var response1 = await EventHandler.Handle(command1, CancellationToken.None);

        // Act - Create event for different action
        var event2 = new ActionEventDto
        {
            PersonId = personId,
            ActionType = "turn_on_lights",
            TimestampUtc = baseTime,
            Context = new ActionContextDto
            {
                TimeBucket = "afternoon",
                DayType = "weekday"
            },
            ProbabilityValue = 0.1,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var command2 = new IngestEventCommand { Event = event2 };
        var response2 = await EventHandler.Handle(command2, CancellationToken.None);

        // Assert
        Assert.NotNull(response1.RelatedReminderId);
        Assert.NotNull(response2.RelatedReminderId);
        Assert.NotEqual(response1.RelatedReminderId, response2.RelatedReminderId); // Should be different reminders
    }

    [Fact]
    public async Task CreateEvents_WithTimeOffsetWithin30Minutes_ShouldMatch()
    {
        // Arrange
        var personId = "test_user_timeoffset";
        var actionType = "play_music";
        var baseTime = DateTime.UtcNow.Date.AddHours(10);

        // Create first event
        var firstEvent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = actionType,
            TimestampUtc = baseTime,
            Context = new ActionContextDto
            {
                TimeBucket = "morning",
                DayType = "weekday"
            },
            ProbabilityValue = 0.1,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var firstCommand = new IngestEventCommand { Event = firstEvent };
        var firstResponse = await EventHandler.Handle(firstCommand, CancellationToken.None);
        var firstReminderId = firstResponse.RelatedReminderId;

        // Act - Create events with various time offsets
        var offsets = new[] { 5, 10, 15, 20, 25, 30 }; // minutes

        foreach (var offset in offsets)
        {
            var eventWithOffset = new ActionEventDto
            {
                PersonId = personId,
                ActionType = actionType,
                TimestampUtc = baseTime.AddMinutes(offset),
                Context = new ActionContextDto
                {
                    TimeBucket = "morning",
                    DayType = "weekday"
                },
                ProbabilityValue = 0.1,
                ProbabilityAction = ProbabilityAction.Increase
            };

            var command = new IngestEventCommand { Event = eventWithOffset };
            var response = await EventHandler.Handle(command, CancellationToken.None);

            // Assert - All should match the first reminder
            Assert.NotNull(response.RelatedReminderId);
            Assert.Equal(firstReminderId, response.RelatedReminderId);
        }
    }

    [Fact]
    public async Task CreateEvents_WithTimeOffsetBeyond30Minutes_ShouldCreateNewReminder()
    {
        // Arrange
        var personId = "test_user_beyondoffset";
        var actionType = "play_music";
        var baseTime = DateTime.UtcNow.Date.AddHours(10);

        // Create first event
        var firstEvent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = actionType,
            TimestampUtc = baseTime,
            Context = new ActionContextDto
            {
                TimeBucket = "morning",
                DayType = "weekday"
            },
            ProbabilityValue = 0.1,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var firstCommand = new IngestEventCommand { Event = firstEvent };
        var firstResponse = await EventHandler.Handle(firstCommand, CancellationToken.None);
        var firstReminderId = firstResponse.RelatedReminderId;

        // Act - Create event 35 minutes later (beyond 30 minute threshold)
        var secondEvent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = actionType,
            TimestampUtc = baseTime.AddMinutes(35),
            Context = new ActionContextDto
            {
                TimeBucket = "morning",
                DayType = "weekday"
            },
            ProbabilityValue = 0.1,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var secondCommand = new IngestEventCommand { Event = secondEvent };
        var secondResponse = await EventHandler.Handle(secondCommand, CancellationToken.None);

        // Assert
        Assert.NotNull(secondResponse.RelatedReminderId);
        Assert.NotEqual(firstReminderId, secondResponse.RelatedReminderId); // Should create new reminder
    }

    [Fact]
    public async Task CreateEvents_WithUpdatedCustomData_ShouldUpdateReminderCustomData()
    {
        // Arrange
        var personId = "test_user_updatecustom";
        var actionType = "play_music";
        var baseTime = DateTime.UtcNow;

        // Create first event with custom data
        var firstEvent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = actionType,
            TimestampUtc = baseTime,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday"
            },
            ProbabilityValue = 0.1,
            ProbabilityAction = ProbabilityAction.Increase,
            CustomData = new Dictionary<string, string> { { "playlist", "chill" } }
        };

        var firstCommand = new IngestEventCommand { Event = firstEvent };
        var firstResponse = await EventHandler.Handle(firstCommand, CancellationToken.None);
        var reminderId = firstResponse.RelatedReminderId;

        // Act - Create second event with updated custom data
        var secondEvent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = actionType,
            TimestampUtc = baseTime.AddMinutes(5),
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday"
            },
            ProbabilityValue = 0.1,
            ProbabilityAction = ProbabilityAction.Increase,
            CustomData = new Dictionary<string, string> 
            { 
                { "playlist", "upbeat" },
                { "volume", "80" }
            }
        };

        var secondCommand = new IngestEventCommand { Event = secondEvent };
        var secondResponse = await EventHandler.Handle(secondCommand, CancellationToken.None);

        // Assert
        Assert.Equal(reminderId, secondResponse.RelatedReminderId); // Should match
        Assert.True(reminderId.HasValue);
        var reminder = await ReminderRepository.GetByIdAsync(reminderId!.Value, CancellationToken.None);
        Assert.NotNull(reminder.CustomData);
        Assert.Equal("upbeat", reminder.CustomData["playlist"]);
        Assert.Equal("80", reminder.CustomData["volume"]);
    }

    [Fact]
    public async Task CreateEvents_OccurrenceShouldNotHaveOffset()
    {
        // Arrange
        var personId = "test_user_occurrence";
        var actionType = "play_music";
        var timestamp = DateTime.UtcNow.Date.AddHours(14).AddMinutes(30); // 2:30 PM

        var eventDto = new ActionEventDto
        {
            PersonId = personId,
            ActionType = actionType,
            TimestampUtc = timestamp,
            Context = new ActionContextDto
            {
                TimeBucket = "afternoon",
                DayType = "weekday"
            },
            ProbabilityValue = 0.1,
            ProbabilityAction = ProbabilityAction.Increase
        };

        // Act
        var command = new IngestEventCommand { Event = eventDto };
        var response = await EventHandler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(response.RelatedReminderId);
        Assert.True(response.RelatedReminderId.HasValue);
        var reminder = await ReminderRepository.GetByIdAsync(response.RelatedReminderId!.Value, CancellationToken.None);
        Assert.NotNull(reminder.Occurrence);
        Assert.DoesNotContain("(+", reminder.Occurrence); // Should not contain offset
        Assert.Contains("14:30", reminder.Occurrence); // Should contain the time
    }

    [Fact]
    public async Task CreateMultipleEvents_VerifyMatchingPattern()
    {
        // Arrange
        var personId = "test_user_pattern";
        var actionType = "play_music";
        var baseTime = DateTime.UtcNow.Date.AddHours(9);

        var remindersCreated = new List<Guid>();

        // Create 10 events with varying time offsets
        for (int i = 0; i < 10; i++)
        {
            var eventDto = new ActionEventDto
            {
                PersonId = personId,
                ActionType = actionType,
                TimestampUtc = baseTime.AddMinutes(i * 3), // 3 minute intervals
                Context = new ActionContextDto
                {
                    TimeBucket = "morning",
                    DayType = "weekday"
                },
                ProbabilityValue = 0.1,
                ProbabilityAction = ProbabilityAction.Increase
            };

            var command = new IngestEventCommand { Event = eventDto };
            var response = await EventHandler.Handle(command, CancellationToken.None);
            
            if (response.RelatedReminderId.HasValue)
            {
                if (!remindersCreated.Contains(response.RelatedReminderId.Value))
                {
                    remindersCreated.Add(response.RelatedReminderId.Value);
                }
            }
        }

        // Assert - Events within 30 minutes should match, creating fewer reminders
        // Events at 0, 3, 6, 9, 12, 15, 18, 21, 24, 27 minutes
        // All should match the first reminder (within 30 minute window)
        Assert.True(remindersCreated.Count <= 2, 
            $"Expected at most 2 reminders, but got {remindersCreated.Count}. " +
            $"This suggests events beyond 30 minutes created new reminders.");
    }
}

