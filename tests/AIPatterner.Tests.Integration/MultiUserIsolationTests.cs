// Integration tests for multi-user data isolation
namespace AIPatterner.Tests.Integration;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Application.Handlers;
using AIPatterner.Application.Queries;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using AIPatterner.Infrastructure.Persistence.Repositories;
using AIPatterner.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

public class MultiUserIsolationTests : RealDatabaseTestBase, IDisposable
{
    private readonly Guid _userAId;
    private readonly Guid _userBId;
    private readonly string _userAPersonId;
    private readonly string _userBPersonId;
    private readonly MockUserContextService _userAContext;
    private readonly MockUserContextService _userBContext;
    private readonly MockUserContextService _adminContext;

    public MultiUserIsolationTests()
    {
        _userAPersonId = "test_user_a";
        _userBPersonId = "test_user_b";

        // Create users in database
        var userA = new User("usera", "usera@test.com", "hash", "user");
        var userB = new User("userb", "userb@test.com", "hash", "user");
        
        Context.Users.Add(userA);
        Context.Users.Add(userB);
        Context.SaveChanges();
        
        _userAId = userA.Id;
        _userBId = userB.Id;

        _userAContext = new MockUserContextService(_userAId, false);
        _userBContext = new MockUserContextService(_userBId, false);
        _adminContext = new MockUserContextService(null, true);
    }

