// Base class for integration tests using real PostgreSQL database
namespace AIPatterner.Tests.Integration;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Application.Handlers;
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
        var connectionString = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5433;Database=aipatterner_test;Username=postgres;Password=postgres";

        // Get API base URL from environment or use default
        ApiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:8080/api";

        // Get API key from environment or use provided
        ApiKey = Environment.GetEnvironmentVariable("TEST_API_KEY")
            ?? "ak_hP2nIKfURZWrA8Qun2iJw0NN2f89kWCwxA844Wk4EbPjXx3t1AGQ1TICsDLvVrxV";

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
                { "Policy:DefaultReminderConfidence", "0.5" }
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
        var matchingRemindersService = new MatchingRemindersService(EventRepository, Context, mapper);

        EventHandler = new IngestEventCommandHandler(
            EventRepository,
            transitionLearner,
            reminderScheduler,
            ReminderRepository,
            mapper,
            mockExecutionHistoryService,
            Configuration,
            matchingRemindersService,
            matchingPolicyService);

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
            ("TimeOffsetMinutes", "30", "Time offset in minutes")
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

    protected void CleanupTestData()
    {
        // Clean up test data with specific prefixes
        var testPersonIds = new[] { "user", "api_user", "api_test_user", "api_related_user", "api_feedback_user", 
            "feedback_user", "daily_user", "weekly_user", "user_a", "user_b", "user_c" };

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

        Context.SaveChanges();
    }

    public virtual void Dispose()
    {
        CleanupTestData();
        Context?.Dispose();
        HttpClient?.Dispose();
    }
}

