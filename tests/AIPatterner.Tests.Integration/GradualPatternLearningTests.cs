// Comprehensive integration tests for gradual pattern learning behavior
// These tests verify that reminders learn patterns gradually, like humans,
// without jumping to conclusions about daily/weekly patterns too early.
namespace AIPatterner.Tests.Integration;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using Xunit;

public class GradualPatternLearningTests : RealDatabaseTestBase
{
    private const string TestPersonId = "user_pattern_learning";
    private const string TestAction = "PlayMusic";
    private const string TestApiKey = "ak_xipo5DFvyjOab4Tj6EM3RHl9Ot4G1TjUJRkXXSVXkYainQ2H0v7QCDMHsdSODD88";

    public GradualPatternLearningTests()
    {
        // Override API key header with the provided test key
        HttpClient.DefaultRequestHeaders.Remove("X-Api-Key");
        HttpClient.DefaultRequestHeaders.Add("X-Api-Key", TestApiKey);
    }

    /// <summary>
    /// Helper method to create an event via the handler
    /// </summary>
    private async Task<IngestEventResponse> CreateEventAsync(
        string personId,
        string actionType,
        DateTime timestampUtc,
        double probabilityValue = 0.1,
        ProbabilityAction probabilityAction = ProbabilityAction.Increase)
    {
        var eventDto = new ActionEventDto
        {
            PersonId = personId,
            ActionType = actionType,
            TimestampUtc = timestampUtc,
            Context = new ActionContextDto
            {
                TimeBucket = GetTimeBucket(timestampUtc),
                DayType = GetDayType(timestampUtc)
            },
            ProbabilityValue = probabilityValue,
            ProbabilityAction = probabilityAction
        };

        var command = new IngestEventCommand { Event = eventDto };
        return await EventHandler.Handle(command, CancellationToken.None);
    }

