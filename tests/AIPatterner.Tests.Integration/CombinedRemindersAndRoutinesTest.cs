// Comprehensive test combining general reminders and routines with random event types
// Tests occurrence pattern display and state signal matching
namespace AIPatterner.Tests.Integration;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

public class CombinedRemindersAndRoutinesTest : RealDatabaseTestBase
{
    private readonly Random _random;
    private readonly DateTime _testStartDate;
    private readonly List<string> _testPersonIds;
    private readonly List<string> _testActions;
    private readonly List<string> _testIntents;
    private readonly List<TestArtifact> _artifacts;
    private readonly Dictionary<string, List<string>> _personStateSignals;

    public CombinedRemindersAndRoutinesTest()
    {
        _random = new Random(54321); // Seeded for reproducibility
        _testStartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _testPersonIds = new List<string>();
        _testActions = new List<string> 
        { 
            "PlayMusic", "TurnOnLights", "AdjustAC", "LockDoors", "BrewCoffee",
            "OpenBlinds", "SetTemperature", "StartDishwasher", "WaterPlants", "FeedPet"
        };
        _testIntents = new List<string>
        {
            "ArrivalHome", "GoingToSleep", "LeavingHome", "StartingWork", "EndingWork"
        };
        _artifacts = new List<TestArtifact>();
        _personStateSignals = new Dictionary<string, List<string>>();
    }

    /// <summary>
    /// Override cleanup to preserve test data for manual verification.
    /// </summary>
    protected override void CleanupTestData()
    {
        // Skip cleanup for combined_test_* data to preserve it for manual verification
        var testPersonIds = new[] { "user", "api_user", "api_test_user", "api_related_user", "api_feedback_user", 
            "feedback_user", "daily_user", "weekly_user", "user_a", "user_b", "user_c", "routine_test_user",
            "event_person", "reminder_person", "routine_person", "duplicate_test_person", "matched_user",
            "user_for_id", "testuser_dual", "testuser1", "testuser2", "adminuser", "comprehensive_test_user",
            "household_person_a", "household_person_b", "household_person_c", "general_reminder_test_user" };

        foreach (var personIdPrefix in testPersonIds)
        {
            // Delete reminders
            var reminders = Context.ReminderCandidates
                .Where(r => r.PersonId.StartsWith(personIdPrefix))
                .ToList();
            Context.ReminderCandidates.RemoveRange(reminders);

            // Delete events
            var events = Context.ActionEvents
                .Where(e => e.PersonId.StartsWith(personIdPrefix))
                .ToList();
            Context.ActionEvents.RemoveRange(events);

            // Delete transitions
            var transitions = Context.ActionTransitions
                .Where(t => t.PersonId.StartsWith(personIdPrefix))
                .ToList();
            Context.ActionTransitions.RemoveRange(transitions);

            // Delete cooldowns
            var cooldowns = Context.ReminderCooldowns
                .Where(c => c.PersonId.StartsWith(personIdPrefix))
                .ToList();
            Context.ReminderCooldowns.RemoveRange(cooldowns);
        }

        // Clean up routines and routine reminders (excluding combined_test_*)
        var routineTestPersonIds = Context.Routines
            .Where(r => (r.PersonId.StartsWith("routine_test_user") || r.PersonId.StartsWith("routine_person")) 
                     && !r.PersonId.StartsWith("combined_test_"))
            .Select(r => r.PersonId)
            .Distinct()
            .ToList();

        foreach (var personId in routineTestPersonIds)
        {
            var routines = Context.Routines
                .Where(r => r.PersonId == personId)
                .ToList();
            
            foreach (var routine in routines)
            {
                var routineReminders = Context.RoutineReminders
                    .Where(rr => rr.RoutineId == routine.Id)
                    .ToList();
                Context.RoutineReminders.RemoveRange(routineReminders);
            }
            
            Context.Routines.RemoveRange(routines);
        }

        // Clean up test users
        var testUsernames = new[] { "testuser1", "testuser2", "adminuser", "matched_user", "user_for_id", "testuser_dual" };
        var testUsers = Context.Users
            .Where(u => testUsernames.Contains(u.Username))
            .ToList();
        Context.Users.RemoveRange(testUsers);

        Context.SaveChanges();
        
        // Note: combined_test_* data is intentionally NOT cleaned up to allow manual verification
    }

