// Comprehensive test for general reminders with multiple users and random events
namespace AIPatterner.Tests.Integration;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public class GeneralRemindersStressTest : RealDatabaseTestBase
{
    private readonly Random _random;
    private readonly DateTime _testStartDate;
    private readonly List<string> _testPersonIds;
    private readonly List<string> _testActions;
    private readonly List<TestArtifact> _artifacts;

    public GeneralRemindersStressTest()
    {
        _random = new Random(12345); // Seeded for reproducibility
        _testStartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _testPersonIds = new List<string>();
        _testActions = new List<string> 
        { 
            "PlayMusic", "TurnOnLights", "AdjustAC", "LockDoors", "BrewCoffee",
            "OpenBlinds", "SetTemperature", "StartDishwasher", "WaterPlants", "FeedPet"
        };
        _artifacts = new List<TestArtifact>();
    }

    /// <summary>
    /// Override cleanup to preserve test data for manual verification.
    /// </summary>
    protected override void CleanupTestData()
    {
        // Skip cleanup for general_reminder_test_* data to preserve it for manual verification
        var testPersonIds = new[] { "user", "api_user", "api_test_user", "api_related_user", "api_feedback_user", 
            "feedback_user", "daily_user", "weekly_user", "user_a", "user_b", "user_c", "routine_test_user",
            "event_person", "reminder_person", "routine_person", "duplicate_test_person", "matched_user",
            "user_for_id", "testuser_dual", "testuser1", "testuser2", "adminuser", "comprehensive_test_user",
            "household_person_a", "household_person_b", "household_person_c" };

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

        // Clean up routines and routine reminders (excluding general_reminder_test_*)
        var routineTestPersonIds = Context.Routines
            .Where(r => (r.PersonId.StartsWith("routine_test_user") || r.PersonId.StartsWith("routine_person")) 
                     && !r.PersonId.StartsWith("general_reminder_test_"))
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
        
        // Note: general_reminder_test_* data is intentionally NOT cleaned up to allow manual verification
    }

    [Fact]
    public async Task GeneralReminders_MultipleUsers_RandomEvents_ComprehensiveTest()
    {
        // Arrange - Create 5 test users
        const int numUsers = 5;
        const int daysToSimulate = 14;
        const int eventsPerDayPerUser = 5;
        
        for (int i = 1; i <= numUsers; i++)
        {
            _testPersonIds.Add($"general_reminder_test_user_{i}");
        }

        Console.WriteLine($"=== Starting General Reminders Stress Test ===");
        Console.WriteLine($"Users: {numUsers}, Days: {daysToSimulate}, Events per day per user: {eventsPerDayPerUser}");
        Console.WriteLine($"Total events to create: {numUsers * daysToSimulate * eventsPerDayPerUser}\n");

        var eventCount = 0;
        var reminderSnapshots = new List<ReminderSnapshot>();

        // Act - Generate random events over time
        for (int day = 0; day < daysToSimulate; day++)
        {
            var dayTime = _testStartDate.AddDays(day);
            
            foreach (var personId in _testPersonIds)
            {
                for (int eventNum = 0; eventNum < eventsPerDayPerUser; eventNum++)
                {
                    // Generate random but realistic event
                    var hour = _random.Next(6, 23); // Between 6 AM and 11 PM
                    var minute = _random.Next(0, 60);
                    var eventTime = new DateTime(dayTime.Year, dayTime.Month, dayTime.Day, hour, minute, 0, DateTimeKind.Utc);
                    
                    // Add some random noise
                    eventTime = eventTime.AddMinutes(_random.Next(-15, 15));
                    
                    var action = _testActions[_random.Next(_testActions.Count)];
                    var timeBucket = GetTimeBucket(eventTime);
                    var dayType = GetDayType(eventTime);
                    
                    // Create event (only Action events, no StateChange for general reminders)
                    await CreateEventAsync(personId, action, eventTime, EventType.Action, 
                        timeBucket, dayType, "home");
                    
                    eventCount++;
                    
                    // Every 10 events, take a snapshot of reminders
                    if (eventCount % 10 == 0)
                    {
                        var snapshot = await TakeReminderSnapshot(day, eventCount);
                        reminderSnapshots.Add(snapshot);
                        
                        // Verify logic during execution
                        await VerifyReminderLogic(snapshot);
                    }
                }
            }
            
            Console.WriteLine($"Day {day + 1}/{daysToSimulate} completed. Total events: {eventCount}");
        }

        // Final verification
        Console.WriteLine($"\n=== Final Verification ===");
        var finalSnapshot = await TakeReminderSnapshot(daysToSimulate, eventCount);
        await VerifyReminderLogic(finalSnapshot);
        
        // Verify person isolation
        await VerifyPersonIsolation();
        
        // Verify confidence values
        await VerifyConfidenceValues();
        
        // Verify no duplicate reminders
        await VerifyNoDuplicateReminders();
        
        // Print summary
        PrintSummary(finalSnapshot);
        
        // Print artifacts
        PrintArtifacts();
    }

