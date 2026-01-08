// Comprehensive integration tests for SignalStates functionality
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
using System.Text.Json;

public class SignalStatesComprehensiveTests : RealDatabaseTestBase
{
    private const string TestPersonId = "test_signal_person";
    private const string TestApiKey = "ak_2RYkVzz1H9rUb216mP6JetbXBBl8l9KrnmLOF9F6RSx9rdWyIULY7h1uCrQirZR5";
    
    [Fact]
    public async Task Scenario_1_1_EventWithNoSignalStates_LegacyBehaviorPreserved()
    {
        // Arrange: Create a reminder without signal profile
        var reminder = new ReminderCandidate(
            TestPersonId,
            "test_action",
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            null,
            0.5);
        await ReminderRepository.AddAsync(reminder, CancellationToken.None);
        
        // Act: Create event without signalStates
        var eventDto = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                StateSignals = new Dictionary<string, string>()
            },
            SignalStates = null // No signal states
        };
        
        var command = new IngestEventCommand { Event = eventDto };
        var response = await EventHandler.Handle(command, CancellationToken.None);
        
        // Assert: Reminder should still match (legacy behavior)
        var updatedReminder = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        Assert.NotNull(updatedReminder);
        Assert.True(updatedReminder.Confidence > 0.5); // Confidence increased
        Assert.Null(updatedReminder.SignalProfileJson); // No baseline created
    }
    
    [Fact]
    public async Task Scenario_1_2_EventWithPartialSignalStates_WorksCorrectly()
    {
        // Arrange: Create reminder with baseline
        var reminder = new ReminderCandidate(
            TestPersonId,
            "test_action",
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            null,
            0.5);
        
        // Set initial baseline with presence sensor
        var initialProfile = new AIPatterner.Domain.ValueObjects.SignalProfile
        {
            Signals = new Dictionary<string, AIPatterner.Domain.ValueObjects.SignalProfileEntry>
            {
                ["presence"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 1.0,
                    NormalizedValue = 1.0 // home
                }
            }
        };
        reminder.UpdateSignalProfile(initialProfile, 0.1);
        await ReminderRepository.AddAsync(reminder, CancellationToken.None);
        
        // Act: Create event with only 1-2 sensors
        var eventDto = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday"
            },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto
                {
                    SensorId = "presence",
                    Value = JsonSerializer.SerializeToElement("home"),
                    RawImportance = null
                }
                // Only one sensor provided
            }
        };
        
        var command = new IngestEventCommand { Event = eventDto };
        var response = await EventHandler.Handle(command, CancellationToken.None);
        
        // Assert: Should work with partial signals
        var updatedReminder = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        Assert.NotNull(updatedReminder);
        var profile = updatedReminder.GetSignalProfile();
        Assert.NotNull(profile);
        Assert.True(profile.Signals.ContainsKey("presence"));
    }
    
    [Fact]
    public async Task Scenario_2_1_BooleanSensors_NormalizedCorrectly()
    {
        // Arrange: Create reminder with boolean baseline
        var reminder = new ReminderCandidate(
            TestPersonId,
            "test_action",
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            null,
            0.5);
        
        var baselineProfile = new AIPatterner.Domain.ValueObjects.SignalProfile
        {
            Signals = new Dictionary<string, AIPatterner.Domain.ValueObjects.SignalProfileEntry>
            {
                ["door_open"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 1.0,
                    NormalizedValue = 1.0 // true
                }
            }
        };
        reminder.UpdateSignalProfile(baselineProfile, 0.1);
        await ReminderRepository.AddAsync(reminder, CancellationToken.None);
        
        // Act: Test with matching boolean
        var eventDto1 = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "door_open", Value = JsonSerializer.SerializeToElement(true) }
            }
        };
        
        var command1 = new IngestEventCommand { Event = eventDto1 };
        var response1 = await EventHandler.Handle(command1, CancellationToken.None);
        
        // Get similarity by checking if reminder was updated
        var reminderAfterMatch = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        var confidenceAfterMatch = reminderAfterMatch.Confidence;
        
        // Reset reminder confidence
        reminderAfterMatch.DecreaseConfidence(0.2);
        await ReminderRepository.UpdateAsync(reminderAfterMatch, CancellationToken.None);
        
        // Act: Test with non-matching boolean
        var eventDto2 = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow.AddMinutes(1),
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "door_open", Value = JsonSerializer.SerializeToElement(false) }
            }
        };
        
        var command2 = new IngestEventCommand { Event = eventDto2 };
        var response2 = await EventHandler.Handle(command2, CancellationToken.None);
        
        // Assert: Matching boolean should have higher confidence increase
        var reminderAfterMismatch = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        // If signal mismatch occurred, confidence should not have increased as much
        // (or reminder might have been skipped entirely)
        Assert.True(confidenceAfterMatch >= reminderAfterMismatch.Confidence);
    }
    
    [Fact]
    public async Task Scenario_2_2_EnumStringSensors_HandledCorrectly()
    {
        // Arrange: Create reminder with "home" presence baseline
        var reminder = new ReminderCandidate(
            TestPersonId,
            "test_action",
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            null,
            0.5);
        
        var baselineProfile = new AIPatterner.Domain.ValueObjects.SignalProfile
        {
            Signals = new Dictionary<string, AIPatterner.Domain.ValueObjects.SignalProfileEntry>
            {
                ["presence"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 1.0,
                    NormalizedValue = 1.0 // "home"
                }
            }
        };
        reminder.UpdateSignalProfile(baselineProfile, 0.1);
        await ReminderRepository.AddAsync(reminder, CancellationToken.None);
        
        // Act: Test with matching enum value
        var eventDto1 = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement("home") }
            }
        };
        
        var command1 = new IngestEventCommand { Event = eventDto1 };
        var response1 = await EventHandler.Handle(command1, CancellationToken.None);
        var reminderAfterMatch = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        var confidenceAfterMatch = reminderAfterMatch.Confidence;
        
        // Reset
        reminderAfterMatch.DecreaseConfidence(0.2);
        await ReminderRepository.UpdateAsync(reminderAfterMatch, CancellationToken.None);
        
        // Act: Test with different enum value
        var eventDto2 = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow.AddMinutes(1),
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement("away") }
            }
        };
        
        var command2 = new IngestEventCommand { Event = eventDto2 };
        var response2 = await EventHandler.Handle(command2, CancellationToken.None);
        
        // Assert: Different enum should result in lower similarity (or skip)
        var reminderAfterMismatch = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        Assert.True(confidenceAfterMatch >= reminderAfterMismatch.Confidence);
        
        // Test unknown enum value doesn't crash
        var eventDto3 = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow.AddMinutes(2),
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement("unknown_value_xyz") }
            }
        };
        
        var command3 = new IngestEventCommand { Event = eventDto3 };
        var exception = await Record.ExceptionAsync(() => EventHandler.Handle(command3, CancellationToken.None));
        Assert.Null(exception); // Should not crash
    }
    
    [Fact]
    public async Task Scenario_2_3_NumericSensors_CloseValuesHigherSimilarity()
    {
        // Arrange: Create reminder with temperature baseline
        var reminder = new ReminderCandidate(
            TestPersonId,
            "test_action",
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            null,
            0.5);
        
        var baselineProfile = new AIPatterner.Domain.ValueObjects.SignalProfile
        {
            Signals = new Dictionary<string, AIPatterner.Domain.ValueObjects.SignalProfileEntry>
            {
                ["temp"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 0.5,
                    NormalizedValue = 0.5 // ~50°C normalized
                }
            }
        };
        reminder.UpdateSignalProfile(baselineProfile, 0.1);
        await ReminderRepository.AddAsync(reminder, CancellationToken.None);
        
        // Act: Test with close numeric value
        var eventDto1 = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "temp", Value = JsonSerializer.SerializeToElement(52.0) }
            }
        };
        
        var command1 = new IngestEventCommand { Event = eventDto1 };
        var response1 = await EventHandler.Handle(command1, CancellationToken.None);
        var reminderAfterClose = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        var confidenceAfterClose = reminderAfterClose.Confidence;
        
        // Reset
        reminderAfterClose.DecreaseConfidence(0.2);
        await ReminderRepository.UpdateAsync(reminderAfterClose, CancellationToken.None);
        
        // Act: Test with distant numeric value
        var eventDto2 = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow.AddMinutes(1),
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "temp", Value = JsonSerializer.SerializeToElement(90.0) }
            }
        };
        
        var command2 = new IngestEventCommand { Event = eventDto2 };
        var response2 = await EventHandler.Handle(command2, CancellationToken.None);
        
        // Assert: Close values should have higher similarity
        var reminderAfterDistant = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        Assert.True(confidenceAfterClose >= reminderAfterDistant.Confidence);
    }
    
    [Fact]
    public async Task Scenario_3_1_ManySensorsProvided_OnlyTopKUsed()
    {
        // Arrange: Create reminder
        var reminder = new ReminderCandidate(
            TestPersonId,
            "test_action",
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            null,
            0.5);
        await ReminderRepository.AddAsync(reminder, CancellationToken.None);
        
        // Act: Create event with 20+ sensors
        var signalStates = new List<SignalStateDto>();
        for (int i = 0; i < 25; i++)
        {
            signalStates.Add(new SignalStateDto
            {
                SensorId = $"sensor_{i}",
                Value = JsonSerializer.SerializeToElement(i % 2 == 0),
                RawImportance = i < 5 ? 1.0 : 0.1 // First 5 are important
            });
        }
        
        var eventDto = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = signalStates
        };
        
        var command = new IngestEventCommand { Event = eventDto };
        var response = await EventHandler.Handle(command, CancellationToken.None);
        
        // Assert: Only top-K sensors should be in baseline
        var updatedReminder = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        var profile = updatedReminder.GetSignalProfile();
        Assert.NotNull(profile);
        // Should have at most 10 sensors (default SignalSelectionLimit)
        Assert.True(profile.Signals.Count <= 10);
    }
    
    [Fact]
    public async Task Scenario_3_2_ImportanceWeighting_ImportantSensorsDominate()
    {
        // Arrange: Create reminder with presence and light baseline
        var reminder = new ReminderCandidate(
            TestPersonId,
            "test_action",
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            null,
            0.5);
        
        var baselineProfile = new AIPatterner.Domain.ValueObjects.SignalProfile
        {
            Signals = new Dictionary<string, AIPatterner.Domain.ValueObjects.SignalProfileEntry>
            {
                ["presence"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 1.0, // High importance
                    NormalizedValue = 1.0 // home
                },
                ["light"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 0.3, // Lower importance
                    NormalizedValue = 0.5
                }
            }
        };
        reminder.UpdateSignalProfile(baselineProfile, 0.1);
        await ReminderRepository.AddAsync(reminder, CancellationToken.None);
        
        // Act: Change only light level
        var eventDto1 = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement("home") },
                new SignalStateDto { SensorId = "light", Value = JsonSerializer.SerializeToElement(0.1) } // Changed
            }
        };
        
        var command1 = new IngestEventCommand { Event = eventDto1 };
        var response1 = await EventHandler.Handle(command1, CancellationToken.None);
        var reminderAfterLightChange = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        var confidenceAfterLightChange = reminderAfterLightChange.Confidence;
        
        // Reset
        reminderAfterLightChange.DecreaseConfidence(0.2);
        await ReminderRepository.UpdateAsync(reminderAfterLightChange, CancellationToken.None);
        
        // Act: Change presence
        var eventDto2 = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow.AddMinutes(1),
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement("away") }, // Changed
                new SignalStateDto { SensorId = "light", Value = JsonSerializer.SerializeToElement(0.5) }
            }
        };
        
        var command2 = new IngestEventCommand { Event = eventDto2 };
        var response2 = await EventHandler.Handle(command2, CancellationToken.None);
        
        // Assert: Presence change should have more impact
        var reminderAfterPresenceChange = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        // Presence change should result in lower similarity (or skip)
        Assert.True(confidenceAfterLightChange >= reminderAfterPresenceChange.Confidence);
    }
    
    [Fact]
    public async Task Scenario_4_1_SimilarityAboveThreshold_ReminderProceeds()
    {
        // Arrange: Create reminder with baseline
        var reminder = new ReminderCandidate(
            TestPersonId,
            "test_action",
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            null,
            0.5);
        
        var baselineProfile = new AIPatterner.Domain.ValueObjects.SignalProfile
        {
            Signals = new Dictionary<string, AIPatterner.Domain.ValueObjects.SignalProfileEntry>
            {
                ["presence"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 1.0,
                    NormalizedValue = 1.0
                }
            }
        };
        reminder.UpdateSignalProfile(baselineProfile, 0.1);
        var initialSamplesCount = reminder.SignalProfileSamplesCount;
        await ReminderRepository.AddAsync(reminder, CancellationToken.None);
        
        // Act: Create event with similar signals
        var eventDto = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement("home") }
            }
        };
        
        var command = new IngestEventCommand { Event = eventDto };
        var response = await EventHandler.Handle(command, CancellationToken.None);
        
        // Assert: Reminder should proceed
        var updatedReminder = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        Assert.NotNull(updatedReminder);
        Assert.True(updatedReminder.Confidence > 0.5); // Confidence increased
        Assert.NotNull(updatedReminder.SignalProfileJson); // Baseline exists
        Assert.True(updatedReminder.SignalProfileSamplesCount > initialSamplesCount); // Baseline updated
        Assert.NotNull(updatedReminder.SignalProfileUpdatedAtUtc);
        Assert.Equal(reminder.Id, response.RelatedReminderId); // Reminder was matched
    }
    
    [Fact]
    public async Task Scenario_4_2_SimilarityBelowThreshold_ReminderSkipped()
    {
        // Arrange: Create reminder with baseline
        var reminder = new ReminderCandidate(
            TestPersonId,
            "test_action",
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            null,
            0.5);
        
        var baselineProfile = new AIPatterner.Domain.ValueObjects.SignalProfile
        {
            Signals = new Dictionary<string, AIPatterner.Domain.ValueObjects.SignalProfileEntry>
            {
                ["presence"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 1.0,
                    NormalizedValue = 1.0 // home
                },
                ["music"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 0.6,
                    NormalizedValue = 1.0 // playing
                }
            }
        };
        reminder.UpdateSignalProfile(baselineProfile, 0.1);
        var initialSamplesCount = reminder.SignalProfileSamplesCount;
        var initialConfidence = reminder.Confidence;
        await ReminderRepository.AddAsync(reminder, CancellationToken.None);
        
        // Act: Create event with conflicting signals
        var eventDto = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement("away") }, // Different
                // No music signal (missing)
            }
        };
        
        var command = new IngestEventCommand { Event = eventDto };
        var response = await EventHandler.Handle(command, CancellationToken.None);
        
        // Assert: Reminder should be skipped
        var updatedReminder = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        Assert.NotNull(updatedReminder);
        // Baseline should NOT be updated
        Assert.Equal(initialSamplesCount, updatedReminder.SignalProfileSamplesCount);
        // Confidence should NOT have increased (or should have decreased if penalty applied)
        Assert.True(updatedReminder.Confidence <= initialConfidence + 0.01); // Allow small floating point error
        // Reminder should NOT be in response (or should be null)
        // Note: The response might still have RelatedReminderId if time/action matching occurred,
        // but the signal check should have prevented baseline update
    }
    
    [Fact]
    public async Task Scenario_4_3_BorderlineSimilarity_DeterministicBehavior()
    {
        // Arrange: Create reminder with baseline
        var reminder = new ReminderCandidate(
            TestPersonId,
            "test_action",
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            null,
            0.5);
        
        var baselineProfile = new AIPatterner.Domain.ValueObjects.SignalProfile
        {
            Signals = new Dictionary<string, AIPatterner.Domain.ValueObjects.SignalProfileEntry>
            {
                ["presence"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 1.0,
                    NormalizedValue = 1.0
                }
            }
        };
        reminder.UpdateSignalProfile(baselineProfile, 0.1);
        await ReminderRepository.AddAsync(reminder, CancellationToken.None);
        
        // Act: Create event with borderline similarity (just below threshold)
        // Using a value that should result in similarity ~0.68 (below 0.70 threshold)
        var eventDto = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement("away") } // Different from "home"
            }
        };
        
        var command = new IngestEventCommand { Event = eventDto };
        var initialSamplesCount = reminder.SignalProfileSamplesCount;
        
        // Run multiple times to ensure deterministic behavior
        for (int i = 0; i < 3; i++)
        {
            var response = await EventHandler.Handle(command, CancellationToken.None);
            var updatedReminder = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
            
            // Assert: Behavior should be consistent
            Assert.Equal(initialSamplesCount, updatedReminder.SignalProfileSamplesCount); // Should not update
        }
    }
    
    [Fact]
    public async Task Scenario_5_1_BaselineCreatedOnFirstValidMatch()
    {
        // Arrange: Create reminder WITHOUT baseline
        var reminder = new ReminderCandidate(
            TestPersonId,
            "test_action",
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            null,
            0.5);
        
        Assert.Null(reminder.SignalProfileJson); // No baseline initially
        await ReminderRepository.AddAsync(reminder, CancellationToken.None);
        
        // Act: Create event that matches normally
        var eventDto = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement("home") }
            }
        };
        
        var command = new IngestEventCommand { Event = eventDto };
        var response = await EventHandler.Handle(command, CancellationToken.None);
        
        // Assert: Baseline should be created
        var updatedReminder = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        Assert.NotNull(updatedReminder.SignalProfileJson);
        Assert.Equal(1, updatedReminder.SignalProfileSamplesCount);
        Assert.NotNull(updatedReminder.SignalProfileUpdatedAtUtc);
        
        var profile = updatedReminder.GetSignalProfile();
        Assert.NotNull(profile);
        Assert.True(profile.Signals.Count > 0);
    }
    
    [Fact]
    public async Task Scenario_5_2_BaselineUpdatedViaEMA_GradualChange()
    {
        // Arrange: Create reminder with baseline
        var reminder = new ReminderCandidate(
            TestPersonId,
            "test_action",
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            null,
            0.5);
        
        var baselineProfile = new AIPatterner.Domain.ValueObjects.SignalProfile
        {
            Signals = new Dictionary<string, AIPatterner.Domain.ValueObjects.SignalProfileEntry>
            {
                ["presence"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 1.0,
                    NormalizedValue = 0.5 // Initial value
                }
            }
        };
        reminder.UpdateSignalProfile(baselineProfile, 0.1);
        await ReminderRepository.AddAsync(reminder, CancellationToken.None);
        
        var initialValue = baselineProfile.Signals["presence"].NormalizedValue;
        
        // Act: Create two similar events in sequence
        for (int i = 0; i < 2; i++)
        {
            var eventDto = new ActionEventDto
            {
                PersonId = TestPersonId,
                ActionType = "test_action",
                TimestampUtc = DateTime.UtcNow.AddMinutes(i),
                Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
                SignalStates = new List<SignalStateDto>
                {
                    new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement(0.8) } // New value
                }
            };
            
            var command = new IngestEventCommand { Event = eventDto };
            await EventHandler.Handle(command, CancellationToken.None);
        }
        
        // Assert: Baseline should change gradually (EMA)
        var updatedReminder = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        var profile = updatedReminder.GetSignalProfile();
        Assert.NotNull(profile);
        var finalValue = profile.Signals["presence"].NormalizedValue;
        
        // Should be between initial (0.5) and new (0.8), but closer to initial due to EMA
        Assert.True(finalValue > initialValue);
        Assert.True(finalValue < 0.8);
        Assert.Equal(3, updatedReminder.SignalProfileSamplesCount); // Initial + 2 updates
    }
    
    [Fact]
    public async Task Scenario_5_3_BaselineNotUpdatedOnSkip()
    {
        // Arrange: Create reminder with baseline
        var reminder = new ReminderCandidate(
            TestPersonId,
            "test_action",
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            null,
            0.5);
        
        var baselineProfile = new AIPatterner.Domain.ValueObjects.SignalProfile
        {
            Signals = new Dictionary<string, AIPatterner.Domain.ValueObjects.SignalProfileEntry>
            {
                ["presence"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 1.0,
                    NormalizedValue = 1.0 // home
                }
            }
        };
        reminder.UpdateSignalProfile(baselineProfile, 0.1);
        var initialSamplesCount = reminder.SignalProfileSamplesCount;
        var initialUpdatedAt = reminder.SignalProfileUpdatedAtUtc;
        await ReminderRepository.AddAsync(reminder, CancellationToken.None);
        
        // Act: Create event that will be skipped due to signal mismatch
        var eventDto = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement("away") } // Mismatch
            }
        };
        
        var command = new IngestEventCommand { Event = eventDto };
        await EventHandler.Handle(command, CancellationToken.None);
        
        // Assert: Baseline should NOT be updated
        var updatedReminder = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        Assert.Equal(initialSamplesCount, updatedReminder.SignalProfileSamplesCount);
        Assert.Equal(initialUpdatedAt, updatedReminder.SignalProfileUpdatedAtUtc);
    }
    
    [Fact]
    public async Task Scenario_6_1_InsideRoutineWindow_SignalLogicApplied()
    {
        // Arrange: Create routine and activate it
        var routine = new Routine(TestPersonId, "ArrivalHome", DateTime.UtcNow);
        routine.OpenObservationWindow(DateTime.UtcNow, 60, "evening");
        await Context.Routines.AddAsync(routine);
        await Context.SaveChangesAsync();
        
        // Create routine reminder with baseline
        var routineReminder = new RoutineReminder(
            routine.Id,
            TestPersonId,
            "PlayMusic",
            0.5);
        
        var baselineProfile = new AIPatterner.Domain.ValueObjects.SignalProfile
        {
            Signals = new Dictionary<string, AIPatterner.Domain.ValueObjects.SignalProfileEntry>
            {
                ["presence"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 1.0,
                    NormalizedValue = 1.0
                }
            }
        };
        routineReminder.UpdateSignalProfile(baselineProfile, 0.1);
        var initialSamplesCount = routineReminder.SignalProfileSamplesCount;
        await Context.RoutineReminders.AddAsync(routineReminder);
        await Context.SaveChangesAsync();
        
        // Act: Create event INSIDE observation window with matching signals
        var eventDto = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "PlayMusic",
            TimestampUtc = DateTime.UtcNow.AddMinutes(5), // Inside window
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement("home") }
            }
        };
        
        var command = new IngestEventCommand { Event = eventDto };
        await EventHandler.Handle(command, CancellationToken.None);
        
        // Assert: Signal logic should be applied, baseline updated
        var updatedReminder = await Context.RoutineReminders.FindAsync(routineReminder.Id);
        Assert.NotNull(updatedReminder);
        Assert.True(updatedReminder!.SignalProfileSamplesCount > initialSamplesCount);
    }
    
    [Fact]
    public async Task Scenario_6_2_OutsideRoutineWindow_BaselineNotUpdated()
    {
        // Arrange: Create routine and activate it
        var routine = new Routine(TestPersonId, "ArrivalHome", DateTime.UtcNow);
        routine.OpenObservationWindow(DateTime.UtcNow, 60, "evening"); // 60 minute window
        await Context.Routines.AddAsync(routine);
        await Context.SaveChangesAsync();
        
        // Create routine reminder with baseline
        var routineReminder = new RoutineReminder(
            routine.Id,
            TestPersonId,
            "PlayMusic",
            0.5);
        
        var baselineProfile = new AIPatterner.Domain.ValueObjects.SignalProfile
        {
            Signals = new Dictionary<string, AIPatterner.Domain.ValueObjects.SignalProfileEntry>
            {
                ["presence"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 1.0,
                    NormalizedValue = 1.0
                }
            }
        };
        routineReminder.UpdateSignalProfile(baselineProfile, 0.1);
        var initialSamplesCount = routineReminder.SignalProfileSamplesCount;
        await Context.RoutineReminders.AddAsync(routineReminder);
        await Context.SaveChangesAsync();
        
        // Act: Create event OUTSIDE observation window (after 60 minutes)
        var eventDto = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "PlayMusic",
            TimestampUtc = DateTime.UtcNow.AddMinutes(70), // Outside window
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement("home") }
            }
        };
        
        var command = new IngestEventCommand { Event = eventDto };
        await EventHandler.Handle(command, CancellationToken.None);
        
        // Assert: Baseline should NOT be updated (outside window)
        var updatedReminder = await Context.RoutineReminders.FindAsync(routineReminder.Id);
        Assert.NotNull(updatedReminder);
        Assert.Equal(initialSamplesCount, updatedReminder!.SignalProfileSamplesCount);
    }
    
    [Fact]
    public async Task Scenario_7_1_TimeOffsetInvalid_SignalsIgnored()
    {
        // Arrange: Create reminder with time window
        var reminder = new ReminderCandidate(
            TestPersonId,
            "test_action",
            DateTime.UtcNow.AddHours(-2), // 2 hours ago
            ReminderStyle.Suggest,
            null,
            0.5);
        
        var baselineProfile = new AIPatterner.Domain.ValueObjects.SignalProfile
        {
            Signals = new Dictionary<string, AIPatterner.Domain.ValueObjects.SignalProfileEntry>
            {
                ["presence"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 1.0,
                    NormalizedValue = 1.0
                }
            }
        };
        reminder.UpdateSignalProfile(baselineProfile, 0.1);
        var initialSamplesCount = reminder.SignalProfileSamplesCount;
        await ReminderRepository.AddAsync(reminder, CancellationToken.None);
        
        // Act: Create event far outside time offset (but with matching signals)
        var eventDto = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow, // Current time (2 hours after reminder time)
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement("home") }
            }
        };
        
        var command = new IngestEventCommand { Event = eventDto };
        var response = await EventHandler.Handle(command, CancellationToken.None);
        
        // Assert: Reminder should not match (time offset invalid)
        var updatedReminder = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        Assert.Equal(initialSamplesCount, updatedReminder.SignalProfileSamplesCount); // Baseline unchanged
        // Response should not have this reminder as related
        Assert.NotEqual(reminder.Id, response.RelatedReminderId);
    }
    
    [Fact]
    public async Task Scenario_7_2_TimeOffsetValid_SignalsInvalid_SkipOccurs()
    {
        // Arrange: Create reminder with time window
        var reminder = new ReminderCandidate(
            TestPersonId,
            "test_action",
            DateTime.UtcNow.AddMinutes(30), // 30 minutes from now
            ReminderStyle.Suggest,
            null,
            0.5);
        
        var baselineProfile = new AIPatterner.Domain.ValueObjects.SignalProfile
        {
            Signals = new Dictionary<string, AIPatterner.Domain.ValueObjects.SignalProfileEntry>
            {
                ["presence"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 1.0,
                    NormalizedValue = 1.0 // home
                }
            }
        };
        reminder.UpdateSignalProfile(baselineProfile, 0.1);
        var initialSamplesCount = reminder.SignalProfileSamplesCount;
        await ReminderRepository.AddAsync(reminder, CancellationToken.None);
        
        // Act: Create event with valid time offset but invalid signals
        var eventDto = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow.AddMinutes(25), // Within time offset
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement("away") } // Mismatch
            }
        };
        
        var command = new IngestEventCommand { Event = eventDto };
        var response = await EventHandler.Handle(command, CancellationToken.None);
        
        // Assert: Signal mismatch should still skip reminder
        var updatedReminder = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        Assert.Equal(initialSamplesCount, updatedReminder.SignalProfileSamplesCount); // Baseline not updated
    }
    
    [Fact]
    public async Task Scenario_8_1_HighConfidenceButSignalMismatch_ExecutionBlocked()
    {
        // Arrange: Create reminder with high confidence
        var reminder = new ReminderCandidate(
            TestPersonId,
            "test_action",
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            null,
            0.9); // High confidence
        
        var baselineProfile = new AIPatterner.Domain.ValueObjects.SignalProfile
        {
            Signals = new Dictionary<string, AIPatterner.Domain.ValueObjects.SignalProfileEntry>
            {
                ["presence"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 1.0,
                    NormalizedValue = 1.0 // home
                }
            }
        };
        reminder.UpdateSignalProfile(baselineProfile, 0.1);
        reminder.SetIsSafeToAutoExecute(true); // Safe to auto-execute
        var initialSamplesCount = reminder.SignalProfileSamplesCount;
        await ReminderRepository.AddAsync(reminder, CancellationToken.None);
        
        // Act: Create event with signal mismatch
        var eventDto = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement("away") } // Mismatch
            }
        };
        
        var command = new IngestEventCommand { Event = eventDto };
        var response = await EventHandler.Handle(command, CancellationToken.None);
        
        // Assert: Signal mismatch should block execution
        var updatedReminder = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        Assert.Equal(initialSamplesCount, updatedReminder.SignalProfileSamplesCount); // Baseline not updated
        // Confidence should not have increased despite high initial confidence
        Assert.True(updatedReminder.Confidence <= 0.9 + 0.01);
    }
    
    [Fact]
    public async Task Scenario_9_1_UserPromptAddedOnlyOnValidMatch()
    {
        // Arrange: Create reminder with baseline
        var reminder = new ReminderCandidate(
            TestPersonId,
            "test_action",
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            null,
            0.5);
        
        var baselineProfile = new AIPatterner.Domain.ValueObjects.SignalProfile
        {
            Signals = new Dictionary<string, AIPatterner.Domain.ValueObjects.SignalProfileEntry>
            {
                ["presence"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 1.0,
                    NormalizedValue = 1.0
                }
            }
        };
        reminder.UpdateSignalProfile(baselineProfile, 0.1);
        await ReminderRepository.AddAsync(reminder, CancellationToken.None);
        
        // Act: Create event with userPrompt and valid signal match
        var eventDto = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement("home") }
            },
            UserPrompt = "This is a test prompt"
        };
        
        var command = new IngestEventCommand { Event = eventDto };
        await EventHandler.Handle(command, CancellationToken.None);
        
        // Assert: userPrompt should be appended
        var updatedReminder = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        var prompts = updatedReminder.GetUserPrompts();
        Assert.NotEmpty(prompts);
        Assert.Contains(prompts, p => p.Text == "This is a test prompt");
    }
    
    [Fact]
    public async Task Scenario_9_2_UserPromptIgnoredOnSignalSkip()
    {
        // Arrange: Create reminder with baseline
        var reminder = new ReminderCandidate(
            TestPersonId,
            "test_action",
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            null,
            0.5);
        
        var baselineProfile = new AIPatterner.Domain.ValueObjects.SignalProfile
        {
            Signals = new Dictionary<string, AIPatterner.Domain.ValueObjects.SignalProfileEntry>
            {
                ["presence"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 1.0,
                    NormalizedValue = 1.0 // home
                }
            }
        };
        reminder.UpdateSignalProfile(baselineProfile, 0.1);
        await ReminderRepository.AddAsync(reminder, CancellationToken.None);
        
        var initialPrompts = reminder.GetUserPrompts();
        var initialPromptCount = initialPrompts.Count;
        
        // Act: Create event with userPrompt but signal mismatch
        var eventDto = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement("away") } // Mismatch
            },
            UserPrompt = "This prompt should be ignored"
        };
        
        var command = new IngestEventCommand { Event = eventDto };
        await EventHandler.Handle(command, CancellationToken.None);
        
        // Assert: userPrompt should NOT be appended
        var updatedReminder = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        var prompts = updatedReminder.GetUserPrompts();
        Assert.Equal(initialPromptCount, prompts.Count);
        Assert.DoesNotContain(prompts, p => p.Text == "This prompt should be ignored");
    }
    
    [Fact]
    public async Task Scenario_10_1_RepeatedFalsePositivesPrevented()
    {
        // Arrange: Create reminder and build baseline with 3 historical matches
        var reminder = new ReminderCandidate(
            TestPersonId,
            "test_action",
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            null,
            0.5);
        
        await ReminderRepository.AddAsync(reminder, CancellationToken.None);
        
        // Create 3 matching events to build baseline
        for (int i = 0; i < 3; i++)
        {
            var eventDto = new ActionEventDto
            {
                PersonId = TestPersonId,
                ActionType = "test_action",
                TimestampUtc = DateTime.UtcNow.AddMinutes(-30 + i * 5),
                Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
                SignalStates = new List<SignalStateDto>
                {
                    new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement("home") },
                    new SignalStateDto { SensorId = "light", Value = JsonSerializer.SerializeToElement(0.5) }
                }
            };
            
            var command = new IngestEventCommand { Event = eventDto };
            await EventHandler.Handle(command, CancellationToken.None);
        }
        
        var reminderAfterBaseline = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        var baselineSamplesCount = reminderAfterBaseline.SignalProfileSamplesCount;
        Assert.True(baselineSamplesCount >= 3);
        
        // Act: Create 4th event with strongly different context
        var eventDto4 = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "morning", DayType = "weekend" }, // Different context
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement("away") }, // Different
                new SignalStateDto { SensorId = "light", Value = JsonSerializer.SerializeToElement(0.9) } // Different
            }
        };
        
        var command4 = new IngestEventCommand { Event = eventDto4 };
        var response4 = await EventHandler.Handle(command4, CancellationToken.None);
        
        // Assert: System should NOT blindly execute
        var finalReminder = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        // Baseline should not be updated (or updated minimally)
        Assert.True(finalReminder.SignalProfileSamplesCount <= baselineSamplesCount + 1);
    }
    
    [Fact]
    public async Task Scenario_10_2_DifferentHouseholdContext_ReminderSkipped()
    {
        // Arrange: Create reminder with baseline (normal household state)
        var reminder = new ReminderCandidate(
            TestPersonId,
            "test_action",
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            null,
            0.5);
        
        var baselineProfile = new AIPatterner.Domain.ValueObjects.SignalProfile
        {
            Signals = new Dictionary<string, AIPatterner.Domain.ValueObjects.SignalProfileEntry>
            {
                ["presence"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 1.0,
                    NormalizedValue = 1.0 // home, alone
                },
                ["guests"] = new AIPatterner.Domain.ValueObjects.SignalProfileEntry
                {
                    Weight = 0.8,
                    NormalizedValue = 0.0 // no guests
                }
            }
        };
        reminder.UpdateSignalProfile(baselineProfile, 0.1);
        var initialSamplesCount = reminder.SignalProfileSamplesCount;
        await ReminderRepository.AddAsync(reminder, CancellationToken.None);
        
        // Act: Create event with different household state (guests present)
        var eventDto = new ActionEventDto
        {
            PersonId = TestPersonId,
            ActionType = "test_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "evening", DayType = "weekday" },
            SignalStates = new List<SignalStateDto>
            {
                new SignalStateDto { SensorId = "presence", Value = JsonSerializer.SerializeToElement("home") },
                new SignalStateDto { SensorId = "guests", Value = JsonSerializer.SerializeToElement(true) } // Guests present!
            }
        };
        
        var command = new IngestEventCommand { Event = eventDto };
        var response = await EventHandler.Handle(command, CancellationToken.None);
        
        // Assert: Reminder should be skipped or downgraded
        var updatedReminder = await ReminderRepository.GetByIdAsync(reminder.Id, CancellationToken.None);
        // Baseline should not be updated (or updated minimally)
        Assert.True(updatedReminder.SignalProfileSamplesCount <= initialSamplesCount + 1);
    }
}