    [Fact]
    public async Task Combined_GeneralRemindersAndRoutines_RandomEvents_ComprehensiveTest()
    {
        // Arrange - Create 4 test users
        const int numUsers = 4;
        const int daysToSimulate = 21; // 3 weeks to test weekly patterns
        const int eventsPerDayPerUser = 6; // Mix of Action and StateChange
        
        for (int i = 1; i <= numUsers; i++)
        {
            var personId = $"combined_test_user_{i}";
            _testPersonIds.Add(personId);
            _personStateSignals[personId] = new List<string> { "home", "work", "vacation" };
        }

        Console.WriteLine($"=== Starting Combined Reminders & Routines Test ===");
        Console.WriteLine($"Users: {numUsers}, Days: {daysToSimulate}, Events per day per user: {eventsPerDayPerUser}");
        Console.WriteLine($"Total events to create: {numUsers * daysToSimulate * eventsPerDayPerUser}\n");

        var eventCount = 0;
        var reminderSnapshots = new List<ReminderSnapshot>();
        var routineSnapshots = new List<RoutineSnapshot>();

        // Act - Generate random events (both Action and StateChange) over time
        for (int day = 0; day < daysToSimulate; day++)
        {
            var dayTime = _testStartDate.AddDays(day);
            var dayOfWeek = dayTime.DayOfWeek;
            
            foreach (var personId in _testPersonIds)
            {
                for (int eventNum = 0; eventNum < eventsPerDayPerUser; eventNum++)
                {
                    // Randomly decide event type: 70% Action, 30% StateChange
                    var eventType = _random.NextDouble() < 0.7 ? EventType.Action : EventType.StateChange;
                    
                    // Generate random but realistic event time
                    var hour = _random.Next(6, 23);
                    var minute = _random.Next(0, 60);
                    var eventTime = new DateTime(dayTime.Year, dayTime.Month, dayTime.Day, hour, minute, 0, DateTimeKind.Utc);
                    eventTime = eventTime.AddMinutes(_random.Next(-15, 15));
                    
                    var timeBucket = GetTimeBucket(eventTime);
                    var dayType = GetDayType(eventTime);
                    
                    // Select action or intent based on event type
                    string actionOrIntent;
                    if (eventType == EventType.StateChange)
                    {
                        actionOrIntent = _testIntents[_random.Next(_testIntents.Count)];
                    }
                    else
                    {
                        actionOrIntent = _testActions[_random.Next(_testActions.Count)];
                    }
                    
                    // Add state signals randomly (30% chance)
                    var stateSignals = new Dictionary<string, string>();
                    if (_random.NextDouble() < 0.3)
                    {
                        var signalKey = _personStateSignals[personId][_random.Next(_personStateSignals[personId].Count)];
                        stateSignals["location"] = signalKey;
                        if (_random.NextDouble() < 0.5)
                        {
                            stateSignals["weather"] = _random.NextDouble() < 0.5 ? "sunny" : "rainy";
                        }
                    }
                    
                    // Create event
                    await CreateEventAsync(personId, actionOrIntent, eventTime, eventType, 
                        timeBucket, dayType, "home", stateSignals);
                    
                    eventCount++;
                    
                    // If StateChange, add follow-up actions (for routine learning)
                    if (eventType == EventType.StateChange && _random.NextDouble() < 0.7)
                    {
                        // Add 1-3 follow-up actions within observation window
                        var numFollowUps = _random.Next(1, 4);
                        for (int i = 0; i < numFollowUps; i++)
                        {
                            var followUpTime = eventTime.AddMinutes(_random.Next(2, 20));
                            var followUpAction = _testActions[_random.Next(_testActions.Count)];
                            await CreateEventAsync(personId, followUpAction, followUpTime, EventType.Action,
                                timeBucket, dayType, "home", stateSignals);
                            eventCount++;
                        }
                    }
                    
                    // Every 20 events, take snapshots
                    if (eventCount % 20 == 0)
                    {
                        var reminderSnapshot = await TakeReminderSnapshot(day, eventCount);
                        reminderSnapshots.Add(reminderSnapshot);
                        
                        var routineSnapshot = await TakeRoutineSnapshot(day, eventCount);
                        routineSnapshots.Add(routineSnapshot);
                        
                        // Verify logic during execution
                        await VerifyReminderLogic(reminderSnapshot);
                        await VerifyRoutineLogic(routineSnapshot);
                    }
                }
            }
            
            Console.WriteLine($"Day {day + 1}/{daysToSimulate} completed. Total events: {eventCount}");
        }

        // Final verification
        Console.WriteLine($"\n=== Final Verification ===");
        var finalReminderSnapshot = await TakeReminderSnapshot(daysToSimulate, eventCount);
        var finalRoutineSnapshot = await TakeRoutineSnapshot(daysToSimulate, eventCount);
        
        await VerifyReminderLogic(finalReminderSnapshot);
        await VerifyRoutineLogic(finalRoutineSnapshot);
        await VerifyPersonIsolation();
        await VerifyOccurrencePatterns(finalReminderSnapshot);
        await VerifyNoDuplicateReminders();
        
        // Print summary
        PrintSummary(finalReminderSnapshot, finalRoutineSnapshot);
        PrintArtifacts();
    }

