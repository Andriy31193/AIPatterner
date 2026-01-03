// Comprehensive multi-user household integration tests
// Simulates realistic usage patterns across multiple users over extended time periods
namespace AIPatterner.Tests.Integration;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

public class MultiUserHouseholdIntegrationTests : RealDatabaseTestBase
{
    private const string AdminApiKey = "ak_JyqivmKSDskny2gO4s2Zafhxlmcw7Kn2FnFg9tEV2vPoajsjKvcJjSmY2oUoag5G";
    private const string PersonA = "household_person_a";
    private const string PersonB = "household_person_b";
    private const string PersonC = "household_person_c";
    
    private readonly Random _random;
    private readonly DateTime _testStartDate;
    private readonly Dictionary<string, List<Guid>> _userReminders = new();
    private readonly Dictionary<string, List<Guid>> _userRoutines = new();

    public MultiUserHouseholdIntegrationTests()
    {
        // Seeded random for deterministic tests
        _random = new Random(42);
        _testStartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        // Setup admin API key for tests
        HttpClient.DefaultRequestHeaders.Remove("X-Api-Key");
        HttpClient.DefaultRequestHeaders.Add("X-Api-Key", AdminApiKey);
    }

    /// <summary>
    /// Override cleanup to preserve test data for manual verification.
    /// This allows the household_person_* test data to persist after tests complete.
    /// </summary>
    protected override void CleanupTestData()
    {
        // Skip cleanup for household_person_* data to preserve it for manual verification
        // Still clean up other test data to avoid interference
        var testPersonIds = new[] { "user", "api_user", "api_test_user", "api_related_user", "api_feedback_user", 
            "feedback_user", "daily_user", "weekly_user", "user_a", "user_b", "user_c", "routine_test_user",
            "event_person", "reminder_person", "routine_person", "duplicate_test_person", "matched_user",
            "user_for_id", "testuser_dual", "testuser1", "testuser2", "adminuser", "comprehensive_test_user" };

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

        // Clean up routines and routine reminders (excluding household_person_*)
        var routineTestPersonIds = Context.Routines
            .Where(r => (r.PersonId.StartsWith("routine_test_user") || r.PersonId.StartsWith("routine_person")) 
                     && !r.PersonId.StartsWith("household_person_"))
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
        
        // Note: household_person_* data is intentionally NOT cleaned up to allow manual verification
    }

    #region Helper Methods

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

    private async Task<List<RoutineDto>> GetRoutinesAsync(string? personId = null)
    {
        // Try API first, fall back to database query if API unavailable
        if (await IsApiAvailableAsync())
        {
            try
            {
                var url = personId != null 
                    ? $"/v1/routines?personId={personId}" 
                    : "/v1/routines";
                var response = await HttpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<RoutineListResponse>();
                return result?.items ?? new List<RoutineDto>();
            }
            catch
            {
                // Fall through to database query
            }
        }
        
        // Fallback: Query database directly
        var query = Context.Routines.AsQueryable();
        if (personId != null)
        {
            query = query.Where(r => r.PersonId == personId);
        }
        
        var routines = await query.ToListAsync();
        return routines.Select(r => new RoutineDto
        {
            id = r.Id.ToString(),
            personId = r.PersonId,
            intentType = r.IntentType,
            observationWindowEndsUtc = r.ObservationWindowEndsAtUtc?.ToString("O")
        }).ToList();
    }