    /// <summary>
    /// Helper method to create an event via API
    /// </summary>
    private async Task<IngestEventResponse?> CreateEventViaApiAsync(
        string personId,
        string actionType,
        DateTime timestampUtc,
        double probabilityValue = 0.1,
        ProbabilityAction probabilityAction = ProbabilityAction.Increase)
    {
        if (!await IsApiAvailableAsync())
        {
            return null;
        }

        var eventDto = new ActionEventDto
        {
            PersonId = personId,
            ActionType = actionType,
            TimestampUtc = timestampUtc,
            Context = new ActionContextDto
            {
                TimeBucket = GetTimeBucket(timestampUtc),
                DayType = GetDayType(timestampUtc)
            },
            ProbabilityValue = probabilityValue,
            ProbabilityAction = probabilityAction
        };

        var response = await HttpClient.PostAsJsonAsync("/v1/events", eventDto);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted);
        return await response.Content.ReadFromJsonAsync<IngestEventResponse>();
    }

    /// <summary>
    /// Helper method to get all reminders for a person
    /// </summary>
    private async Task<List<ReminderCandidate>> GetRemindersForPersonAsync(string personId)
    {
        return await Context.ReminderCandidates
            .Where(r => r.PersonId == personId)
            .ToListAsync();
    }

    /// <summary>
    /// Helper method to get a reminder by ID
    /// </summary>
    private async Task<ReminderCandidate?> GetReminderAsync(Guid reminderId)
    {
        return await ReminderRepository.GetByIdAsync(reminderId, CancellationToken.None);
    }

    private string GetTimeBucket(DateTime timestamp)
    {
        var hour = timestamp.Hour;
        return hour switch
        {
            >= 6 and < 12 => "morning",
            >= 12 and < 17 => "afternoon",
            >= 17 and < 22 => "evening",
            _ => "night"
        };
    }

    private string GetDayType(DateTime timestamp)
    {
        return timestamp.DayOfWeek == DayOfWeek.Saturday || timestamp.DayOfWeek == DayOfWeek.Sunday
            ? "weekend"
            : "weekday";
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

    #region Scenario 1: First Event Creates Flexible Reminder

    [Fact]
    public async Task Scenario1_FirstEvent_CreatesFlexibleReminder()
    {
        // Arrange
        var timestamp = new DateTime(2026, 1, 2, 20, 0, 0, DateTimeKind.Utc); // Friday 20:00

        // Act
        var response = await CreateEventAsync(TestPersonId, TestAction, timestamp);

        // Assert
        response.EventId.Should().NotBeEmpty();
        response.RelatedReminderId.Should().NotBeNull();

        var reminders = await GetRemindersForPersonAsync(TestPersonId);
        reminders.Should().HaveCount(1);

        var reminder = await GetReminderAsync(response.RelatedReminderId!.Value);
        reminder.Should().NotBeNull();
        reminder!.PersonId.Should().Be(TestPersonId);
        reminder.SuggestedAction.Should().Be(TestAction);
        
        // Time window should be set to approximately 20:00
        reminder.TimeWindowCenter.Should().NotBeNull();
        var timeWindow = reminder.TimeWindowCenter!.Value;
        timeWindow.Hours.Should().Be(20);
        timeWindow.Minutes.Should().BeOneOf(0, 1); // Allow for slight rounding

        // Probability should be default (from configuration, typically 0.5)
        reminder.Confidence.Should().BeGreaterThan(0.0);

        // Pattern should be Unknown initially
        reminder.PatternInferenceStatus.Should().Be(PatternInferenceStatus.Unknown);
        reminder.EvidenceCount.Should().Be(1);

        // Occurrence should be null or indicate flexible/unknown pattern
        if (!string.IsNullOrEmpty(reminder.Occurrence))
        {
            reminder.Occurrence.Should().ContainAny(new[] { "flexible", "Unknown", "around" });
        }

        // No weekday should be inferred
        reminder.InferredWeekday.Should().BeNull();
    }

    #endregion

    #region Scenario 2: Next-Day Same Time Reinforces Existing Reminder

    [Fact]
    public async Task Scenario2_NextDaySameTime_ReinforcesExistingReminder()
    {
        // Arrange - Create first event (Friday 20:00)
        var firstTimestamp = new DateTime(2026, 1, 2, 20, 0, 0, DateTimeKind.Utc); // Friday
        var firstResponse = await CreateEventAsync(TestPersonId, TestAction, firstTimestamp);
        var firstReminderId = firstResponse.RelatedReminderId!.Value;
        var firstReminder = await GetReminderAsync(firstReminderId);
        var initialConfidence = firstReminder!.Confidence;
        var initialEvidenceCount = firstReminder.EvidenceCount;

        // Act - Create second event (Saturday 19:50)
        var secondTimestamp = new DateTime(2026, 1, 3, 19, 50, 0, DateTimeKind.Utc); // Saturday
        var secondResponse = await CreateEventAsync(TestPersonId, TestAction, secondTimestamp);

        // Assert - Should match the same reminder
        secondResponse.RelatedReminderId.Should().Be(firstReminderId);

        var reminders = await GetRemindersForPersonAsync(TestPersonId);
        reminders.Should().HaveCount(1, "Should still have exactly one reminder");

        var updatedReminder = await GetReminderAsync(firstReminderId);
        updatedReminder.Should().NotBeNull();

        // Probability should have increased
        updatedReminder!.Confidence.Should().BeGreaterThan(initialConfidence);

        // Evidence count should have increased
        updatedReminder.EvidenceCount.Should().Be(initialEvidenceCount + 1);

        // Pattern should still not be Weekly (too early)
        updatedReminder.PatternInferenceStatus.Should().NotBe(PatternInferenceStatus.Weekly);

        // Time window should still be around 20:00 (may have shifted slightly)
        updatedReminder.TimeWindowCenter.Should().NotBeNull();
        var timeWindow = updatedReminder.TimeWindowCenter!.Value;
        timeWindow.Hours.Should().BeOneOf(19, 20); // Allow for slight adjustment
        // Minutes should be reasonable (around 0 for 20:00, or around 55 for 19:55)
        if (timeWindow.Hours == 20)
        {
            timeWindow.Minutes.Should().BeLessThan(15);
        }
        else if (timeWindow.Hours == 19)
        {
            timeWindow.Minutes.Should().BeGreaterThan(45);
        }

        // Should have observed both Friday and Saturday
        var observedDays = updatedReminder.GetObservedDays();
        observedDays.Should().HaveCount(2);
        observedDays.Should().Contain(d => d.DayOfWeek == DayOfWeek.Friday);
        observedDays.Should().Contain(d => d.DayOfWeek == DayOfWeek.Saturday);
    }

    #endregion

    #region Scenario 3: Third Consecutive Day Infers Daily Habit

    [Fact]
    public async Task Scenario3_ThirdConsecutiveDay_InfersDailyHabit()
    {
        // Arrange - Create first two events
        var firstTimestamp = new DateTime(2026, 1, 2, 20, 0, 0, DateTimeKind.Utc); // Friday
        var firstResponse = await CreateEventAsync(TestPersonId, TestAction, firstTimestamp);
        var reminderId = firstResponse.RelatedReminderId!.Value;

        var secondTimestamp = new DateTime(2026, 1, 3, 19, 50, 0, DateTimeKind.Utc); // Saturday
        await CreateEventAsync(TestPersonId, TestAction, secondTimestamp);

        // Act - Create third event (Sunday 20:10)
        var thirdTimestamp = new DateTime(2026, 1, 4, 20, 10, 0, DateTimeKind.Utc); // Sunday
        var thirdResponse = await CreateEventAsync(TestPersonId, TestAction, thirdTimestamp);

        // Assert - Should still be the same reminder
        thirdResponse.RelatedReminderId.Should().Be(reminderId);

        var reminders = await GetRemindersForPersonAsync(TestPersonId);
        reminders.Should().HaveCount(1, "Should still have exactly one reminder");

        var reminder = await GetReminderAsync(reminderId);
        reminder.Should().NotBeNull();

        // Probability should have increased again (from default, with 3 probability increases)
        reminder!.Confidence.Should().BeGreaterThan(0.0);

        // Evidence count should be 3
        reminder.EvidenceCount.Should().Be(3);

        // Pattern should now be inferred as Daily
        reminder.PatternInferenceStatus.Should().Be(PatternInferenceStatus.Daily);

        // Occurrence should indicate daily pattern
        reminder.Occurrence.Should().NotBeNullOrEmpty();
        reminder.Occurrence!.ToLowerInvariant().Should().Contain("daily");

        // Time window should remain around 20:00
        reminder.TimeWindowCenter.Should().NotBeNull();
        var timeWindow = reminder.TimeWindowCenter!.Value;
        timeWindow.Hours.Should().BeOneOf(19, 20);

        // No weekday should be set (it's daily, not weekly)
        reminder.InferredWeekday.Should().BeNull();

        // Should have observed three days
        var observedDays = reminder.GetObservedDays();
        observedDays.Should().HaveCount(3);
    }

    #endregion

    #region Scenario 4: Weekly Pattern Should NOT Be Inferred Early

    [Fact]
    public async Task Scenario4_WeeklyPattern_ShouldNotBeInferredEarly()
    {
        // Arrange - Create events on Fri, Sat, Sun at ~20:00
        var fridayTimestamp = new DateTime(2026, 1, 2, 20, 0, 0, DateTimeKind.Utc); // Friday
        var firstResponse = await CreateEventAsync(TestPersonId, TestAction, fridayTimestamp);
        var reminderId = firstResponse.RelatedReminderId!.Value;

        var saturdayTimestamp = new DateTime(2026, 1, 3, 19, 50, 0, DateTimeKind.Utc); // Saturday
        await CreateEventAsync(TestPersonId, TestAction, saturdayTimestamp);

        var sundayTimestamp = new DateTime(2026, 1, 4, 20, 10, 0, DateTimeKind.Utc); // Sunday
        await CreateEventAsync(TestPersonId, TestAction, sundayTimestamp);

        // Act - Verify pattern inference

        // Assert
        var reminder = await GetReminderAsync(reminderId);
        reminder.Should().NotBeNull();

        // Pattern should NOT be Weekly (only 3 events, across different days)
        reminder!.PatternInferenceStatus.Should().NotBe(PatternInferenceStatus.Weekly,
            "Weekly pattern should not be inferred from just 3 events across different days");

        // Weekday should not be locked
        reminder.InferredWeekday.Should().BeNull("Weekday should not be set when pattern is not Weekly");

        // Should be Daily or Flexible, not Weekly
        reminder.PatternInferenceStatus.Should().BeOneOf(
            PatternInferenceStatus.Daily,
            PatternInferenceStatus.Flexible,
            PatternInferenceStatus.Unknown);

        // Occurrence should not mention a specific weekday
        if (!string.IsNullOrEmpty(reminder.Occurrence))
        {
            var occurrenceLower = reminder.Occurrence.ToLowerInvariant();
            occurrenceLower.Should().NotContain("friday");
            occurrenceLower.Should().NotContain("saturday");
            occurrenceLower.Should().NotContain("sunday");
        }
    }

    #endregion

    #region Scenario 5: Weekly Pattern Inferred Only After Multiple Weeks

    [Fact]
    public async Task Scenario5_WeeklyPattern_InferredOnlyAfterMultipleWeeks()
    {
        // Arrange - Create first event (Friday 20:00)
        var firstFriday = new DateTime(2026, 1, 2, 20, 0, 0, DateTimeKind.Utc); // Friday
        var firstResponse = await CreateEventAsync(TestPersonId, TestAction, firstFriday);
        var reminderId = firstResponse.RelatedReminderId!.Value;

        // Act - Create events on subsequent Fridays (multiple weeks)
        var secondFriday = new DateTime(2026, 1, 9, 20, 5, 0, DateTimeKind.Utc); // Next Friday
        await CreateEventAsync(TestPersonId, TestAction, secondFriday);

        var thirdFriday = new DateTime(2026, 1, 16, 19, 55, 0, DateTimeKind.Utc); // Next Friday
        await CreateEventAsync(TestPersonId, TestAction, thirdFriday);

        var fourthFriday = new DateTime(2026, 1, 23, 20, 0, 0, DateTimeKind.Utc); // Next Friday
        await CreateEventAsync(TestPersonId, TestAction, fourthFriday);

        // Assert
        var reminders = await GetRemindersForPersonAsync(TestPersonId);
        reminders.Should().HaveCount(1, "Should still have exactly one reminder");

        var reminder = await GetReminderAsync(reminderId);
        reminder.Should().NotBeNull();

        // Evidence count should be 4
        reminder!.EvidenceCount.Should().Be(4);

        // Pattern should now be inferred as Weekly (after 3+ weeks of Friday-only evidence)
        reminder.PatternInferenceStatus.Should().Be(PatternInferenceStatus.Weekly,
            "Weekly pattern should be inferred after multiple weeks of same weekday");

        // Weekday should be Friday (5 = Friday in DayOfWeek enum, but we store 0-6 where 0=Sunday)
        // Friday is day 5 in DayOfWeek enum, but we need to check our storage format
        reminder.InferredWeekday.Should().NotBeNull("Weekday should be set for weekly pattern");
        
        // Verify it's Friday (DayOfWeek.Friday = 5 in .NET enum where 0=Sunday, 1=Monday, ..., 5=Friday)
        var dayOfWeekHistogram = reminder.GetDayOfWeekHistogram();
        var fridayIndex = (int)DayOfWeek.Friday; // Should be 5
        dayOfWeekHistogram[fridayIndex].Should().BeGreaterOrEqualTo(3, "Should have at least 3 Friday observations");

        // Occurrence should mention Friday
        reminder.Occurrence.Should().NotBeNullOrEmpty();
        reminder.Occurrence!.ToLowerInvariant().Should().Contain("friday");

        // Probability should reflect strong confidence
        reminder.Confidence.Should().BeGreaterThan(0.5, "Confidence should be high after multiple weeks");
    }

    #endregion

    #region Scenario 6: Time Window Mismatch Creates New Reminder

    [Fact]
    public async Task Scenario6_TimeWindowMismatch_CreatesNewReminder()
    {
        // Arrange - Create first event at 20:00
        var eveningTimestamp = new DateTime(2026, 1, 2, 20, 0, 0, DateTimeKind.Utc);
        var firstResponse = await CreateEventAsync(TestPersonId, TestAction, eveningTimestamp);
        var firstReminderId = firstResponse.RelatedReminderId!.Value;

        // Act - Create event at 10:00 (different time window)
        var morningTimestamp = new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc);
        var secondResponse = await CreateEventAsync(TestPersonId, TestAction, morningTimestamp);

        // Assert - Should create a new reminder
        secondResponse.RelatedReminderId.Should().NotBe(firstReminderId,
            "Different time window should create a new reminder");

        var reminders = await GetRemindersForPersonAsync(TestPersonId);
        reminders.Should().HaveCount(2, "Should have two reminders for different time windows");

        var eveningReminder = await GetReminderAsync(firstReminderId);
        var morningReminder = await GetReminderAsync(secondResponse.RelatedReminderId!.Value);

        eveningReminder.Should().NotBeNull();
        morningReminder.Should().NotBeNull();

        // Evening reminder should be around 20:00
        eveningReminder!.TimeWindowCenter.Should().NotBeNull();
        eveningReminder.TimeWindowCenter!.Value.Hours.Should().Be(20);

        // Morning reminder should be around 10:00
        morningReminder!.TimeWindowCenter.Should().NotBeNull();
        morningReminder.TimeWindowCenter!.Value.Hours.Should().Be(10);

        // Probabilities should be independent (both should have default confidence)
        eveningReminder.Confidence.Should().BeGreaterThan(0.0);
        morningReminder.Confidence.Should().BeGreaterThan(0.0);

        // Evidence counts should be independent
        eveningReminder.EvidenceCount.Should().Be(1);
        morningReminder.EvidenceCount.Should().Be(1);
    }

    #endregion

    #region Scenario 7: Different Action Does Not Match

    [Fact]
    public async Task Scenario7_DifferentAction_DoesNotMatch()
    {
        // Arrange - Create first event with PlayMusic
        var firstTimestamp = new DateTime(2026, 1, 2, 20, 0, 0, DateTimeKind.Utc);
        var firstResponse = await CreateEventAsync(TestPersonId, TestAction, firstTimestamp);
        var firstReminderId = firstResponse.RelatedReminderId!.Value;

        // Act - Create event with different action at same time
        var secondTimestamp = new DateTime(2026, 1, 4, 20, 5, 0, DateTimeKind.Utc);
        var secondResponse = await CreateEventAsync(TestPersonId, "BoilWater", secondTimestamp);

        // Assert - Should create a new reminder
        secondResponse.RelatedReminderId.Should().NotBe(firstReminderId,
            "Different action should create a new reminder");

        var reminders = await GetRemindersForPersonAsync(TestPersonId);
        reminders.Should().HaveCount(2, "Should have two reminders for different actions");

        var musicReminder = await GetReminderAsync(firstReminderId);
        var waterReminder = await GetReminderAsync(secondResponse.RelatedReminderId!.Value);

        musicReminder.Should().NotBeNull();
        waterReminder.Should().NotBeNull();

        // Actions should be different
        musicReminder!.SuggestedAction.Should().Be(TestAction);
        waterReminder!.SuggestedAction.Should().Be("BoilWater");

        // PlayMusic reminder should be unaffected
        musicReminder.Confidence.Should().BeGreaterThan(0.0);
        musicReminder.EvidenceCount.Should().Be(1);

        // BoilWater reminder should be new
        waterReminder.Confidence.Should().BeGreaterThan(0.0);
        waterReminder.EvidenceCount.Should().Be(1);
    }

    #endregion

    #region Scenario 8: Probability Increases Instead of Duplicate Reminders

    [Fact]
    public async Task Scenario8_ProbabilityIncreases_InsteadOfDuplicateReminders()
    {
        // Arrange - Create first event
        var baseTimestamp = new DateTime(2026, 1, 2, 20, 0, 0, DateTimeKind.Utc);
        var firstResponse = await CreateEventAsync(TestPersonId, TestAction, baseTimestamp);
        var reminderId = firstResponse.RelatedReminderId!.Value;
        var initialReminder = await GetReminderAsync(reminderId);
        var initialConfidence = initialReminder!.Confidence;
        var initialEvidenceCount = initialReminder.EvidenceCount;

        // Act - Insert multiple events within time window across different days
        var events = new[]
        {
            new DateTime(2026, 1, 3, 19, 55, 0, DateTimeKind.Utc), // Saturday
            new DateTime(2026, 1, 4, 20, 5, 0, DateTimeKind.Utc),  // Sunday
            new DateTime(2026, 1, 5, 19, 50, 0, DateTimeKind.Utc), // Monday
            new DateTime(2026, 1, 6, 20, 10, 0, DateTimeKind.Utc),  // Tuesday
        };

        foreach (var eventTimestamp in events)
        {
            var response = await CreateEventAsync(TestPersonId, TestAction, eventTimestamp);
            response.RelatedReminderId.Should().Be(reminderId, 
                $"Event at {eventTimestamp} should match existing reminder");
        }

        // Assert
        var reminders = await GetRemindersForPersonAsync(TestPersonId);
        reminders.Should().HaveCount(1, 
            "Reminder count should remain constant - no duplicates created");

        var finalReminder = await GetReminderAsync(reminderId);
        finalReminder.Should().NotBeNull();

        // Probability should have increased monotonically
        finalReminder!.Confidence.Should().BeGreaterThan(initialConfidence,
            "Probability should increase with each matching event");

        // Evidence count should have increased correctly (1 initial + 4 new = 5)
        finalReminder.EvidenceCount.Should().Be(initialEvidenceCount + events.Length,
            $"Evidence count should be {initialEvidenceCount + events.Length}");

        // Should have observed multiple days
        var observedDays = finalReminder.GetObservedDays();
        observedDays.Should().HaveCountGreaterThan(3,
            "Should have observed events across multiple days");
    }

    #endregion

    #region Scenario 9: Occurrence Remains Flexible for Irregular Patterns

    [Fact]
    public async Task Scenario9_OccurrenceRemainsFlexible_ForIrregularPatterns()
    {
        // Arrange - Create events at irregular intervals
        var mondayTimestamp = new DateTime(2026, 1, 5, 20, 0, 0, DateTimeKind.Utc); // Monday
        var firstResponse = await CreateEventAsync(TestPersonId, TestAction, mondayTimestamp);
        var reminderId = firstResponse.RelatedReminderId!.Value;

        // Act - Create events at irregular intervals (Monday, Thursday, Sunday)
        var thursdayTimestamp = new DateTime(2026, 1, 8, 20, 5, 0, DateTimeKind.Utc); // Thursday
        await CreateEventAsync(TestPersonId, TestAction, thursdayTimestamp);

        var sundayTimestamp = new DateTime(2026, 1, 11, 19, 55, 0, DateTimeKind.Utc); // Sunday
        await CreateEventAsync(TestPersonId, TestAction, sundayTimestamp);

        // Assert
        var reminders = await GetRemindersForPersonAsync(TestPersonId);
        reminders.Should().HaveCount(1, "Should have exactly one reminder");

        var reminder = await GetReminderAsync(reminderId);
        reminder.Should().NotBeNull();

        // Probability should have increased moderately
        reminder!.Confidence.Should().BeGreaterThan(0.0,
            "Probability should increase with evidence");

        // Pattern should remain Flexible (not Daily or Weekly)
        reminder.PatternInferenceStatus.Should().Be(PatternInferenceStatus.Flexible,
            "Irregular patterns should remain Flexible, not Daily or Weekly");

        // Occurrence should indicate flexible timing
        reminder.Occurrence.Should().NotBeNullOrEmpty();
        var occurrenceLower = reminder.Occurrence.ToLowerInvariant();
        occurrenceLower.Should().Match(s => s.Contains("flexible") || s.Contains("around"));

        // Should NOT be Daily
        reminder.PatternInferenceStatus.Should().NotBe(PatternInferenceStatus.Daily,
            "Irregular patterns should not be inferred as Daily");

        // Should NOT be Weekly
        reminder.PatternInferenceStatus.Should().NotBe(PatternInferenceStatus.Weekly,
            "Irregular patterns should not be inferred as Weekly");

        // Weekday should not be set
        reminder.InferredWeekday.Should().BeNull("No specific weekday for flexible patterns");
    }

    #endregion

    #region Additional Edge Case Tests

    [Fact]
    public async Task TimeWindowMatching_WorksAcrossDifferentDays()
    {
        // Verify that time-of-day matching works regardless of day
        var fridayTimestamp = new DateTime(2026, 1, 2, 20, 0, 0, DateTimeKind.Utc); // Friday
        var firstResponse = await CreateEventAsync(TestPersonId, TestAction, fridayTimestamp);
        var reminderId = firstResponse.RelatedReminderId!.Value;

        // Create event on Monday at same time - should match
        var mondayTimestamp = new DateTime(2026, 1, 5, 20, 0, 0, DateTimeKind.Utc); // Monday
        var secondResponse = await CreateEventAsync(TestPersonId, TestAction, mondayTimestamp);

        secondResponse.RelatedReminderId.Should().Be(reminderId,
            "Time-of-day matching should work across different days");

        var reminder = await GetReminderAsync(reminderId);
        reminder!.EvidenceCount.Should().Be(2);
    }

    [Fact]
    public async Task EvidenceAccumulation_UpdatesTimeWindowGradually()
    {
        // Verify that time window center adjusts gradually with EMA
        var baseTimestamp = new DateTime(2026, 1, 2, 20, 0, 0, DateTimeKind.Utc);
        var firstResponse = await CreateEventAsync(TestPersonId, TestAction, baseTimestamp);
        var reminderId = firstResponse.RelatedReminderId!.Value;

        var firstReminder = await GetReminderAsync(reminderId);
        var initialTimeWindow = firstReminder!.TimeWindowCenter!.Value;

        // Add event slightly earlier
        var earlierTimestamp = new DateTime(2026, 1, 3, 19, 45, 0, DateTimeKind.Utc);
        await CreateEventAsync(TestPersonId, TestAction, earlierTimestamp);

        var updatedReminder = await GetReminderAsync(reminderId);
        updatedReminder.Should().NotBeNull();
        updatedReminder!.TimeWindowCenter.Should().NotBeNull();
        var updatedTimeWindow = updatedReminder.TimeWindowCenter!.Value;

        // Time window should have shifted slightly toward 19:45, but not completely
        // (EMA with alpha=0.1 means 90% weight on old, 10% on new)
        updatedTimeWindow.Should().NotBe(initialTimeWindow,
            "Time window should adjust gradually with new evidence");
    }

    #endregion
}