    private async Task<ReminderSnapshot> TakeReminderSnapshot(int day, int eventCount)
    {
        var reminders = await Context.ReminderCandidates
            .Where(r => _testPersonIds.Contains(r.PersonId))
            .ToListAsync();
        
        return new ReminderSnapshot
        {
            Day = day,
            EventCount = eventCount,
            ReminderCount = reminders.Count,
            Reminders = reminders.Select(r => new ReminderInfo
            {
                PersonId = r.PersonId,
                Action = r.SuggestedAction,
                Confidence = r.Confidence,
                Id = r.Id,
                Occurrence = r.Occurrence,
                PatternStatus = r.PatternInferenceStatus.ToString(),
                TimeWindowCenter = r.TimeWindowCenter?.ToString(@"hh\:mm"),
                EvidenceCount = r.EvidenceCount,
                CustomData = r.CustomData
            }).ToList()
        };
    }

    private async Task<RoutineSnapshot> TakeRoutineSnapshot(int day, int eventCount)
    {
        var routines = await Context.Routines
            .Where(r => _testPersonIds.Contains(r.PersonId))
            .ToListAsync();
        
        var routineDetails = new List<RoutineInfo>();
        foreach (var routine in routines)
        {
            var routineReminders = await Context.RoutineReminders
                .Where(rr => rr.RoutineId == routine.Id)
                .ToListAsync();
            
            routineDetails.Add(new RoutineInfo
            {
                PersonId = routine.PersonId,
                IntentType = routine.IntentType,
                RoutineId = routine.Id,
                ReminderCount = routineReminders.Count,
                Reminders = routineReminders.Select(rr => new RoutineReminderInfo
                {
                    Action = rr.SuggestedAction,
                    Confidence = rr.Confidence
                }).ToList()
            });
        }
        
        return new RoutineSnapshot
        {
            Day = day,
            EventCount = eventCount,
            RoutineCount = routines.Count,
            Routines = routineDetails
        };
    }

    private async Task VerifyReminderLogic(ReminderSnapshot snapshot)
    {
        // Check confidence values
        foreach (var reminder in snapshot.Reminders)
        {
            if (reminder.Confidence < 0 || reminder.Confidence > 1.0)
            {
                RecordArtifact("Invalid Confidence", 
                    $"Reminder {reminder.PersonId} -> {reminder.Action} has invalid confidence: {reminder.Confidence}");
            }
        }
    }

    private async Task VerifyRoutineLogic(RoutineSnapshot snapshot)
    {
        // Verify routines have reminders
        foreach (var routine in snapshot.Routines)
        {
            if (routine.ReminderCount == 0)
            {
                RecordArtifact("Routine Without Reminders", 
                    $"Routine {routine.PersonId} -> {routine.IntentType} has no reminders");
            }
        }
    }

    private async Task VerifyPersonIsolation()
    {
        var reminders = await Context.ReminderCandidates
            .Where(r => _testPersonIds.Contains(r.PersonId))
            .ToListAsync();
        
        var routines = await Context.Routines
            .Where(r => _testPersonIds.Contains(r.PersonId))
            .ToListAsync();
        
        // Verify reminders are properly scoped
        foreach (var reminder in reminders)
        {
            if (!_testPersonIds.Contains(reminder.PersonId))
            {
                RecordArtifact("Person Isolation", 
                    $"Reminder {reminder.Id} has invalid personId: {reminder.PersonId}");
            }
        }
        
        // Verify routines are properly scoped
        foreach (var routine in routines)
        {
            if (!_testPersonIds.Contains(routine.PersonId))
            {
                RecordArtifact("Person Isolation", 
                    $"Routine {routine.Id} has invalid personId: {routine.PersonId}");
            }
        }
    }

