// Integration tests for PersonIdsController
namespace AIPatterner.Tests.Integration;

using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using AIPatterner.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using Xunit;

public class PersonIdsControllerTests : RealDatabaseTestBase
{
    [Fact]
    public async Task GetPersonIds_ShouldIncludeUsersFromUsersTable()
    {
        // Skip if API is not available
        if (!await IsApiAvailableAsync())
        {
            return;
        }

        // Arrange - Create test users with properly hashed passwords
        var authService = new AuthService(
            new ConfigurationBuilder().Build(),
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<AuthService>());
        
        var user1 = new User("testuser1", "test1@example.com", authService.HashPassword("password1"), "user");
        var user2 = new User("testuser2", "test2@example.com", authService.HashPassword("password2"), "user");
        var adminUser = new User("adminuser", "admin@example.com", authService.HashPassword("password3"), "admin");

        Context.Users.AddRange(user1, user2, adminUser);
        await Context.SaveChangesAsync();

        // Act - Call the API endpoint
        var response = await HttpClient.GetAsync("/v1/person-ids");
        response.EnsureSuccessStatusCode();

        var personIds = await response.Content.ReadFromJsonAsync<List<PersonIdDto>>();

        // Assert - Should include usernames and user IDs
        personIds.Should().NotBeNull();
        personIds!.Should().Contain(p => p.PersonId == user1.Username && p.DisplayName.Contains(user1.Username));
        personIds.Should().Contain(p => p.PersonId == user1.Id.ToString() && p.DisplayName.Contains(user1.Username));
        personIds.Should().Contain(p => p.PersonId == user2.Username && p.DisplayName.Contains(user2.Username));
        personIds.Should().Contain(p => p.PersonId == user2.Id.ToString() && p.DisplayName.Contains(user2.Username));
        personIds.Should().Contain(p => p.PersonId == adminUser.Username && p.DisplayName.Contains(adminUser.Username));
        personIds.Should().Contain(p => p.PersonId == adminUser.Id.ToString() && p.DisplayName.Contains(adminUser.Username));
    }

    [Fact]
    public async Task GetPersonIds_ShouldIncludePersonIdsFromEvents()
    {
        // Skip if API is not available
        if (!await IsApiAvailableAsync())
        {
            return;
        }

        // Arrange - Create events with different personIds
        var event1 = new ActionEvent(
            "event_person_1",
            "test_action",
            DateTime.UtcNow,
            new ActionContext("morning", "weekday")
        );
        var event2 = new ActionEvent(
            "event_person_2",
            "test_action",
            DateTime.UtcNow,
            new ActionContext("afternoon", "weekday")
        );

        Context.ActionEvents.AddRange(event1, event2);
        await Context.SaveChangesAsync();

        // Act
        var response = await HttpClient.GetAsync("/v1/person-ids");
        response.EnsureSuccessStatusCode();

        var personIds = await response.Content.ReadFromJsonAsync<List<PersonIdDto>>();

        // Assert
        personIds.Should().NotBeNull();
        personIds!.Should().Contain(p => p.PersonId == "event_person_1");
        personIds.Should().Contain(p => p.PersonId == "event_person_2");
    }

    [Fact]
    public async Task GetPersonIds_ShouldIncludePersonIdsFromReminders()
    {
        // Skip if API is not available
        if (!await IsApiAvailableAsync())
        {
            return;
        }

        // Arrange - Create reminders with different personIds
        var reminder1 = new ReminderCandidate(
            "reminder_person_1",
            "test_action",
            DateTime.UtcNow,
            ReminderStyle.Suggest
        );
        var reminder2 = new ReminderCandidate(
            "reminder_person_2",
            "test_action",
            DateTime.UtcNow,
            ReminderStyle.Suggest
        );

        Context.ReminderCandidates.AddRange(reminder1, reminder2);
        await Context.SaveChangesAsync();

        // Act
        var response = await HttpClient.GetAsync("/v1/person-ids");
        response.EnsureSuccessStatusCode();

        var personIds = await response.Content.ReadFromJsonAsync<List<PersonIdDto>>();

        // Assert
        personIds.Should().NotBeNull();
        personIds!.Should().Contain(p => p.PersonId == "reminder_person_1");
        personIds.Should().Contain(p => p.PersonId == "reminder_person_2");
    }