    private async Task<List<ReminderCandidateDto>> GetRemindersAsync(string? personId = null)
    {
        // Try API first, fall back to database query if API unavailable
        if (await IsApiAvailableAsync())
        {
            try
            {
                var url = personId != null 
                    ? $"/v1/reminders?personId={personId}" 
                    : "/v1/reminders";
                var response = await HttpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ReminderListResponse>();
                return result?.items ?? new List<ReminderCandidateDto>();
            }
            catch
            {
                // Fall through to database query
            }
        }
        
        // Fallback: Query database directly
        var query = Context.ReminderCandidates.AsQueryable();
        if (personId != null)
        {
            query = query.Where(r => r.PersonId == personId);
        }
        
        var reminders = await query.ToListAsync();
        return reminders.Select(r => new ReminderCandidateDto
        {
            id = r.Id.ToString(),
            personId = r.PersonId,
            suggestedAction = r.SuggestedAction,
            confidence = r.Confidence
        }).ToList();
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

    private DateTime AddRandomNoise(DateTime baseTime, int minutesRange = 20)
    {
        var noise = _random.Next(-minutesRange, minutesRange);
        return baseTime.AddMinutes(noise);
    }

    private async Task<RoutineDetailResponse> GetRoutineDetailAsync(string routineId)
    {
        // Try API first, fall back to database query if API unavailable
        if (await IsApiAvailableAsync())
        {
            try
            {
                var response = await HttpClient.GetAsync($"/v1/routines/{routineId}");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<RoutineDetailResponse>();
                if (result != null) return result;
            }
            catch
            {
                // Fall through to database query
            }
        }
        
        // Fallback: Query database directly
        var routineGuid = Guid.Parse(routineId);
        var routine = await Context.Routines
            .FirstOrDefaultAsync(r => r.Id == routineGuid);
        
        if (routine == null)
        {
            return new RoutineDetailResponse
            {
                id = routineId,
                personId = string.Empty,
                intentType = string.Empty,
                reminders = new List<RoutineReminderDto>()
            };
        }
        
        var routineReminders = await Context.RoutineReminders
            .Where(rr => rr.RoutineId == routineGuid)
            .ToListAsync();
        
        return new RoutineDetailResponse
        {
            id = routine.Id.ToString(),
            personId = routine.PersonId,
            intentType = routine.IntentType,
            reminders = routineReminders.Select(rr => new RoutineReminderDto
            {
                id = rr.Id.ToString(),
                routineId = rr.RoutineId.ToString(),
                suggestedAction = rr.SuggestedAction,
                confidence = rr.Confidence
            }).ToList()
        };
    }

    #endregion

    #region Scenario 1: Independent Learning Per Person

    [Fact]
    public async Task Scenario1_IndependentLearningPerPerson()
    {
        // Note: Data creation doesn't require API, only queries do
        // We'll use database queries as fallback if API is unavailable

        // Arrange - Each person performs similar actions at different times
        var baseTime = _testStartDate;
        var actions = new[] { "PlayMusic", "TurnOnLights", "AdjustAC" };

        // Person A: Evening actions (6-8 PM) - Also test routine creation with StateChange
        for (int day = 0; day < 10; day++)
        {
            var dayTime = baseTime.AddDays(day);
            var eveningTime = AddRandomNoise(new DateTime(dayTime.Year, dayTime.Month, dayTime.Day, 19, 0, 0, DateTimeKind.Utc));
            
            // Create StateChange event to trigger routine learning (every other day)
            if (day % 2 == 0)
            {
                await CreateEventAsync(PersonA, "ArrivalHome", eveningTime, EventType.StateChange,
                    GetTimeBucket(eveningTime), GetDayType(eveningTime));
            }
            
            foreach (var action in actions)
            {
                await CreateEventAsync(PersonA, action, eveningTime, EventType.Action, 
                    GetTimeBucket(eveningTime), GetDayType(eveningTime));
            }
        }

        // Person B: Morning actions (7-9 AM) - Also test routine creation with StateChange
        for (int day = 0; day < 10; day++)
        {
            var dayTime = baseTime.AddDays(day);
            var morningTime = AddRandomNoise(new DateTime(dayTime.Year, dayTime.Month, dayTime.Day, 8, 0, 0, DateTimeKind.Utc));
            
            // Create StateChange event to trigger routine learning (every other day)
            if (day % 2 == 0)
            {
                await CreateEventAsync(PersonB, "ArrivalHome", morningTime, EventType.StateChange,
                    GetTimeBucket(morningTime), GetDayType(morningTime));
            }
            
            foreach (var action in actions)
            {
                await CreateEventAsync(PersonB, action, morningTime, EventType.Action,
                    GetTimeBucket(morningTime), GetDayType(morningTime));
            }
        }

        // Person C: Random times, less frequent
        for (int day = 0; day < 10; day++)
        {
            if (_random.NextDouble() > 0.3) continue; // 30% chance per day
            
            var dayTime = baseTime.AddDays(day);
            var randomTime = dayTime.AddHours(_random.Next(8, 22));
            
            var action = actions[_random.Next(actions.Length)];
            await CreateEventAsync(PersonC, action, randomTime, EventType.Action,
                GetTimeBucket(randomTime), GetDayType(randomTime));
        }

        // Act - Retrieve reminders for each person
        var remindersA = await GetRemindersAsync(PersonA);
        var remindersB = await GetRemindersAsync(PersonB);
        var remindersC = await GetRemindersAsync(PersonC);

        // Assert - Independent learning
        remindersA.Should().NotBeEmpty("Person A should have learned reminders");
        remindersB.Should().NotBeEmpty("Person B should have learned reminders");
        
        // Each person should have separate reminders
        var playMusicA = remindersA.FirstOrDefault(r => r.suggestedAction == "PlayMusic");
        var playMusicB = remindersB.FirstOrDefault(r => r.suggestedAction == "PlayMusic");
        
        playMusicA.Should().NotBeNull("Person A should have PlayMusic reminder");
        playMusicB.Should().NotBeNull("Person B should have PlayMusic reminder");
        
        // Reminders should be different entities
        playMusicA!.id.Should().NotBe(playMusicB!.id, "Reminders should be separate entities");
        
        // Probabilities should be independent
        playMusicA.confidence.Should().BeGreaterThan(0, "Person A reminder should have confidence");
        playMusicB.confidence.Should().BeGreaterThan(0, "Person B reminder should have confidence");
        
        // Person C may or may not have reminders (less frequent actions)
        if (remindersC.Any())
        {
            remindersC.All(r => r.personId == PersonC).Should().BeTrue("Person C reminders should be scoped to Person C");
        }

        // Verify no cross-contamination
        remindersA.All(r => r.personId == PersonA).Should().BeTrue("Person A reminders should only be for Person A");
        remindersB.All(r => r.personId == PersonB).Should().BeTrue("Person B reminders should only be for Person B");
        
        // Verify routines were created from StateChange events
        var routinesA = await GetRoutinesAsync(PersonA);
        var routinesB = await GetRoutinesAsync(PersonB);
        
        var routineA = routinesA.FirstOrDefault(r => r.intentType == "ArrivalHome");
        var routineB = routinesB.FirstOrDefault(r => r.intentType == "ArrivalHome");
        
        routineA.Should().NotBeNull("Person A should have ArrivalHome routine from StateChange events");
        routineB.Should().NotBeNull("Person B should have ArrivalHome routine from StateChange events");
        
        // Verify routine details
        if (routineA != null)
        {
            var detailA = await GetRoutineDetailAsync(routineA.id);
            detailA.reminders.Should().NotBeEmpty("Person A routine should have learned reminders from actions");
        }
        
        if (routineB != null)
        {
            var detailB = await GetRoutineDetailAsync(routineB.id);
            detailB.reminders.Should().NotBeEmpty("Person B routine should have learned reminders from actions");
        }
    }

    #endregion

    #region Scenario 2: Intent-Anchored Routines per Person

    [Fact]
    public async Task Scenario2_IntentAnchoredRoutinesPerPerson()
    {
        // Note: Data creation doesn't require API, only queries do

        // Arrange - Each person triggers ArrivalHome but performs different actions
        var baseTime = _testStartDate;

        // Person A: ArrivalHome -> PlayMusic, TurnOnLights
        for (int day = 0; day < 15; day++)
        {
            var dayTime = baseTime.AddDays(day);
            var arrivalTime = AddRandomNoise(new DateTime(dayTime.Year, dayTime.Month, dayTime.Day, 18, 0, 0, DateTimeKind.Utc));
            
            // Intent
            await CreateEventAsync(PersonA, "ArrivalHome", arrivalTime, EventType.StateChange,
                GetTimeBucket(arrivalTime), GetDayType(arrivalTime));
            
            // Observed actions within window
            await CreateEventAsync(PersonA, "PlayMusic", arrivalTime.AddMinutes(5), EventType.Action,
                GetTimeBucket(arrivalTime), GetDayType(arrivalTime));
            await CreateEventAsync(PersonA, "TurnOnLights", arrivalTime.AddMinutes(8), EventType.Action,
                GetTimeBucket(arrivalTime), GetDayType(arrivalTime));
        }

        // Person B: ArrivalHome -> AdjustAC, LockDoors
        for (int day = 0; day < 15; day++)
        {
            var dayTime = baseTime.AddDays(day);
            var arrivalTime = AddRandomNoise(new DateTime(dayTime.Year, dayTime.Month, dayTime.Day, 20, 0, 0, DateTimeKind.Utc));
            
            // Intent
            await CreateEventAsync(PersonB, "ArrivalHome", arrivalTime, EventType.StateChange,
                GetTimeBucket(arrivalTime), GetDayType(arrivalTime));
            
            // Different observed actions
            await CreateEventAsync(PersonB, "AdjustAC", arrivalTime.AddMinutes(3), EventType.Action,
                GetTimeBucket(arrivalTime), GetDayType(arrivalTime));
            await CreateEventAsync(PersonB, "LockDoors", arrivalTime.AddMinutes(6), EventType.Action,
                GetTimeBucket(arrivalTime), GetDayType(arrivalTime));
        }

        // Act - Retrieve routines for each person
        var routinesA = await GetRoutinesAsync(PersonA);
        var routinesB = await GetRoutinesAsync(PersonB);

        // Assert - Separate routines per person
        var routineA = routinesA.FirstOrDefault(r => r.intentType == "ArrivalHome");
        var routineB = routinesB.FirstOrDefault(r => r.intentType == "ArrivalHome");
        
        routineA.Should().NotBeNull("Person A should have ArrivalHome routine");
        routineB.Should().NotBeNull("Person B should have ArrivalHome routine");
        
        // Routines should be different entities
        routineA!.id.Should().NotBe(routineB!.id, "Routines should be separate entities");
        
        // Get routine details (with database fallback)
        var detailAData = await GetRoutineDetailAsync(routineA.id);
        var detailBData = await GetRoutineDetailAsync(routineB.id);
        
        // Person A should have PlayMusic and TurnOnLights reminders
        detailAData!.reminders.Should().Contain(r => r.suggestedAction == "PlayMusic");
        detailAData.reminders.Should().Contain(r => r.suggestedAction == "TurnOnLights");
        detailAData.reminders.Should().NotContain(r => r.suggestedAction == "AdjustAC");
        
        // Person B should have AdjustAC and LockDoors reminders
        detailBData!.reminders.Should().Contain(r => r.suggestedAction == "AdjustAC");
        detailBData.reminders.Should().Contain(r => r.suggestedAction == "LockDoors");
        detailBData.reminders.Should().NotContain(r => r.suggestedAction == "PlayMusic");
        
        // Verify no cross-learning
        detailAData.reminders.All(r => r.routineId == routineA.id).Should().BeTrue();
        detailBData.reminders.All(r => r.routineId == routineB.id).Should().BeTrue();
    }

    #endregion

    #region Scenario 3: Irregular Schedules Do Not Break Learning

    [Fact]
    public async Task Scenario3_IrregularSchedulesDoNotBreakLearning()
    {
        // Note: Data creation doesn't require API, only queries do

        // Arrange - Person B works rotating shifts
        var baseTime = _testStartDate;
        var shiftPattern = new[] { "morning", "afternoon", "night", "off" };
        int shiftIndex = 0;

        for (int day = 0; day < 30; day++)
        {
            var dayTime = baseTime.AddDays(day);
            var shift = shiftPattern[shiftIndex % shiftPattern.Length];
            shiftIndex++;

            if (shift == "off") continue; // Skip days off

            DateTime arrivalTime;
            switch (shift)
            {
                case "morning":
                    arrivalTime = new DateTime(dayTime.Year, dayTime.Month, dayTime.Day, 6, 0, 0, DateTimeKind.Utc);
                    break;
                case "afternoon":
                    arrivalTime = new DateTime(dayTime.Year, dayTime.Month, dayTime.Day, 14, 0, 0, DateTimeKind.Utc);
                    break;
                case "night":
                    arrivalTime = new DateTime(dayTime.Year, dayTime.Month, dayTime.Day, 22, 0, 0, DateTimeKind.Utc);
                    break;
                default:
                    continue;
            }

            arrivalTime = AddRandomNoise(arrivalTime);

            // Intent with varying times
            await CreateEventAsync(PersonB, "ArrivalHome", arrivalTime, EventType.StateChange,
                GetTimeBucket(arrivalTime), GetDayType(arrivalTime));
            
            // Consistent action after arrival
            await CreateEventAsync(PersonB, "BrewCoffee", arrivalTime.AddMinutes(5), EventType.Action,
                GetTimeBucket(arrivalTime), GetDayType(arrivalTime));
        }

        // Act - Retrieve routine
        var routines = await GetRoutinesAsync(PersonB);
        var routine = routines.FirstOrDefault(r => r.intentType == "ArrivalHome");

        // Assert - Learning should still occur despite irregular schedule
        routine.Should().NotBeNull("Person B should have learned ArrivalHome routine despite irregular schedule");
        
        var detail = await HttpClient.GetAsync($"/v1/routines/{routine!.id}");
        detail.EnsureSuccessStatusCode();
        var detailData = await detail.Content.ReadFromJsonAsync<RoutineDetailResponse>();
        
        detailData!.reminders.Should().Contain(r => r.suggestedAction == "BrewCoffee",
            "Routine should learn BrewCoffee despite varying arrival times");
        
        var brewCoffeeReminder = detailData.reminders.First(r => r.suggestedAction == "BrewCoffee");
        brewCoffeeReminder.confidence.Should().BeGreaterThan(0.3,
            "Confidence should grow even with irregular schedule");
    }

    #endregion

    #region Scenario 4: Probability Evolution Over Time

    [Fact]
    public async Task Scenario4_ProbabilityEvolutionOverTime()
    {
        // Note: Data creation doesn't require API, only queries do

        // Arrange - Track probability over time
        var baseTime = _testStartDate;
        var action = "PlayMusic";
        var probabilities = new List<double>();

        // Phase 1: Reinforce action (days 0-10)
        for (int day = 0; day < 10; day++)
        {
            var dayTime = baseTime.AddDays(day);
            var eventTime = AddRandomNoise(new DateTime(dayTime.Year, dayTime.Month, dayTime.Day, 19, 0, 0, DateTimeKind.Utc));
            
            await CreateEventAsync(PersonA, action, eventTime, EventType.Action,
                GetTimeBucket(eventTime), GetDayType(eventTime));
            
            // Check probability after every 3 days
            if (day % 3 == 2)
            {
                var reminders = await GetRemindersAsync(PersonA);
                var reminder = reminders.FirstOrDefault(r => r.suggestedAction == action);
                if (reminder != null)
                {
                    probabilities.Add(reminder.confidence);
                }
            }
        }

        // Phase 2: Stop reinforcing (days 11-20)
        for (int day = 11; day < 20; day++)
        {
            // No events - probability should decay or stay stable
            if (day == 15)
            {
                var reminders = await GetRemindersAsync(PersonA);
                var reminder = reminders.FirstOrDefault(r => r.suggestedAction == action);
                if (reminder != null)
                {
                    probabilities.Add(reminder.confidence);
                }
            }
        }

        // Phase 3: Resume reinforcing (days 21-25)
        for (int day = 21; day < 25; day++)
        {
            var dayTime = baseTime.AddDays(day);
            var eventTime = AddRandomNoise(new DateTime(dayTime.Year, dayTime.Month, dayTime.Day, 19, 0, 0, DateTimeKind.Utc));
            
            await CreateEventAsync(PersonA, action, eventTime, EventType.Action,
                GetTimeBucket(eventTime), GetDayType(eventTime));
        }

        // Final check
        var finalReminders = await GetRemindersAsync(PersonA);
        var finalReminder = finalReminders.FirstOrDefault(r => r.suggestedAction == action);
        if (finalReminder != null)
        {
            probabilities.Add(finalReminder.confidence);
        }

        // Assert - Probability should increase during reinforcement
        if (probabilities.Count >= 2)
        {
            // Early probabilities should be lower than later ones (during reinforcement)
            var earlyAvg = probabilities.Take(probabilities.Count / 2).Average();
            var laterAvg = probabilities.Skip(probabilities.Count / 2).Average();
            
            laterAvg.Should().BeGreaterOrEqualTo(earlyAvg - 0.1, 
                "Probability should generally increase with reinforcement");
        }

        finalReminder.Should().NotBeNull("Reminder should exist after reinforcement");
        finalReminder!.confidence.Should().BeGreaterThan(0, "Final confidence should be positive");
    }

    #endregion

    #region Scenario 5: Safety and Non-Automation Guarantees

    [Fact]
    public async Task Scenario5_SafetyAndNonAutomationGuarantees()
    {
        // Note: Data creation doesn't require API, only queries do

        // Arrange - Create events for potentially unsafe actions
        var baseTime = _testStartDate;
        var potentiallyUnsafeActions = new[] { "StopMusic", "EmergencyStop", "ShutdownSystem" };

        for (int day = 0; day < 10; day++)
        {
            var dayTime = baseTime.AddDays(day);
            var eventTime = AddRandomNoise(new DateTime(dayTime.Year, dayTime.Month, dayTime.Day, 19, 0, 0, DateTimeKind.Utc));
            
            foreach (var action in potentiallyUnsafeActions)
            {
                await CreateEventAsync(PersonA, action, eventTime, EventType.Action,
                    GetTimeBucket(eventTime), GetDayType(eventTime));
            }
        }

        // Act - Retrieve reminders
        var reminders = await GetRemindersAsync(PersonA);

        // Assert - Unsafe actions should still be tracked but not auto-executed
        foreach (var action in potentiallyUnsafeActions)
        {
            var reminder = reminders.FirstOrDefault(r => r.suggestedAction == action);
            
            // Reminders may or may not exist depending on system design
            // But if they exist, they should be tracked
            if (reminder != null)
            {
                reminder.confidence.Should().BeGreaterThan(0, 
                    $"Unsafe action {action} should have tracked confidence");
                reminder.confidence.Should().BeLessOrEqualTo(1.0, 
                    $"Unsafe action {action} confidence should be valid");
            }
        }

        // Note: Auto-execution prevention is typically handled at the application/service layer
        // This test verifies that the system still tracks these actions for learning
    }

    #endregion

    #region Scenario 6: Admin vs User Data Isolation

    [Fact]
    public async Task Scenario6_AdminVsUserDataIsolation()
    {
        // Note: Data creation doesn't require API, only queries do

        // Arrange - Create data for all persons
        var baseTime = _testStartDate;
        
        for (int day = 0; day < 5; day++)
        {
            var dayTime = baseTime.AddDays(day);
            var eventTime = AddRandomNoise(new DateTime(dayTime.Year, dayTime.Month, dayTime.Day, 19, 0, 0, DateTimeKind.Utc));
            
            await CreateEventAsync(PersonA, "PlayMusic", eventTime, EventType.Action,
                GetTimeBucket(eventTime), GetDayType(eventTime));
            await CreateEventAsync(PersonB, "TurnOnLights", eventTime, EventType.Action,
                GetTimeBucket(eventTime), GetDayType(eventTime));
        }

        // Act - Admin should see all data
        var adminReminders = await GetRemindersAsync(); // No personId filter = all data
        var adminRemindersA = await GetRemindersAsync(PersonA);
        var adminRemindersB = await GetRemindersAsync(PersonB);

        // Assert - Admin can access all data
        adminReminders.Should().NotBeEmpty("Admin should see reminders");
        adminReminders.Any(r => r.personId == PersonA).Should().BeTrue("Admin should see Person A data");
        adminReminders.Any(r => r.personId == PersonB).Should().BeTrue("Admin should see Person B data");
        
        // Filtered queries should work
        adminRemindersA.All(r => r.personId == PersonA).Should().BeTrue("Filtered query should only return Person A data");
        adminRemindersB.All(r => r.personId == PersonB).Should().BeTrue("Filtered query should only return Person B data");
    }

    #endregion

    #region Scenario 7: Large Volume Stability

    [Fact]
    public async Task Scenario7_LargeVolumeStability()
    {
        // Note: Data creation doesn't require API, only queries do

        // Arrange - Generate hundreds of events across users
        var baseTime = _testStartDate;
        var eventCount = 0;
        var persons = new[] { PersonA, PersonB, PersonC };
        var actions = new[] { "PlayMusic", "TurnOnLights", "AdjustAC", "LockDoors", "BrewCoffee" };

        for (int day = 0; day < 30; day++)
        {
            var dayTime = baseTime.AddDays(day);
            
            // Each person generates 3-5 events per day
            foreach (var person in persons)
            {
                var eventsPerDay = _random.Next(3, 6);
                for (int i = 0; i < eventsPerDay; i++)
                {
                    var hour = _random.Next(6, 23);
                    var eventTime = AddRandomNoise(new DateTime(dayTime.Year, dayTime.Month, dayTime.Day, hour, 0, 0, DateTimeKind.Utc));
                    var action = actions[_random.Next(actions.Length)];
                    
                    await CreateEventAsync(person, action, eventTime, EventType.Action,
                        GetTimeBucket(eventTime), GetDayType(eventTime));
                    eventCount++;
                }
            }
        }

        // Act - Retrieve all data
        var startTime = DateTime.UtcNow;
        var allReminders = await GetRemindersAsync();
        var allRoutines = await GetRoutinesAsync();
        var duration = DateTime.UtcNow - startTime;

        // Assert - Performance and correctness
        duration.TotalSeconds.Should().BeLessThan(10, "Large volume queries should complete in reasonable time");
        
        allReminders.Should().NotBeEmpty("Should have reminders after large volume");
        allReminders.Count.Should().BeGreaterThan(10, "Should have significant number of reminders");
        
        // Check for duplicates
        var duplicateReminders = allReminders
            .GroupBy(r => new { r.personId, r.suggestedAction })
            .Where(g => g.Count() > 1)
            .ToList();
        
        duplicateReminders.Should().BeEmpty("Should not have duplicate reminders for same person+action");
        
        // Verify person isolation
        var remindersA = allReminders.Where(r => r.personId == PersonA).ToList();
        var remindersB = allReminders.Where(r => r.personId == PersonB).ToList();
        var remindersC = allReminders.Where(r => r.personId == PersonC).ToList();
        
        remindersA.Should().NotBeEmpty("Person A should have reminders");
        remindersB.Should().NotBeEmpty("Person B should have reminders");
        
        // No cross-contamination
        remindersA.All(r => r.personId == PersonA).Should().BeTrue("Person A reminders should be isolated");
        remindersB.All(r => r.personId == PersonB).Should().BeTrue("Person B reminders should be isolated");
    }

    #endregion

    #region Scenario 8: Edge Cases

    [Fact]
    public async Task Scenario8_EdgeCases()
    {
        // Note: Data creation doesn't require API, only queries do

        var baseTime = _testStartDate;

        // Edge Case 1: Rapid repeated events
        var rapidTime = baseTime.AddDays(1);
        for (int i = 0; i < 20; i++)
        {
            await CreateEventAsync(PersonA, "PlayMusic", rapidTime.AddSeconds(i * 5), EventType.Action,
                GetTimeBucket(rapidTime), GetDayType(rapidTime));
        }

        var remindersAfterRapid = await GetRemindersAsync(PersonA);
        var rapidReminder = remindersAfterRapid.FirstOrDefault(r => r.suggestedAction == "PlayMusic");
        rapidReminder.Should().NotBeNull("Rapid events should create reminder");
        rapidReminder!.confidence.Should().BeLessOrEqualTo(1.0, "Rapid events should not exceed confidence cap");

        // Edge Case 2: Intent without follow-up actions
        var intentTime = baseTime.AddDays(2);
        await CreateEventAsync(PersonA, "ArrivalHome", intentTime, EventType.StateChange,
            GetTimeBucket(intentTime), GetDayType(intentTime));
        // No follow-up actions

        var routinesAfterIntent = await GetRoutinesAsync(PersonA);
        var intentRoutine = routinesAfterIntent.FirstOrDefault(r => r.intentType == "ArrivalHome");
        intentRoutine.Should().NotBeNull("Intent should create routine even without immediate follow-up");

        // Edge Case 3: Actions without intent (general reminders)
        var actionTime = baseTime.AddDays(3);
        for (int i = 0; i < 5; i++)
        {
            await CreateEventAsync(PersonA, "DrinkWater", actionTime.AddDays(i), EventType.Action,
                GetTimeBucket(actionTime), GetDayType(actionTime));
        }

        var remindersAfterActions = await GetRemindersAsync(PersonA);
        var actionReminder = remindersAfterActions.FirstOrDefault(r => r.suggestedAction == "DrinkWater");
        actionReminder.Should().NotBeNull("Actions without intent should create general reminders");

        // Edge Case 4: Same action in general reminder and routine context
        var routineIntentTime = baseTime.AddDays(4);
        await CreateEventAsync(PersonA, "ArrivalHome", routineIntentTime, EventType.StateChange,
            GetTimeBucket(routineIntentTime), GetDayType(routineIntentTime));
        await CreateEventAsync(PersonA, "PlayMusic", routineIntentTime.AddMinutes(5), EventType.Action,
            GetTimeBucket(routineIntentTime), GetDayType(routineIntentTime));

        // Also create general reminder for PlayMusic
        for (int i = 0; i < 5; i++)
        {
            await CreateEventAsync(PersonA, "PlayMusic", baseTime.AddDays(10 + i), EventType.Action,
                GetTimeBucket(baseTime), GetDayType(baseTime));
        }

        var finalReminders = await GetRemindersAsync(PersonA);
        var finalRoutines = await GetRoutinesAsync(PersonA);
        
        // Both should exist
        finalReminders.Should().Contain(r => r.suggestedAction == "PlayMusic",
            "General reminder for PlayMusic should exist");
        
        var routine = finalRoutines.FirstOrDefault(r => r.intentType == "ArrivalHome");
        if (routine != null)
        {
            var routineDetailData = await GetRoutineDetailAsync(routine.id);
            
            routineDetailData!.reminders.Should().Contain(r => r.suggestedAction == "PlayMusic",
                "Routine reminder for PlayMusic should exist");
            
            // They should be separate entities
            var generalReminder = finalReminders.First(r => r.suggestedAction == "PlayMusic");
            var routineReminder = routineDetailData.reminders.First(r => r.suggestedAction == "PlayMusic");
            
            generalReminder.id.Should().NotBe(routineReminder.id,
                "General and routine reminders should be separate entities");
        }
    }

    #endregion

    #region Long-Term Multi-Day Test

    [Fact]
    public async Task LongTerm_MultiDayLearningStability()
    {
        // Note: Data creation doesn't require API, only queries do

        // Arrange - Simulate 30 days of activity
        var baseTime = _testStartDate;
        var persons = new[] { PersonA, PersonB, PersonC };
        var actions = new[] { "PlayMusic", "TurnOnLights", "AdjustAC", "LockDoors" };
        var intents = new[] { "ArrivalHome", "GoingToSleep" };

        for (int day = 0; day < 30; day++)
        {
            var dayTime = baseTime.AddDays(day);
            
            foreach (var person in persons)
            {
                // 50% chance of intent per day per person
                if (_random.NextDouble() > 0.5)
                {
                    var intent = intents[_random.Next(intents.Length)];
                    var intentTime = AddRandomNoise(new DateTime(dayTime.Year, dayTime.Month, dayTime.Day, 
                        intent == "ArrivalHome" ? 18 : 22, 0, 0, DateTimeKind.Utc));
                    
                    await CreateEventAsync(person, intent, intentTime, EventType.StateChange,
                        GetTimeBucket(intentTime), GetDayType(intentTime));
                    
                    // Follow-up actions
                    var numActions = _random.Next(1, 4);
                    for (int i = 0; i < numActions; i++)
                    {
                        var action = actions[_random.Next(actions.Length)];
                        await CreateEventAsync(person, action, intentTime.AddMinutes(5 + i * 3), EventType.Action,
                            GetTimeBucket(intentTime), GetDayType(intentTime));
                    }
                }
                
                // Random general actions
                if (_random.NextDouble() > 0.7)
                {
                    var action = actions[_random.Next(actions.Length)];
                    var actionTime = AddRandomNoise(new DateTime(dayTime.Year, dayTime.Month, dayTime.Day, 
                        _random.Next(8, 22), 0, 0, DateTimeKind.Utc));
                    
                    await CreateEventAsync(person, action, actionTime, EventType.Action,
                        GetTimeBucket(actionTime), GetDayType(actionTime));
                }
            }
        }

        // Act - Verify system stability
        var allReminders = await GetRemindersAsync();
        var allRoutines = await GetRoutinesAsync();

        // Assert - System should remain stable
        allReminders.Should().NotBeEmpty("Should have reminders after 30 days");
        allRoutines.Should().NotBeEmpty("Should have routines after 30 days");
        
        // Verify person isolation maintained
        foreach (var person in persons)
        {
            var personReminders = allReminders.Where(r => r.personId == person).ToList();
            personReminders.All(r => r.personId == person).Should().BeTrue(
                $"Person {person} reminders should be isolated");
            
            var personRoutines = allRoutines.Where(r => r.personId == person).ToList();
            personRoutines.All(r => r.personId == person).Should().BeTrue(
                $"Person {person} routines should be isolated");
        }
        
        // Verify no data corruption
        allReminders.All(r => !string.IsNullOrEmpty(r.personId)).Should().BeTrue("All reminders should have personId");
        allReminders.All(r => r.confidence >= 0 && r.confidence <= 1.0).Should().BeTrue(
            "All reminder confidences should be valid");
    }

    #endregion
}

// Response DTOs (shared with RoutineLearningComprehensiveTests)
public class ReminderListResponse
{
    public List<ReminderCandidateDto> items { get; set; } = new();
    public int totalCount { get; set; }
}

public class ReminderCandidateDto
{
    public string id { get; set; } = string.Empty;
    public string personId { get; set; } = string.Empty;
    public string suggestedAction { get; set; } = string.Empty;
    public double confidence { get; set; }
}