    private async Task VerifyOccurrencePatterns(ReminderSnapshot snapshot)
    {
        Console.WriteLine($"\n=== Occurrence Pattern Analysis ===");
        
        var patternsByStatus = snapshot.Reminders
            .GroupBy(r => r.PatternStatus)
            .Select(g => new { Status = g.Key, Count = g.Count(), Reminders = g.ToList() })
            .ToList();
        
        foreach (var patternGroup in patternsByStatus)
        {
            Console.WriteLine($"\n{patternGroup.Status} Pattern ({patternGroup.Count} reminders):");
            
            var sampleReminders = patternGroup.Reminders.Take(5).ToList();
            foreach (var reminder in sampleReminders)
            {
                var occurrenceDisplay = reminder.Occurrence ?? "No occurrence pattern";
                var timeWindow = reminder.TimeWindowCenter ?? "N/A";
                var evidence = reminder.EvidenceCount;
                var customData = reminder.CustomData != null && reminder.CustomData.Count > 0 
                    ? string.Join(", ", reminder.CustomData.Select(kvp => $"{kvp.Key}={kvp.Value}"))
                    : "None";
                
                Console.WriteLine($"  {reminder.PersonId} -> {reminder.Action}");
                Console.WriteLine($"    Occurrence: {occurrenceDisplay}");
                Console.WriteLine($"    Time Window: {timeWindow}");
                Console.WriteLine($"    Evidence Count: {evidence}");
                Console.WriteLine($"    State Signals: {customData}");
                
                // Check for issues with occurrence display
                if (reminder.PatternStatus == "Flexible" && reminder.Occurrence != null)
                {
                    if (reminder.Occurrence.Contains("flexible timing") && !reminder.Occurrence.Contains("evening") 
                        && !reminder.Occurrence.Contains("morning") && !reminder.Occurrence.Contains("afternoon")
                        && !reminder.Occurrence.Contains("night"))
                    {
                        RecordArtifact("Occurrence Display", 
                            $"Flexible reminder {reminder.PersonId} -> {reminder.Action} doesn't show time bucket. " +
                            $"Current: {reminder.Occurrence}. Should show time bucket (morning/afternoon/evening/night)");
                    }
                }
                
                if (reminder.PatternStatus == "Daily" && reminder.Occurrence != null)
                {
                    if (!reminder.Occurrence.Contains("daily"))
                    {
                        RecordArtifact("Occurrence Display", 
                            $"Daily reminder {reminder.PersonId} -> {reminder.Action} doesn't clearly show 'daily'. " +
                            $"Current: {reminder.Occurrence}");
                    }
                }
                
                if (reminder.PatternStatus == "Weekly" && reminder.Occurrence != null)
                {
                    var dayNames = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
                    var hasDayName = dayNames.Any(day => reminder.Occurrence.Contains(day));
                    if (!hasDayName)
                    {
                        RecordArtifact("Occurrence Display", 
                            $"Weekly reminder {reminder.PersonId} -> {reminder.Action} doesn't show weekday name. " +
                            $"Current: {reminder.Occurrence}");
                    }
                }
                
                // Check if state signals are present but not shown in occurrence
                if (customData != "None" && reminder.Occurrence != null)
                {
                    var hasStateSignalInOccurrence = customData.Split(',').Any(signal => 
                        reminder.Occurrence.Contains(signal.Split('=')[0]) || 
                        reminder.Occurrence.Contains(signal.Split('=')[1]));
                    
                    if (!hasStateSignalInOccurrence)
                    {
                        RecordArtifact("State Signal Display", 
                            $"Reminder {reminder.PersonId} -> {reminder.Action} has state signals ({customData}) " +
                            $"but they're not shown in occurrence pattern: {reminder.Occurrence}");
                    }
                }
            }
        }
    }

    private async Task VerifyNoDuplicateReminders()
    {
        var reminders = await Context.ReminderCandidates
            .Where(r => _testPersonIds.Contains(r.PersonId))
            .ToListAsync();
        
        var duplicates = reminders
            .GroupBy(r => new { r.PersonId, r.SuggestedAction })
            .Where(g => g.Count() > 1)
            .ToList();
        
        if (duplicates.Any())
        {
            foreach (var dup in duplicates)
            {
                RecordArtifact("Duplicate Reminder", 
                    $"Found {dup.Count()} duplicate reminders for {dup.Key.PersonId} -> {dup.Key.SuggestedAction}");
            }
        }
        else
        {
            Console.WriteLine($"\n✓ No duplicate reminders found");
        }
    }

