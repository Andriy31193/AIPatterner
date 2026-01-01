// Integration tests using real PostgreSQL database
namespace AIPatterner.Tests.Integration;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Domain.Entities;
using FluentAssertions;
using System.Net.Http.Json;
using Xunit;

public class RealDatabaseTests : RealDatabaseTestBase
{
    [Fact]
    public async Task CreateEvent_WithProbability_ShouldCreateReminderCandidate()
    {
        // Arrange
        var eventDto = new ActionEventDto
        {
            PersonId = "real_test_user",
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
            CustomData = new Dictionary<string, string> { { "source", "real_db_test" } }
        };

        // Act
        var command = new IngestEventCommand { Event = eventDto };
        var response = await EventHandler.Handle(command, CancellationToken.None);

        // Assert
        response.EventId.Should().NotBeEmpty();
        response.RelatedReminderId.Should().NotBeNull();

        var reminder = await ReminderRepository.GetByIdAsync(response.RelatedReminderId!.Value, CancellationToken.None);
        reminder.Should().NotBeNull();
        reminder!.PersonId.Should().Be("real_test_user");
        reminder.SuggestedAction.Should().Be("drink_water");
        reminder.Confidence.Should().BeApproximately(0.5, 0.01);
        reminder.CheckAtUtc.Should().BeCloseTo(eventDto.TimestampUtc, TimeSpan.FromSeconds(1));
        reminder.SourceEventId.Should().Be(response.EventId);
        reminder.CustomData.Should().ContainKey("source").WhoseValue.Should().Be("real_db_test");
        reminder.Occurrence.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateEvent_ViaAPI_ShouldReturnAccepted()
    {
        // Skip if API is not available
        if (!await IsApiAvailableAsync())
        {
            return;
        }

        // Arrange
        var eventDto = new ActionEventDto
        {
            PersonId = "api_real_user",
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
        var response = await HttpClient.PostAsJsonAsync("/v1/events", eventDto);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted);
        var result = await response.Content.ReadFromJsonAsync<IngestEventResponse>();
        result.Should().NotBeNull();
        result!.EventId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateMatchingEvents_ShouldUpdateExistingReminder()
    {
        // Arrange - Create first event with reminder
        var firstEvent = new ActionEventDto
        {
            PersonId = "real_match_user",
            ActionType = "exercise",
            TimestampUtc = new DateTime(2026, 1, 2, 17, 0, 0, DateTimeKind.Utc),
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "gym",
                PresentPeople = new List<string> { "real_match_user" },
                StateSignals = new Dictionary<string, string> { { "energy", "high" } }
            },
            ProbabilityValue = 0.3,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var firstCommand = new IngestEventCommand { Event = firstEvent };
        var firstResponse = await EventHandler.Handle(firstCommand, CancellationToken.None);
        var firstReminderId = firstResponse.RelatedReminderId!.Value;

        // Act - Create second matching event
        var secondEvent = new ActionEventDto
        {
            PersonId = "real_match_user",
            ActionType = "exercise",
            TimestampUtc = new DateTime(2026, 1, 9, 17, 5, 0, DateTimeKind.Utc),
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "gym",
                PresentPeople = new List<string> { "real_match_user" },
                StateSignals = new Dictionary<string, string> { { "energy", "high" } }
            },
            ProbabilityValue = 0.2,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var secondCommand = new IngestEventCommand { Event = secondEvent };
        var secondResponse = await EventHandler.Handle(secondCommand, CancellationToken.None);

        // Assert - Should update existing reminder
        secondResponse.RelatedReminderId.Should().Be(firstReminderId);

        var updatedReminder = await ReminderRepository.GetByIdAsync(firstReminderId, CancellationToken.None);
        updatedReminder.Should().NotBeNull();
        updatedReminder!.Confidence.Should().BeGreaterThan(0.5);
    }

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
            PersonId = "api_list_user",
            ActionType = "api_list_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "morning", DayType = "weekday" },
            ProbabilityValue = 0.3,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var command = new IngestEventCommand { Event = eventDto };
        await EventHandler.Handle(command, CancellationToken.None);

        // Act
        var response = await HttpClient.GetAsync("/v1/reminder-candidates?personId=api_list_user");

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
            PersonId = "api_related_real_user",
            ActionType = "api_related_action",
            TimestampUtc = DateTime.UtcNow,
            Context = new ActionContextDto { TimeBucket = "afternoon", DayType = "weekday" },
            ProbabilityValue = 0.3,
            ProbabilityAction = ProbabilityAction.Increase
        };

        var command = new IngestEventCommand { Event = eventDto };
        var response = await EventHandler.Handle(command, CancellationToken.None);
        var eventId = response.EventId;

        // Act
        var apiResponse = await HttpClient.GetAsync($"/v1/events/{eventId}/related-reminders");

        // Assert
        apiResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var result = await apiResponse.Content.ReadFromJsonAsync<ReminderCandidateListResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.Items[0].SourceEventId.Should().Be(eventId);
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
}


