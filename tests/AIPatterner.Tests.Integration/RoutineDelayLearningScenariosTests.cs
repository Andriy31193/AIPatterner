// Integration tests for contextual, delay-based routine learning + execution
namespace AIPatterner.Tests.Integration;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using System.Linq;
using Xunit;

public class RoutineDelayLearningScenariosTests : RealDatabaseTestBase
{
    [Fact]
    public async Task Scenario1_MorningVsEvening_BucketsLearnSeparately_AndScheduleByDelay()
    {
        var personId = "routine_person_s1";
        var intentType = "ImHome";

        // Learn "Coffee after ~3 minutes" in morning
        var tMorning1 = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc);
        await EventHandler.Handle(new IngestEventCommand { Event = MakeIntent(personId, intentType, tMorning1) }, CancellationToken.None);
        await EventHandler.Handle(new IngestEventCommand { Event = MakeAction(personId, "Coffee", tMorning1.AddMinutes(3)) }, CancellationToken.None);

        // Learn "TV after ~8 minutes" in evening
        var tEvening1 = new DateTime(2026, 1, 5, 19, 0, 0, DateTimeKind.Utc);
        await EventHandler.Handle(new IngestEventCommand { Event = MakeIntent(personId, intentType, tEvening1) }, CancellationToken.None);
        await EventHandler.Handle(new IngestEventCommand { Event = MakeAction(personId, "TV", tEvening1.AddMinutes(8)) }, CancellationToken.None);

        // Activate again in the morning: should schedule Coffee, not TV
        var tMorning2 = new DateTime(2026, 1, 6, 8, 0, 0, DateTimeKind.Utc);
        var activationResponse = await EventHandler.Handle(
            new IngestEventCommand { Event = MakeIntent(personId, intentType, tMorning2) },
            CancellationToken.None);

        var candidates = await ReminderRepository.GetBySourceEventIdAsync(activationResponse.EventId, CancellationToken.None);
        candidates.Should().NotBeEmpty();

        candidates.Should().ContainSingle(c => c.SuggestedAction == "Coffee");
        candidates.Should().NotContain(c => c.SuggestedAction == "TV");

