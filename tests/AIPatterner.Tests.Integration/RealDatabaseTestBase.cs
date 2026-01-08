// Base class for integration tests using real PostgreSQL database
namespace AIPatterner.Tests.Integration;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Application.Handlers;
using AIPatterner.Application.Services;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using AIPatterner.Infrastructure.Persistence.Repositories;
using AIPatterner.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

public abstract class RealDatabaseTestBase : IDisposable
{
    protected readonly ApplicationDbContext Context;
    protected readonly IEventRepository EventRepository;
    protected readonly IReminderCandidateRepository ReminderRepository;
    protected readonly ITransitionRepository TransitionRepository;
    protected readonly IConfiguration Configuration;
    protected readonly IngestEventCommandHandler EventHandler;
    protected readonly HttpClient HttpClient;
    protected readonly string ApiBaseUrl;
    protected readonly string ApiKey;

    protected RealDatabaseTestBase()
    {
        //REVERT
        // Get connection string from environment or use default
        var connectionString = "Host=localhost;Port=5433;Database=aipatterner;Username=postgres;Password=postgres";

        // Get API base URL from environment or use default
        ApiBaseUrl = "http://localhost:8080/api";

        // Get API key from environment or use provided
        ApiKey = "ak_JyqivmKSDskny2gO4s2Zafhxlmcw7Kn2FnFg9tEV2vPoajsjKvcJjSmY2oUoag5G";

        // Setup database context with real PostgreSQL
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        Context = new ApplicationDbContext(options);

        // Ensure database is created and migrations are applied
        try
        {
            // Only run migrations, don't use EnsureCreated as it conflicts with migrations
            Context.Database.Migrate();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to connect to database: {ex.Message}. Please ensure PostgreSQL is running and connection string is correct.", ex);
        }

        // Clean up test data before starting
        CleanupTestData();

        EventRepository = new EventRepository(Context);
        ReminderRepository = new ReminderCandidateRepository(Context);
        TransitionRepository = new TransitionRepository(Context);

        Configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Learning:SessionWindowMinutes", "30" },
                { "Learning:ConfidenceAlpha", "0.1" },
                { "Learning:DelayBeta", "0.2" },
                { "ContextBucket:Format", "{dayType}*{timeBucket}*{location}" },
                { "Policy:MinimumOccurrences", "1" },
                { "Policy:MinimumConfidence", "0.05" },
                { "Policy:DefaultReminderConfidence", "0.5" },
                { "Policies:RoutineObservationWindowMinutes", "45" }, // Set to 45 for tests to match expectations
                { "Policies:TimeOffsetMinutes", "45" }, // Set to 45 for tests
                { "Policies:MatchByStateSignals", "true" },
                { "Policies:ExecuteAutoThreshold", "0.95" }
            })
            .Build();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var contextBucketBuilder = new ContextBucketKeyBuilder(Configuration);
        var transitionLearner = new TransitionLearner(
            EventRepository,
            TransitionRepository,
            contextBucketBuilder,
            Configuration,
            loggerFactory.CreateLogger<TransitionLearner>());

        var policyEvaluator = new ReminderPolicyEvaluator(
            Configuration,
            loggerFactory.CreateLogger<ReminderPolicyEvaluator>());

        var reminderScheduler = new ReminderScheduler(
            Context,
            TransitionRepository,
            policyEvaluator,
            Configuration,
            loggerFactory.CreateLogger<ReminderScheduler>());

        var mapper = new AutoMapper.Mapper(new AutoMapper.MapperConfiguration(cfg =>
            cfg.AddProfile<AIPatterner.Application.Mappings.MappingProfile>()));

        var mockExecutionHistoryService = new MockExecutionHistoryService();
        var configRepo = new ConfigurationRepository(Context);
        var matchingPolicyService = new MatchingPolicyService(configRepo, Configuration);
        var signalSelector = new AIPatterner.Infrastructure.Services.SignalSelector(Configuration, loggerFactory.CreateLogger<AIPatterner.Infrastructure.Services.SignalSelector>());
        var similarityEvaluator = new AIPatterner.Infrastructure.Services.SignalSimilarityEvaluator(loggerFactory.CreateLogger<AIPatterner.Infrastructure.Services.SignalSimilarityEvaluator>());
        var signalPolicyService = new AIPatterner.Infrastructure.Services.SignalPolicyService(configRepo, Configuration);
        var matchingRemindersService = new MatchingRemindersService(EventRepository, Context, mapper, signalSelector, similarityEvaluator, signalPolicyService, loggerFactory.CreateLogger<MatchingRemindersService>());
        
        var routineRepository = new RoutineRepository(Context);
        var routineReminderRepository = new RoutineReminderRepository(Context);
        var routineLearningService = new RoutineLearningService(
            routineRepository,
            routineReminderRepository,
            ReminderRepository,
            EventRepository,
            Configuration,
            loggerFactory.CreateLogger<RoutineLearningService>(),
            signalSelector,
            similarityEvaluator,
            signalPolicyService);

        EventHandler = new IngestEventCommandHandler(
            EventRepository,
            transitionLearner,
            reminderScheduler,
            ReminderRepository,
            mapper,
            mockExecutionHistoryService,
            Configuration,
            matchingRemindersService,
            matchingPolicyService,
            routineLearningService,
            signalSelector,
            signalPolicyService);

        // Setup matching policies
        SetupMatchingPoliciesAsync().GetAwaiter().GetResult();

        // Setup HTTP client with API key
        HttpClient = new HttpClient { BaseAddress = new Uri(ApiBaseUrl) };
        HttpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        HttpClient.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
    }

    private async Task SetupMatchingPoliciesAsync()
    {
        var configRepo = new ConfigurationRepository(Context);
        var policies = new[]
        {
            ("MatchByActionType", "true", "Match by action type"),
            ("MatchByDayType", "true", "Match by day type"),
            ("MatchByPeoplePresent", "true", "Match by people present"),
            ("MatchByStateSignals", "true", "Match by state signals"),
            ("MatchByTimeBucket", "false", "Match by time bucket"),
            ("MatchByLocation", "false", "Match by location"),
            ("TimeOffsetMinutes", "45", "Time offset in minutes")
        };

        foreach (var (key, value, description) in policies)
        {
            var existing = await configRepo.GetByKeyAndCategoryAsync(key, "MatchingPolicy", CancellationToken.None);
            if (existing == null)
            {
                var config = new Configuration(key, value, "MatchingPolicy", description);
                await configRepo.AddAsync(config, CancellationToken.None);
            }
        }
    }

    protected virtual void CleanupTestData()
    {
        // Clean up test data with specific prefixes
        var testPersonIds = new[] { "user", "api_user", "api_test_user", "api_related_user", "api_feedback_user", 
            "feedback_user", "daily_user", "weekly_user", "user_a", "user_b", "user_c", "routine_test_user",
            "event_person", "reminder_person", "routine_person", "duplicate_test_person", "matched_user",
            "user_for_id", "testuser_dual", "testuser1", "testuser2", "adminuser", "comprehensive_test_user",
            "household_person_a", "household_person_b", "household_person_c", 
            "life_sim_piotr", "life_sim_victoria", "life_sim_andrii" };

        // Also clean up personIds that start with comprehensive_test_user (for sub-tests)
        var comprehensiveTestPersonIds = Context.ActionEvents
            .Where(e => e.PersonId.StartsWith("comprehensive_test_user"))
            .Select(e => e.PersonId)
            .Distinct()
            .ToList();

        foreach (var personId in comprehensiveTestPersonIds)
        {
            // Delete reminders
            var reminders = Context.ReminderCandidates
                .Where(r => r.PersonId == personId)
                .ToList();
            Context.ReminderCandidates.RemoveRange(reminders);

            // Delete events
            var events = Context.ActionEvents
                .Where(e => e.PersonId == personId)
                .ToList();
            Context.ActionEvents.RemoveRange(events);

            // Delete transitions
            var transitions = Context.ActionTransitions
                .Where(t => t.PersonId == personId)
                .ToList();
            Context.ActionTransitions.RemoveRange(transitions);

            // Delete cooldowns
            var cooldowns = Context.ReminderCooldowns
                .Where(c => c.PersonId == personId)
                .ToList();
            Context.ReminderCooldowns.RemoveRange(cooldowns);

            // Delete routines and routine reminders
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

        // Clean up routines and routine reminders
        var routineTestPersonIds = Context.Routines
            .Where(r => r.PersonId.StartsWith("routine_test_user") || r.PersonId.StartsWith("routine_person"))
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
    }

    public virtual void Dispose()
    {
        //CleanupTestData();
        Context?.Dispose();
        HttpClient?.Dispose();
    }
}