    private void PrintSummary(ReminderSnapshot reminderSnapshot, RoutineSnapshot routineSnapshot)
    {
        Console.WriteLine($"\n=== Test Summary ===");
        Console.WriteLine($"Total Events Created: {reminderSnapshot.EventCount}");
        Console.WriteLine($"Total General Reminders: {reminderSnapshot.ReminderCount}");
        Console.WriteLine($"Total Routines: {routineSnapshot.RoutineCount}");
        
        var remindersByPerson = reminderSnapshot.Reminders
            .GroupBy(r => r.PersonId)
            .Select(g => new { PersonId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();
        
        Console.WriteLine($"\nGeneral Reminders per Person:");
        foreach (var person in remindersByPerson)
        {
            Console.WriteLine($"  {person.PersonId}: {person.Count} reminders");
        }
        
        var routinesByPerson = routineSnapshot.Routines
            .GroupBy(r => r.PersonId)
            .Select(g => new { PersonId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();
        
        Console.WriteLine($"\nRoutines per Person:");
        foreach (var person in routinesByPerson)
        {
            Console.WriteLine($"  {person.PersonId}: {person.Count} routines");
        }
    }

    private void RecordArtifact(string category, string message)
    {
        _artifacts.Add(new TestArtifact
        {
            Category = category,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }

    private void PrintArtifacts()
    {
        Console.WriteLine($"\n=== Artifacts/Issues Found ===");
        
        if (_artifacts.Count == 0)
        {
            Console.WriteLine("✓ No artifacts or issues detected!");
        }
        else
        {
            Console.WriteLine($"Found {_artifacts.Count} artifact(s):\n");
            
            var grouped = _artifacts.GroupBy(a => a.Category);
            foreach (var group in grouped)
            {
                Console.WriteLine($"{group.Key} ({group.Count()}):");
                foreach (var artifact in group.Take(10)) // Limit to 10 per category
                {
                    Console.WriteLine($"  - {artifact.Message}");
                }
                if (group.Count() > 10)
                {
                    Console.WriteLine($"  ... and {group.Count() - 10} more");
                }
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

    private string GetDayType(DateTime time)
    {
        return time.DayOfWeek == DayOfWeek.Saturday || time.DayOfWeek == DayOfWeek.Sunday 
            ? "weekend" 
            : "weekday";
    }

    private async Task<ActionEventDto> CreateEventAsync(
        string personId,
        string actionType,
        DateTime timestamp,
        EventType eventType = EventType.Action,
        string timeBucket = "evening",
        string dayType = "weekday",
        string location = "home",
        Dictionary<string, string>? stateSignals = null)
    {
        var evt = new ActionEventDto
        {
            PersonId = personId,
            ActionType = actionType,
            TimestampUtc = timestamp,
            EventType = eventType,
            Context = new ActionContextDto
            {
                TimeBucket = timeBucket,
                DayType = dayType,
                Location = location,
                PresentPeople = new List<string> { personId },
                StateSignals = stateSignals ?? new Dictionary<string, string>()
            },
            CustomData = stateSignals // Store state signals in CustomData for reminder matching
        };

        var command = new IngestEventCommand { Event = evt };
        await EventHandler.Handle(command, CancellationToken.None);
        return evt;
    }
}

// Helper classes
public class ReminderSnapshot
{
    public int Day { get; set; }
    public int EventCount { get; set; }
    public int ReminderCount { get; set; }
    public List<ReminderInfo> Reminders { get; set; } = new();
}

public class ReminderInfo
{
    public string PersonId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public Guid Id { get; set; }
    public string? Occurrence { get; set; }
    public string PatternStatus { get; set; } = string.Empty;
    public string? TimeWindowCenter { get; set; }
    public int EvidenceCount { get; set; }
    public Dictionary<string, string>? CustomData { get; set; }
}

public class RoutineSnapshot
{
    public int Day { get; set; }
    public int EventCount { get; set; }
    public int RoutineCount { get; set; }
    public List<RoutineInfo> Routines { get; set; } = new();
}

public class RoutineInfo
{
    public string PersonId { get; set; } = string.Empty;
    public string IntentType { get; set; } = string.Empty;
    public Guid RoutineId { get; set; }
    public int ReminderCount { get; set; }
    public List<RoutineReminderInfo> Reminders { get; set; } = new();
}

public class RoutineReminderInfo
{
    public string Action { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

public class TestArtifact
{
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