        var coffee = candidates.Single(c => c.SuggestedAction == "Coffee");
        coffee.CheckAtUtc.Should().Be(tMorning2.AddMinutes(3));
    }

    [Fact]
    public async Task Scenario2_ShiftWork_LearnsRelativeDelay_NotAbsoluteClockTime()
    {
        var personId = "routine_person_s2";
        var intentType = "ImHome";

        var activations = new[]
        {
            new DateTime(2026, 1, 5, 7, 30, 0, DateTimeKind.Utc), // morning
            new DateTime(2026, 1, 8, 9, 10, 0, DateTimeKind.Utc), // morning
            new DateTime(2026, 1, 11, 8, 45, 0, DateTimeKind.Utc), // morning
        };

        foreach (var t in activations)
        {
            await EventHandler.Handle(new IngestEventCommand { Event = MakeIntent(personId, intentType, t) }, CancellationToken.None);
            await EventHandler.Handle(new IngestEventCommand { Event = MakeAction(personId, "Coffee", t.AddMinutes(3)) }, CancellationToken.None);
        }

        var tActivate = new DateTime(2026, 1, 12, 8, 0, 0, DateTimeKind.Utc);
        var activationResponse = await EventHandler.Handle(
            new IngestEventCommand { Event = MakeIntent(personId, intentType, tActivate) },
            CancellationToken.None);

        var candidates = await ReminderRepository.GetBySourceEventIdAsync(activationResponse.EventId, CancellationToken.None);
        var coffee = candidates.Single(c => c.SuggestedAction == "Coffee");

        // Learned delay should be around 2-4 minutes after activation (we trained at 3 minutes)
        coffee.CheckAtUtc.Should().BeOnOrAfter(tActivate.AddMinutes(2));
        coffee.CheckAtUtc.Should().BeOnOrBefore(tActivate.AddMinutes(4));
    }

    [Fact]
    public async Task Scenario3_OutlierHandling_StoresEvidenceButMedianStaysStable()
    {
        var personId = "routine_person_s3";
        var intentType = "ImHome";

        var t1 = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc);
        await EventHandler.Handle(new IngestEventCommand { Event = MakeIntent(personId, intentType, t1) }, CancellationToken.None);
        await EventHandler.Handle(new IngestEventCommand { Event = MakeAction(personId, "Coffee", t1.AddMinutes(2)) }, CancellationToken.None);

        var t2 = new DateTime(2026, 1, 6, 8, 0, 0, DateTimeKind.Utc);
        await EventHandler.Handle(new IngestEventCommand { Event = MakeIntent(personId, intentType, t2) }, CancellationToken.None);
        await EventHandler.Handle(new IngestEventCommand { Event = MakeAction(personId, "Coffee", t2.AddMinutes(2).AddSeconds(10)) }, CancellationToken.None);

        // Outlier: 30 minutes
        var t3 = new DateTime(2026, 1, 7, 8, 0, 0, DateTimeKind.Utc);
        await EventHandler.Handle(new IngestEventCommand { Event = MakeIntent(personId, intentType, t3) }, CancellationToken.None);
        await EventHandler.Handle(new IngestEventCommand { Event = MakeAction(personId, "Coffee", t3.AddMinutes(30)) }, CancellationToken.None);

        var routineRepo = new RoutineRepository(Context);
        var rrRepo = new RoutineReminderRepository(Context);
        var routine = await routineRepo.GetByPersonAndIntentAsync(personId, intentType, CancellationToken.None);
        routine.Should().NotBeNull();

        // Bucket is selected at activation; these were all "morning"
        var reminders = await rrRepo.GetByRoutineAndBucketAsync(routine!.Id, "morning", CancellationToken.None);
        var coffee = reminders.Single(r => r.SuggestedAction == "Coffee");

        coffee.MedianDelayApproxSeconds.Should().NotBeNull();
        coffee.MedianDelayApproxSeconds!.Value.Should().BeLessThan(300); // median stays near ~2-4 minutes, not 30 minutes

        var evidence = coffee.GetDelayEvidence();
        evidence.Should().HaveCount(3);
        evidence.Count(e => e.IsOutlier).Should().Be(1);
    }

    [Fact]
    public async Task Scenario4_LearningWindowEnforcement_IgnoresEventsAfterWindowEnd()
    {
        var personId = "routine_person_s4";
        var intentType = "ImHome";

        var routineRepo = new RoutineRepository(Context);
        var rrRepo = new RoutineReminderRepository(Context);

        // Set routine observation window to 30 minutes so it ends at 18:30
        var tActivate = new DateTime(2026, 1, 5, 18, 0, 0, DateTimeKind.Utc); // evening bucket
        await EventHandler.Handle(new IngestEventCommand { Event = MakeIntent(personId, intentType, tActivate) }, CancellationToken.None);

        var routine = await routineRepo.GetByPersonAndIntentAsync(personId, intentType, CancellationToken.None);
        routine.Should().NotBeNull();
        routine!.UpdateObservationWindowMinutes(30);
        await routineRepo.UpdateAsync(routine, CancellationToken.None);

        // Reactivate so the 30-minute window is applied
        await EventHandler.Handle(new IngestEventCommand { Event = MakeIntent(personId, intentType, tActivate) }, CancellationToken.None);

        // Event at 18:45 is outside window -> must not learn
        await EventHandler.Handle(new IngestEventCommand { Event = MakeAction(personId, "TV", tActivate.AddMinutes(45)) }, CancellationToken.None);

        routine = await routineRepo.GetByPersonAndIntentAsync(personId, intentType, CancellationToken.None);
        routine!.ObservationWindowStartUtc.Should().BeNull(); // closed after seeing late event

        var reminders = await rrRepo.GetByRoutineAndBucketAsync(routine.Id, "evening", CancellationToken.None);
        reminders.Should().BeEmpty();
    }

    private static ActionEventDto MakeIntent(string personId, string actionType, DateTime timestampUtc)
    {
        return new ActionEventDto
        {
            PersonId = personId,
            ActionType = actionType,
            TimestampUtc = timestampUtc,
            EventType = EventType.StateChange,
            Context = new ActionContextDto
            {
                TimeBucket = "unknown",
                DayType = "weekday",
                Location = "home",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>()
            }
        };
    }

    private static ActionEventDto MakeAction(string personId, string actionType, DateTime timestampUtc)
    {
        return new ActionEventDto
        {
            PersonId = personId,
            ActionType = actionType,
            TimestampUtc = timestampUtc,
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = "unknown",
                DayType = "weekday",
                Location = "home",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>()
            }
        };
    }
}