    [Fact]
    public async Task Routines_ShouldBeIsolatedPerUser()
    {
        // Arrange
        var eventRepo = new EventRepository(Context);
        var routineRepo = new RoutineRepository(Context);
        var routineReminderRepo = new RoutineReminderRepository(Context);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Routine:ObservationWindowMinutes", "45" },
            { "Routine:DefaultRoutineProbability", "0.5" }
        }).Build();
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var routineLearningService = new RoutineLearningService(
            routineRepo,
            routineReminderRepo,
            eventRepo,
            config,
            loggerFactory.CreateLogger<RoutineLearningService>());

        // Create StateChange event for User A
        var userAEvent = new ActionEvent(
            _userAPersonId,
            "ArrivalHome",
            DateTime.UtcNow,
            new ActionContext("evening", "weekday", "home", new List<string>(), new Dictionary<string, string>()),
            _userAId,
            eventType: EventType.StateChange);

        await eventRepo.AddAsync(userAEvent, CancellationToken.None);
        await routineLearningService.HandleIntentAsync(userAEvent, CancellationToken.None);

        // Create StateChange event for User B
        var userBEvent = new ActionEvent(
            _userBPersonId,
            "ArrivalHome",
            DateTime.UtcNow,
            new ActionContext("evening", "weekday", "home", new List<string>(), new Dictionary<string, string>()),
            _userBId,
            eventType: EventType.StateChange);

        await eventRepo.AddAsync(userBEvent, CancellationToken.None);
        await routineLearningService.HandleIntentAsync(userBEvent, CancellationToken.None);

        // Act - Query routines as User A
        var userARoutines = await routineRepo.GetFilteredAsync(_userAPersonId, 1, 100, CancellationToken.None);
        var userARoutinesFiltered = userARoutines.Where(r => r.UserId == _userAId).ToList();

        // Query routines as User B
        var userBRoutines = await routineRepo.GetFilteredAsync(_userBPersonId, 1, 100, CancellationToken.None);
        var userBRoutinesFiltered = userBRoutines.Where(r => r.UserId == _userBId).ToList();

        // Assert
        Assert.True(userARoutinesFiltered.Count > 0, "User A should have at least one routine");
        Assert.True(userBRoutinesFiltered.Count > 0, "User B should have at least one routine");
        Assert.All(userARoutinesFiltered, r => Assert.Equal(_userAId, r.UserId));
        Assert.All(userBRoutinesFiltered, r => Assert.Equal(_userBId, r.UserId));
        
        // User A should not see User B's routines
        var userASeesUserBRoutines = userARoutinesFiltered.Any(r => r.UserId == _userBId);
        Assert.False(userASeesUserBRoutines, "User A should not see User B's routines");
    }

    [Fact]
    public async Task Reminders_ShouldBeIsolatedPerUser()
    {
        // Arrange
        var reminderRepo = new ReminderCandidateRepository(Context);
        
        var userAReminder = new ReminderCandidate(
            _userAPersonId,
            "PlayMusic",
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            _userAId,
            confidence: 0.7);

        var userBReminder = new ReminderCandidate(
            _userBPersonId,
            "PlayMusic",
            DateTime.UtcNow.AddHours(1),
            ReminderStyle.Suggest,
            _userBId,
            confidence: 0.7);

        await reminderRepo.AddAsync(userAReminder, CancellationToken.None);
        await reminderRepo.AddAsync(userBReminder, CancellationToken.None);

        // Act - Query reminders
        var allReminders = await reminderRepo.GetFilteredAsync(null, null, null, null, 1, 100, CancellationToken.None);
        var userAReminders = allReminders.Where(r => r.UserId == _userAId).ToList();
        var userBReminders = allReminders.Where(r => r.UserId == _userBId).ToList();

        // Assert
        Assert.True(userAReminders.Count > 0, "User A should have reminders");
        Assert.True(userBReminders.Count > 0, "User B should have reminders");
        Assert.All(userAReminders, r => Assert.Equal(_userAId, r.UserId));
        Assert.All(userBReminders, r => Assert.Equal(_userBId, r.UserId));
    }

    [Fact]
    public async Task Events_ShouldBeIsolatedPerUser()
    {
        // Arrange
        var eventRepo = new EventRepository(Context);

        var userAEvent = new ActionEvent(
            _userAPersonId,
            "PlayMusic",
            DateTime.UtcNow,
            new ActionContext("evening", "weekday", "home", new List<string>(), new Dictionary<string, string>()),
            _userAId);

        var userBEvent = new ActionEvent(
            _userBPersonId,
            "PlayMusic",
            DateTime.UtcNow,
            new ActionContext("evening", "weekday", "home", new List<string>(), new Dictionary<string, string>()),
            _userBId);

        await eventRepo.AddAsync(userAEvent, CancellationToken.None);
        await eventRepo.AddAsync(userBEvent, CancellationToken.None);

        // Act - Query events
        var allEvents = await eventRepo.GetFilteredAsync(null, null, null, null, 1, 100, CancellationToken.None);
        var userAEvents = allEvents.Where(e => e.UserId == _userAId).ToList();
        var userBEvents = allEvents.Where(e => e.UserId == _userBId).ToList();

        // Assert
        Assert.True(userAEvents.Count > 0, "User A should have events");
        Assert.True(userBEvents.Count > 0, "User B should have events");
        Assert.All(userAEvents, e => Assert.Equal(_userAId, e.UserId));
        Assert.All(userBEvents, e => Assert.Equal(_userBId, e.UserId));
    }

    [Fact]
    public async Task RoutineLearning_ShouldCreateRoutinesWithCorrectUserId()
    {
        // Arrange
        var eventRepo = new EventRepository(Context);
        var routineRepo = new RoutineRepository(Context);
        var routineReminderRepo = new RoutineReminderRepository(Context);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Routine:ObservationWindowMinutes", "45" },
            { "Routine:DefaultRoutineProbability", "0.5" }
        }).Build();
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var routineLearningService = new RoutineLearningService(
            routineRepo,
            routineReminderRepo,
            eventRepo,
            config,
            loggerFactory.CreateLogger<RoutineLearningService>());

        // Create StateChange event for User A
        var userAEvent = new ActionEvent(
            _userAPersonId,
            "ArrivalHome",
            DateTime.UtcNow,
            new ActionContext("evening", "weekday", "home", new List<string>(), new Dictionary<string, string>()),
            _userAId,
            eventType: EventType.StateChange);

        await eventRepo.AddAsync(userAEvent, CancellationToken.None);
        var routine = await routineLearningService.HandleIntentAsync(userAEvent, CancellationToken.None);

        // Assert
        Assert.NotNull(routine);
        Assert.Equal(_userAId, routine.UserId);
        Assert.Equal(_userAPersonId, routine.PersonId);
    }

    [Fact]
    public async Task GetRoutinesQueryHandler_ShouldFilterByUserId()
    {
        // Arrange - Create routines for both users
        var routineRepo = new RoutineRepository(Context);
        
        var userARoutine = new Routine(_userAPersonId, "ArrivalHome", DateTime.UtcNow, _userAId);
        var userBRoutine = new Routine(_userBPersonId, "ArrivalHome", DateTime.UtcNow, _userBId);
        
        await routineRepo.AddAsync(userARoutine, CancellationToken.None);
        await routineRepo.AddAsync(userBRoutine, CancellationToken.None);

        // Act - Query as User A
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Routine:ObservationWindowMinutes", "45" }
        }).Build();
        
        var handlerA = new GetRoutinesQueryHandler(routineRepo, config, _userAContext);
        var resultA = await handlerA.Handle(new GetRoutinesQuery 
        { 
            PersonId = _userAPersonId,
            Page = 1,
            PageSize = 100
        }, CancellationToken.None);

        // Act - Query as User B
        var handlerB = new GetRoutinesQueryHandler(routineRepo, config, _userBContext);
        var resultB = await handlerB.Handle(new GetRoutinesQuery 
        { 
            PersonId = _userBPersonId,
            Page = 1,
            PageSize = 100
        }, CancellationToken.None);

        // Get all routines from repository to verify userId
        var allRoutines = await routineRepo.GetFilteredAsync(null, 1, 100, CancellationToken.None);
        
        // Assert
        Assert.True(resultA.Items.Count > 0, "User A should see their routines");
        Assert.True(resultB.Items.Count > 0, "User B should see their routines");
        
        // User A should not see User B's routines
        var userASeesUserBRoutine = resultA.Items.Any(r => r.Id == userBRoutine.Id);
        Assert.False(userASeesUserBRoutine, "User A should not see User B's routines");
        
        // User B should not see User A's routines
        var userBSeesUserARoutine = resultB.Items.Any(r => r.Id == userARoutine.Id);
        Assert.False(userBSeesUserARoutine, "User B should not see User A's routines");
        
        // Verify all routines in resultA belong to User A
        foreach (var item in resultA.Items)
        {
            var routine = allRoutines.FirstOrDefault(rt => rt.Id == item.Id);
            if (routine != null)
            {
                Assert.Equal(_userAId, routine.UserId);
            }
        }
        
        // Verify all routines in resultB belong to User B
        foreach (var item in resultB.Items)
        {
            var routine = allRoutines.FirstOrDefault(rt => rt.Id == item.Id);
            if (routine != null)
            {
                Assert.Equal(_userBId, routine.UserId);
            }
        }
    }

    public new void Dispose()
    {
        // Clean up test data
        var testPersonIds = new[] { _userAPersonId, _userBPersonId };
        
        foreach (var personId in testPersonIds)
        {
            var reminders = Context.ReminderCandidates.Where(r => r.PersonId == personId).ToList();
            Context.ReminderCandidates.RemoveRange(reminders);

            var events = Context.ActionEvents.Where(e => e.PersonId == personId).ToList();
            Context.ActionEvents.RemoveRange(events);

            var routines = Context.Routines.Where(r => r.PersonId == personId).ToList();
            foreach (var routine in routines)
            {
                var routineReminders = Context.RoutineReminders.Where(rr => rr.RoutineId == routine.Id).ToList();
                Context.RoutineReminders.RemoveRange(routineReminders);
            }
            Context.Routines.RemoveRange(routines);
        }

        // Remove test users
        var testUsers = Context.Users.Where(u => u.Username == "usera" || u.Username == "userb").ToList();
        Context.Users.RemoveRange(testUsers);

        Context.SaveChanges();
        base.Dispose();
    }
}

