// EF Core DbContext for the application
namespace AIPatterner.Infrastructure.Persistence;

using AIPatterner.Domain.Entities;
using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ActionEvent> ActionEvents { get; set; }
    public DbSet<ActionTransition> ActionTransitions { get; set; }
    public DbSet<ReminderCandidate> ReminderCandidates { get; set; }
    public DbSet<ReminderCooldown> ReminderCooldowns { get; set; }
    public DbSet<UserReminderPreferences> UserReminderPreferences { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }
    public DbSet<Configuration> Configurations { get; set; }
    public DbSet<ExecutionHistory> ExecutionHistories { get; set; }
    public DbSet<Routine> Routines { get; set; }
    public DbSet<RoutineReminder> RoutineReminders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ActionEvent>(entity =>
        {
            entity.ToTable("actionevents"); // lowercase
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PersonId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ActionType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ProbabilityValue).HasPrecision(18, 4);
            entity.Property(e => e.ProbabilityAction).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.EventType)
                .HasConversion<int>()
                .HasDefaultValue(EventType.Action);
            entity.Property(e => e.CustomData)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null))
                .HasColumnType("jsonb");
            entity.HasIndex(e => new { e.PersonId, e.TimestampUtc });
            entity.HasIndex(e => e.RelatedReminderId);
            entity.OwnsOne(e => e.Context, context =>
            {
                context.Property(c => c.TimeBucket).IsRequired().HasMaxLength(50);
                context.Property(c => c.DayType).IsRequired().HasMaxLength(50);
                context.Property(c => c.Location).HasMaxLength(200);
                context.Property(c => c.PresentPeople)
                    .HasConversion(
                        v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                        v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
                    .HasColumnType("jsonb");
                context.Property(c => c.StateSignals)
                    .HasConversion(
                        v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                        v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, string>())
                    .HasColumnType("jsonb");
                context.OwnedEntityType.SetPropertyAccessMode(PropertyAccessMode.Field);
            });
        });

        modelBuilder.Entity<ActionTransition>(entity =>
        {
            entity.ToTable("actiontransitions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PersonId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.FromAction).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ToAction).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ContextBucket).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Confidence).HasPrecision(18, 4);
            entity.HasIndex(e => new { e.PersonId, e.FromAction, e.ContextBucket });
            entity.HasIndex(e => new { e.PersonId, e.ToAction });
        });

        modelBuilder.Entity<ReminderCandidate>(entity =>
        {
            entity.ToTable("remindercandidates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PersonId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SuggestedAction).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Confidence).HasPrecision(18, 4);
            entity.Property(e => e.Occurrence).HasMaxLength(200);
            entity.Property(e => e.CustomData)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null))
                .HasColumnType("jsonb");
            
            // Evidence tracking fields for gradual pattern learning
            entity.Property(e => e.TimeWindowCenter)
                .HasConversion(
                    v => v.HasValue ? v.Value.Ticks : (long?)null,
                    v => v.HasValue ? TimeSpan.FromTicks(v.Value) : (TimeSpan?)null)
                .IsRequired(false);
            entity.Property(e => e.TimeWindowSizeMinutes).HasDefaultValue(45);
            entity.Property(e => e.EvidenceCount).HasDefaultValue(0);
            entity.Property(e => e.ObservedDaysJson).HasColumnType("text");
            entity.Property(e => e.ObservedDayOfWeekHistogramJson).HasColumnType("text");
            entity.Property(e => e.PatternInferenceStatus)
                .HasConversion<int>()
                .HasDefaultValue(PatternInferenceStatus.Unknown);
            entity.Property(e => e.InferredWeekday).IsRequired(false);
            entity.Property(e => e.UserPromptsListJson).HasColumnType("jsonb");
            entity.Property(e => e.IsSafeToAutoExecute).HasDefaultValue(false);
            entity.Property(e => e.SignalProfileJson).HasColumnType("jsonb");
            entity.Property(e => e.SignalProfileUpdatedAtUtc).IsRequired(false);
            entity.Property(e => e.SignalProfileSamplesCount).HasDefaultValue(0);
            
            entity.HasIndex(e => e.CheckAtUtc);
            entity.HasIndex(e => e.SourceEventId);
            entity.HasIndex(e => new { e.PersonId, e.Status });
            entity.HasIndex(e => new { e.PersonId, e.SuggestedAction, e.CheckAtUtc });
            entity.OwnsOne(e => e.Decision, decision =>
            {
                decision.Property(d => d.ShouldSpeak);
                decision.Property(d => d.SpeechTemplateKey).HasMaxLength(200);
                decision.Property(d => d.Reason).HasMaxLength(500);
                decision.Property(d => d.ConfidenceLevel).HasPrecision(18, 4);
                decision.Property(d => d.NaturalLanguagePhrase).HasMaxLength(1000);
                decision.OwnedEntityType.SetPropertyAccessMode(PropertyAccessMode.Field);
            });
        });

        modelBuilder.Entity<ReminderCooldown>(entity =>
        {
            entity.ToTable("remindercooldowns");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PersonId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ActionType).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => new { e.PersonId, e.ActionType, e.SuppressedUntilUtc });
        });

        modelBuilder.Entity<UserReminderPreferences>(entity =>
        {
            entity.ToTable("userreminderpreferences");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PersonId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.AllowAutoExecute).HasDefaultValue(false);
            entity.HasIndex(e => e.PersonId).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.ToTable("apikeys");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.KeyHash).IsRequired().HasMaxLength(500);
            entity.Property(e => e.KeyPrefix).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PersonId).HasMaxLength(100);
            entity.HasIndex(e => e.KeyHash);
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<Configuration>(entity =>
        {
            entity.ToTable("configurations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Value).HasMaxLength(2000);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasIndex(e => new { e.Key, e.Category }).IsUnique();
        });

        modelBuilder.Entity<ExecutionHistory>(entity =>
        {
            entity.ToTable("executionhistories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Endpoint).IsRequired().HasMaxLength(500);
            entity.Property(e => e.RequestPayload).IsRequired().HasColumnType("text");
            entity.Property(e => e.ResponsePayload).IsRequired().HasColumnType("text");
            entity.Property(e => e.PersonId).HasMaxLength(100);
            entity.Property(e => e.UserId).HasMaxLength(100);
            entity.Property(e => e.ActionType).HasMaxLength(100);
            entity.HasIndex(e => e.ExecutedAtUtc);
            entity.HasIndex(e => new { e.PersonId, e.ExecutedAtUtc });
            entity.HasIndex(e => e.ReminderCandidateId);
            entity.HasIndex(e => e.EventId);
        });

        modelBuilder.Entity<Routine>(entity =>
        {
            entity.ToTable("routines");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PersonId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IntentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ObservationWindowMinutes).HasDefaultValue(60);
            entity.HasIndex(e => new { e.PersonId, e.IntentType }).IsUnique();
            entity.HasIndex(e => e.PersonId);
        });

        modelBuilder.Entity<RoutineReminder>(entity =>
        {
            entity.ToTable("routinereminders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RoutineId).IsRequired();
            entity.Property(e => e.PersonId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SuggestedAction).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Confidence).HasPrecision(18, 4);
            entity.Property(e => e.CustomData)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null))
                .HasColumnType("jsonb");
            entity.Property(e => e.UserPromptsListJson).HasColumnType("jsonb");
            entity.Property(e => e.IsSafeToAutoExecute).HasDefaultValue(false);
            entity.Property(e => e.SignalProfileJson).HasColumnType("jsonb");
            entity.Property(e => e.SignalProfileUpdatedAtUtc).IsRequired(false);
            entity.Property(e => e.SignalProfileSamplesCount).HasDefaultValue(0);
            entity.HasIndex(e => new { e.RoutineId, e.SuggestedAction }).IsUnique();
            entity.HasIndex(e => e.RoutineId);
            entity.HasIndex(e => e.PersonId);
        });
    }
}

