// Test to verify routine reminders are NOT duplicated in general reminder lists
namespace AIPatterner.Tests.Integration;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Application.Handlers;
using AIPatterner.Application.Queries;
using AIPatterner.Application.Services;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence.Repositories;
using AIPatterner.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

public class RoutineReminderDuplicationTest : RealDatabaseTestBase
{
    private readonly IRoutineRepository _routineRepository;
    private readonly IRoutineReminderRepository _routineReminderRepository;
    private readonly IRoutineLearningService _routineLearningService;
    private readonly string _testPersonId = "routine_duplication_test_user";

    public RoutineReminderDuplicationTest()
    {
        _routineRepository = new RoutineRepository(Context);
        _routineReminderRepository = new RoutineReminderRepository(Context);
        
        // Get RoutineLearningService from base class setup
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var configRepo = new ConfigurationRepository(Context);
        var signalSelector = new AIPatterner.Infrastructure.Services.SignalSelector(Configuration, loggerFactory.CreateLogger<AIPatterner.Infrastructure.Services.SignalSelector>());
        var similarityEvaluator = new AIPatterner.Infrastructure.Services.SignalSimilarityEvaluator(loggerFactory.CreateLogger<AIPatterner.Infrastructure.Services.SignalSimilarityEvaluator>());
        var signalPolicyService = new AIPatterner.Infrastructure.Services.SignalPolicyService(configRepo, Configuration);
        _routineLearningService = new RoutineLearningService(
            _routineRepository,
            _routineReminderRepository,
            ReminderRepository,
            EventRepository,
            Configuration,
            loggerFactory.CreateLogger<RoutineLearningService>(),
            signalSelector,
            similarityEvaluator,
            signalPolicyService);
    }

    [Fact]
    public async Task RoutineReminders_ShouldNotAppearInGeneralReminderList_WhenLearningWindowReopens()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var intentType = "ArrivalHome";
        var observedAction = "PlayMusic";

        // Step 1: Open first learning window and create routine reminder
        var intentEvent1 = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = intentType,
            TimestampUtc = baseTime,
            EventType = EventType.StateChange,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home"
            }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = intentEvent1 }, CancellationToken.None);

        // Wait a moment and create an observed action during learning window
        var observedEvent1 = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = observedAction,
            TimestampUtc = baseTime.AddMinutes(5),
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home"
            }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = observedEvent1 }, CancellationToken.None);

        // Verify routine reminder was created
        var routine1 = await _routineRepository.GetByPersonAndIntentAsync(_testPersonId, intentType, CancellationToken.None);
        routine1.Should().NotBeNull();
        var routineReminders1 = await _routineReminderRepository.GetByRoutineAsync(routine1!.Id, CancellationToken.None);
        routineReminders1.Should().Contain(r => r.SuggestedAction == observedAction);

        // Step 2: Wait for learning window to close (simulate by advancing time)
        // In real scenario, window would close after configured minutes
        // For test, we'll manually close it and then reopen
        
        // Step 3: Query general reminders - should NOT include routine reminders
        var query1 = new GetReminderCandidatesQuery
        {
            PersonId = _testPersonId,
            Status = ReminderCandidateStatus.Scheduled.ToString(),
            Page = 1,
            PageSize = 100
        };
        var mapper = new AutoMapper.Mapper(new AutoMapper.MapperConfiguration(cfg => cfg.AddProfile<AIPatterner.Application.Mappings.MappingProfile>()));
        var result1 = await new GetReminderCandidatesQueryHandler(
            ReminderRepository,
            mapper,
            _routineLearningService,
            EventRepository).Handle(query1, CancellationToken.None);

        // Verify routine reminders are NOT in general reminder list
        result1.Items.Should().NotContain(r => r.SuggestedAction == observedAction && 
            r.CustomData != null && r.CustomData.ContainsKey("source") && r.CustomData["source"] == "routine");

        // Step 4: Reopen learning window (trigger intent again)
        var intentEvent2 = new ActionEventDto
        {
            PersonId = _testPersonId,
            ActionType = intentType,
            TimestampUtc = baseTime.AddHours(2), // 2 hours later
            EventType = EventType.StateChange,
            Context = new ActionContextDto
            {
                TimeBucket = "evening",
                DayType = "weekday",
                Location = "home"
            }
        };
        await EventHandler.Handle(new IngestEventCommand { Event = intentEvent2 }, CancellationToken.None);

        // Step 5: Query general reminders again - should STILL NOT include routine reminders
        var query2 = new GetReminderCandidatesQuery
        {
            PersonId = _testPersonId,
            Status = ReminderCandidateStatus.Scheduled.ToString(),
            Page = 1,
            PageSize = 100
        };
        var result2 = await new GetReminderCandidatesQueryHandler(
            ReminderRepository,
            mapper,
            _routineLearningService,
            EventRepository).Handle(query2, CancellationToken.None);

        // CRITICAL ASSERTION: Routine reminders should NEVER appear in general reminder list
        // Check 1: No reminders with source="routine" should appear
        result2.Items.Should().NotContain(r => 
            r.CustomData != null && 
            r.CustomData.ContainsKey("source") && 
            r.CustomData["source"] == "routine");
        
        // Check 2: Specifically for the observed action, no routine reminders
        var routineRemindersInList = result2.Items
            .Where(r => r.SuggestedAction == observedAction && 
                       r.CustomData != null && 
                       r.CustomData.ContainsKey("source") && 
                       r.CustomData["source"] == "routine")
            .ToList();
        routineRemindersInList.Should().BeEmpty("Routine reminders should NEVER appear in general reminder lists");

        // Also verify: No general reminders with the same action should exist
        // (unless they were created outside learning windows)
        var generalRemindersWithSameAction = result2.Items
            .Where(r => r.SuggestedAction == observedAction)
            .ToList();
        
        // If any exist, they should NOT be routine reminders
        foreach (var reminder in generalRemindersWithSameAction)
        {
            reminder.CustomData.Should().NotContainKey("source");
            // Or if it has source, it should not be "routine"
            if (reminder.CustomData != null && reminder.CustomData.ContainsKey("source"))
            {
                reminder.CustomData["source"].Should().NotBe("routine");
            }
        }

        // Verify routine reminders still exist in routine
        var routine2 = await _routineRepository.GetByPersonAndIntentAsync(_testPersonId, intentType, CancellationToken.None);
        routine2.Should().NotBeNull();
        var routineReminders2 = await _routineReminderRepository.GetByRoutineAsync(routine2!.Id, CancellationToken.None);
        routineReminders2.Should().Contain(r => r.SuggestedAction == observedAction);
    }
}