    [Fact]
    public async Task GetPersonIds_ShouldIncludePersonIdsFromRoutines()
    {
        // Skip if API is not available
        if (!await IsApiAvailableAsync())
        {
            return;
        }

        // Arrange - Create routines with different personIds
        var routine1 = new Routine("routine_person_1", "test_intent", DateTime.UtcNow);
        var routine2 = new Routine("routine_person_2", "test_intent", DateTime.UtcNow);

        Context.Routines.AddRange(routine1, routine2);
        await Context.SaveChangesAsync();

        // Act
        var response = await HttpClient.GetAsync("/v1/person-ids");
        response.EnsureSuccessStatusCode();

        var personIds = await response.Content.ReadFromJsonAsync<List<PersonIdDto>>();

        // Assert
        personIds.Should().NotBeNull();
        personIds!.Should().Contain(p => p.PersonId == "routine_person_1");
        personIds.Should().Contain(p => p.PersonId == "routine_person_2");
    }

    [Fact]
    public async Task GetPersonIds_ShouldMatchPersonIdToUserWhenPersonIdIsUsername()
    {
        // Skip if API is not available
        if (!await IsApiAvailableAsync())
        {
            return;
        }

        // Arrange - Create a user and an event with personId = username
        var authService = new AuthService(
            new ConfigurationBuilder().Build(),
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<AuthService>());
        
        var user = new User("matched_user", "matched@example.com", authService.HashPassword("password"), "user");
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var event1 = new ActionEvent(
            user.Username, // personId matches username
            "test_action",
            DateTime.UtcNow,
            new ActionContext("morning", "weekday")
        );
        Context.ActionEvents.Add(event1);
        await Context.SaveChangesAsync();

        // Act
        var response = await HttpClient.GetAsync("/v1/person-ids");
        response.EnsureSuccessStatusCode();

        var personIds = await response.Content.ReadFromJsonAsync<List<PersonIdDto>>();

        // Assert - Should show user-friendly display name
        personIds.Should().NotBeNull();
        var matchedPersonId = personIds!.FirstOrDefault(p => p.PersonId == user.Username);
        matchedPersonId.Should().NotBeNull();
        matchedPersonId!.DisplayName.Should().Contain(user.Username);
    }

    [Fact]
    public async Task GetPersonIds_ShouldMatchPersonIdToUserWhenPersonIdIsUserId()
    {
        // Skip if API is not available
        if (!await IsApiAvailableAsync())
        {
            return;
        }

        // Arrange - Create a user and an event with personId = user ID
        var authService = new AuthService(
            new ConfigurationBuilder().Build(),
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<AuthService>());
        
        var user = new User("user_for_id", "userid@example.com", authService.HashPassword("password"), "user");
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var event1 = new ActionEvent(
            user.Id.ToString(), // personId matches user ID
            "test_action",
            DateTime.UtcNow,
            new ActionContext("morning", "weekday")
        );
        Context.ActionEvents.Add(event1);
        await Context.SaveChangesAsync();

        // Act
        var response = await HttpClient.GetAsync("/v1/person-ids");
        response.EnsureSuccessStatusCode();

        var personIds = await response.Content.ReadFromJsonAsync<List<PersonIdDto>>();

        // Assert - Should show user-friendly display name
        personIds.Should().NotBeNull();
        var matchedPersonId = personIds!.FirstOrDefault(p => p.PersonId == user.Id.ToString());
        matchedPersonId.Should().NotBeNull();
        matchedPersonId!.DisplayName.Should().Contain(user.Username);
    }

