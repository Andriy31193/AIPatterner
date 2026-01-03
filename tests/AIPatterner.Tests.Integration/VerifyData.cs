// Quick verification to check if test data persists
namespace AIPatterner.Tests.Integration;

using AIPatterner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class VerifyData
{
    public static async Task Run()
    {
        var connectionString = "Host=localhost;Port=5433;Database=aipatterner;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        using var context = new ApplicationDbContext(options);

        var personIds = new[] { "household_person_a", "household_person_b", "household_person_c", "household_person_test" };
        
        Console.WriteLine("Checking for person IDs: " + string.Join(", ", personIds));

        Console.WriteLine("=== Verifying Test Data Persistence ===\n");

        // Check Reminders
        var reminderCount = await context.ReminderCandidates
            .Where(r => personIds.Contains(r.PersonId))
            .CountAsync();
        Console.WriteLine($"Reminders found: {reminderCount}");

        if (reminderCount > 0)
        {
            var reminders = await context.ReminderCandidates
                .Where(r => personIds.Contains(r.PersonId))
                .Select(r => new { r.PersonId, r.SuggestedAction, r.Confidence })
                .Take(10)
                .ToListAsync();
            
            Console.WriteLine("\nSample Reminders:");
            foreach (var r in reminders)
            {
                Console.WriteLine($"  {r.PersonId} -> {r.SuggestedAction} (confidence: {r.Confidence:F2})");
            }
        }

        // Check Routines
        var routineCount = await context.Routines
            .Where(r => personIds.Contains(r.PersonId))
            .CountAsync();
        Console.WriteLine($"\nRoutines found: {routineCount}");

        if (routineCount > 0)
        {
            var routines = await context.Routines
                .Where(r => personIds.Contains(r.PersonId))
                .Select(r => new { r.PersonId, r.IntentType, r.CreatedAtUtc })
                .Take(10)
                .ToListAsync();
            
            Console.WriteLine("\nSample Routines:");
            foreach (var r in routines)
            {
                Console.WriteLine($"  {r.PersonId} -> {r.IntentType} (created: {r.CreatedAtUtc:yyyy-MM-dd HH:mm:ss})");
            }
        }

        // Check Events
        var eventCount = await context.ActionEvents
            .Where(e => personIds.Contains(e.PersonId))
            .CountAsync();
        Console.WriteLine($"\nEvents found: {eventCount}");

        if (eventCount > 0)
        {
            var events = await context.ActionEvents
                .Where(e => personIds.Contains(e.PersonId))
                .OrderByDescending(e => e.TimestampUtc)
                .Select(e => new { e.PersonId, e.ActionType, e.TimestampUtc })
                .Take(10)
                .ToListAsync();
            
            Console.WriteLine("\nSample Events (most recent):");
            foreach (var e in events)
            {
                Console.WriteLine($"  {e.PersonId} -> {e.ActionType} at {e.TimestampUtc:yyyy-MM-dd HH:mm:ss}");
            }
        }

        Console.WriteLine("\n=== Verification Complete ===");
        Console.WriteLine($"Total data items: {reminderCount + routineCount + eventCount}");

        if (reminderCount > 0 || routineCount > 0 || eventCount > 0)
        {
            Console.WriteLine("✓ Data persists in database!");
        }
        else
        {
            Console.WriteLine("✗ No test data found - cleanup may have run");
        }
    }
}