    private async Task<ReminderSnapshot> TakeReminderSnapshot(int day, int eventCount)
    {
        var reminders = await Context.ReminderCandidates
            .Where(r => _testPersonIds.Contains(r.PersonId))
            .ToListAsync();
        
        var events = await Context.ActionEvents
            .Where(e => _testPersonIds.Contains(e.PersonId))
            .CountAsync();
        
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
                Id = r.Id
            }).ToList()
        };
    }

    private async Task VerifyReminderLogic(ReminderSnapshot snapshot)
    {
        // Check 1: Each person should have reminders only for actions they performed
        var personEvents = await Context.ActionEvents
            .Where(e => _testPersonIds.Contains(e.PersonId))
            .GroupBy(e => new { e.PersonId, e.ActionType })
            .Select(g => new { g.Key.PersonId, g.Key.ActionType, Count = g.Count() })
            .ToListAsync();
        
        foreach (var reminder in snapshot.Reminders)
        {
            var hasEvent = personEvents.Any(e => 
                e.PersonId == reminder.PersonId && 
                e.ActionType == reminder.Action);
            
            if (!hasEvent)
            {
                RecordArtifact("Logic Error", 
                    $"Reminder exists for {reminder.PersonId} -> {reminder.Action} but no events found");
            }
        }
        
        // Check 2: Reminders should have valid confidence values
        foreach (var reminder in snapshot.Reminders)
        {
            if (reminder.Confidence < 0 || reminder.Confidence > 1.0)
            {
                RecordArtifact("Invalid Confidence", 
                    $"Reminder {reminder.PersonId} -> {reminder.Action} has invalid confidence: {reminder.Confidence}");
            }
        }
        
        // Check 3: Confidence should generally increase with more events
        // (This is a heuristic check - confidence may vary based on timing)
    }

    private async Task VerifyPersonIsolation()
    {
        var reminders = await Context.ReminderCandidates
            .Where(r => _testPersonIds.Contains(r.PersonId))
            .ToListAsync();
        
        // Check that reminders are properly scoped to their person
        foreach (var reminder in reminders)
        {
            if (!_testPersonIds.Contains(reminder.PersonId))
            {
                RecordArtifact("Person Isolation", 
                    $"Reminder {reminder.Id} has invalid personId: {reminder.PersonId}");
            }
        }
        
        // Verify no cross-contamination
        var personReminderCounts = reminders
            .GroupBy(r => r.PersonId)
            .Select(g => new { PersonId = g.Key, Count = g.Count() })
            .ToList();
        
        Console.WriteLine($"\nPerson Isolation Check:");
        foreach (var personCount in personReminderCounts)
        {
            var personReminders = reminders.Where(r => r.PersonId == personCount.PersonId).ToList();
            var allCorrect = personReminders.All(r => r.PersonId == personCount.PersonId);
            
            if (!allCorrect)
            {
                RecordArtifact("Person Isolation", 
                    $"Person {personCount.PersonId} has reminders with incorrect personId");
            }
            
            Console.WriteLine($"  {personCount.PersonId}: {personCount.Count} reminders");
        }
    }

    private async Task VerifyConfidenceValues()
    {
        var reminders = await Context.ReminderCandidates
            .Where(r => _testPersonIds.Contains(r.PersonId))
            .ToListAsync();
        
        var invalidConfidences = reminders
            .Where(r => r.Confidence < 0 || r.Confidence > 1.0)
            .ToList();
        
        if (invalidConfidences.Any())
        {
            RecordArtifact("Invalid Confidence", 
                $"Found {invalidConfidences.Count} reminders with invalid confidence values");
        }
        
        // Check confidence distribution
        var avgConfidence = reminders.Average(r => r.Confidence);
        var minConfidence = reminders.Min(r => r.Confidence);
        var maxConfidence = reminders.Max(r => r.Confidence);
        
        Console.WriteLine($"\nConfidence Statistics:");
        Console.WriteLine($"  Average: {avgConfidence:F3}");
        Console.WriteLine($"  Min: {minConfidence:F3}");
        Console.WriteLine($"  Max: {maxConfidence:F3}");
        
        if (minConfidence < 0 || maxConfidence > 1.0)
        {
            RecordArtifact("Confidence Range", 
                $"Confidence values out of valid range [0, 1.0]: min={minConfidence}, max={maxConfidence}");
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
            Console.WriteLine($"\n✓ No duplicate reminders found (each person+action has unique reminder)");
        }
    }

    private void PrintSummary(ReminderSnapshot finalSnapshot)
    {
        Console.WriteLine($"\n=== Test Summary ===");
        Console.WriteLine($"Total Events Created: {finalSnapshot.EventCount}");
        Console.WriteLine($"Total Reminders Created: {finalSnapshot.ReminderCount}");
        Console.WriteLine($"Average Reminders per User: {finalSnapshot.ReminderCount / (double)_testPersonIds.Count:F2}");
        
        var remindersByPerson = finalSnapshot.Reminders
            .GroupBy(r => r.PersonId)
            .Select(g => new { PersonId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();
        
        Console.WriteLine($"\nReminders per Person:");
        foreach (var person in remindersByPerson)
        {
            Console.WriteLine($"  {person.PersonId}: {person.Count} reminders");
        }
        
        var remindersByAction = finalSnapshot.Reminders
            .GroupBy(r => r.Action)
            .Select(g => new { Action = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();
        
        Console.WriteLine($"\nMost Common Actions:");
        foreach (var action in remindersByAction.Take(5))
        {
            Console.WriteLine($"  {action.Action}: {action.Count} reminders");
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
                foreach (var artifact in group)
                {
                    Console.WriteLine($"  - {artifact.Message}");
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
        string location = "home")
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
                StateSignals = new Dictionary<string, string>()
            }
        };

        var command = new IngestEventCommand { Event = evt };
        await EventHandler.Handle(command, CancellationToken.None);
        return evt;
    }
}

// Helper classes (moved to CombinedRemindersAndRoutinesTest.cs to avoid duplicates)