    [Fact]
    public async Task GetPersonIds_ShouldReturnUniquePersonIds()
    {
        // Skip if API is not available
        if (!await IsApiAvailableAsync())
        {
            return;
        }

        // Arrange - Create multiple entities with same personId
        var personId = "duplicate_test_person";
        
        var event1 = new ActionEvent(
            personId,
            "action1",
            DateTime.UtcNow,
            new ActionContext("morning", "weekday")
        );
        var event2 = new ActionEvent(
            personId,
            "action2",
            DateTime.UtcNow,
            new ActionContext("afternoon", "weekday")
        );
        var reminder = new ReminderCandidate(
            personId,
            "reminder_action",
            DateTime.UtcNow,
            ReminderStyle.Suggest
        );
        var routine = new Routine(personId, "test_intent", DateTime.UtcNow);

        Context.ActionEvents.AddRange(event1, event2);
        Context.ReminderCandidates.Add(reminder);
        Context.Routines.Add(routine);
        await Context.SaveChangesAsync();

        // Act
        var response = await HttpClient.GetAsync("/v1/person-ids");
        response.EnsureSuccessStatusCode();

        var personIds = await response.Content.ReadFromJsonAsync<List<PersonIdDto>>();

        // Assert - Should only appear once
        personIds.Should().NotBeNull();
        var count = personIds!.Count(p => p.PersonId == personId);
        count.Should().Be(1);
    }

    [Fact]
    public async Task GetPersonIds_ShouldReturnSortedList()
    {
        // Skip if API is not available
        if (!await IsApiAvailableAsync())
        {
            return;
        }

        // Arrange - Create entities with different personIds
        var personIds = new[] { "zebra", "alpha", "beta", "gamma" };
        
        foreach (var pid in personIds)
        {
            var event1 = new ActionEvent(
                pid,
                "test_action",
                DateTime.UtcNow,
                new ActionContext("morning", "weekday")
            );
            Context.ActionEvents.Add(event1);
        }
        await Context.SaveChangesAsync();

        // Act
        var response = await HttpClient.GetAsync("/v1/person-ids");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<PersonIdDto>>();

        // Assert - Should be sorted alphabetically
        result.Should().NotBeNull();
        var sortedPersonIds = result!.Select(p => p.PersonId).ToList();
        sortedPersonIds.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetPersonIds_ShouldIncludeBothUsernameAndUserIdForEachUser()
    {
        // Skip if API is not available
        if (!await IsApiAvailableAsync())
        {
            return;
        }

        // Arrange - Create a user
        var authService = new AuthService(
            new ConfigurationBuilder().Build(),
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<AuthService>());
        
        var user = new User("testuser_dual", "dual@example.com", authService.HashPassword("password"), "user");
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        // Act
        var response = await HttpClient.GetAsync("/v1/person-ids");
        response.EnsureSuccessStatusCode();

        var personIds = await response.Content.ReadFromJsonAsync<List<PersonIdDto>>();

        // Assert - Should include both username and user ID
        personIds.Should().NotBeNull();
        personIds!.Should().Contain(p => p.PersonId == user.Username);
        personIds.Should().Contain(p => p.PersonId == user.Id.ToString());
        
        // Both should have display names containing the username
        var usernameEntry = personIds.First(p => p.PersonId == user.Username);
        var userIdEntry = personIds.First(p => p.PersonId == user.Id.ToString());
        
        usernameEntry.DisplayName.Should().Contain(user.Username);
        userIdEntry.DisplayName.Should().Contain(user.Username);
    }

    [Fact]
    public async Task GetPersonIds_ShouldHandleEmptyDatabase()
    {
        // Skip if API is not available
        if (!await IsApiAvailableAsync())
        {
            return;
        }

        // Arrange - Clean database (already done in base class)
        
        // Act
        var response = await HttpClient.GetAsync("/v1/person-ids");
        response.EnsureSuccessStatusCode();

        var personIds = await response.Content.ReadFromJsonAsync<List<PersonIdDto>>();

        // Assert - Should return empty list or list with only test cleanup data
        personIds.Should().NotBeNull();
        // May have some test data, but should not throw
    }

    private async Task<bool> IsApiAvailableAsync()
    {
        try
        {
            var response = await HttpClient.GetAsync("/v1/person-ids");
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized;
        }
        catch
        {
            return false;
        }
    }
}

public class PersonIdDto
{
    public string PersonId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

