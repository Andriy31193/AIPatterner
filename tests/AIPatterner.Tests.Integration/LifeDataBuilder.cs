// Data builder for creating realistic life simulation data (3-5 months) without cleanup
// This creates data that remains in the database for inspection
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class LifeDataBuilder : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IngestEventCommandHandler _eventHandler;
    private readonly SubmitFeedbackCommandHandler _feedbackHandler;
    private readonly IRoutineRepository _routineRepository;
    private readonly IRoutineReminderRepository _routineReminderRepository;
    private readonly IReminderCandidateRepository _reminderRepository;
    private readonly IRoutineLearningService _routineLearningService;
    
    // Person IDs
    private const string PiotrId = "builder_piotr";
    private const string VictoriaId = "builder_victoria";
    private const string AndriiId = "builder_andrii";
    
    public LifeDataBuilder()
    {
        // Use real database connection
        var connectionString = "Host=localhost;Port=5433;Database=aipatterner;Username=postgres;Password=postgres";
        
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        
        _context = new ApplicationDbContext(options);
        
        try
        {
            _context.Database.Migrate();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to connect to database: {ex.Message}", ex);
        }
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Learning:SessionWindowMinutes", "30" },
                { "Learning:ConfidenceAlpha", "0.1" },
                { "Learning:DelayBeta", "0.2" },
                { "ContextBucket:Format", "{dayType}*{timeBucket}*{location}" },
                { "Policy:MinimumOccurrences", "1" },
                { "Policy:MinimumConfidence", "0.05" },
                { "Policy:DefaultReminderConfidence", "0.5" },
                { "Policies:RoutineObservationWindowMinutes", "45" },
                { "Policies:TimeOffsetMinutes", "45" },
                { "Policies:MatchByStateSignals", "true" },
                { "Policies:ExecuteAutoThreshold", "0.95" }
            })
            .Build();
        
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var eventRepo = new EventRepository(_context);
        var reminderRepo = new ReminderCandidateRepository(_context);
        var transitionRepo = new TransitionRepository(_context);
        var contextBucketBuilder = new ContextBucketKeyBuilder(configuration);
        
        var transitionLearner = new TransitionLearner(
            eventRepo,
            transitionRepo,
            contextBucketBuilder,
            configuration,
            loggerFactory.CreateLogger<TransitionLearner>());
        
        var policyEvaluator = new ReminderPolicyEvaluator(
            configuration,
            loggerFactory.CreateLogger<ReminderPolicyEvaluator>());
        
        var reminderScheduler = new ReminderScheduler(
            _context,
            transitionRepo,
            policyEvaluator,
            configuration,
            loggerFactory.CreateLogger<ReminderScheduler>());
        
        var mapper = new AutoMapper.Mapper(new AutoMapper.MapperConfiguration(cfg =>
            cfg.AddProfile<AIPatterner.Application.Mappings.MappingProfile>()));
        
        var mockExecutionHistoryService = new MockExecutionHistoryService();
        var configRepo = new ConfigurationRepository(_context);
        var matchingPolicyService = new MatchingPolicyService(configRepo, configuration);
        var signalSelector = new AIPatterner.Infrastructure.Services.SignalSelector(configuration, loggerFactory.CreateLogger<AIPatterner.Infrastructure.Services.SignalSelector>());
        var similarityEvaluator = new AIPatterner.Infrastructure.Services.SignalSimilarityEvaluator(loggerFactory.CreateLogger<AIPatterner.Infrastructure.Services.SignalSimilarityEvaluator>());
        var signalPolicyService = new AIPatterner.Infrastructure.Services.SignalPolicyService(configRepo, configuration);
        var matchingRemindersService = new MatchingRemindersService(eventRepo, _context, mapper, signalSelector, similarityEvaluator, signalPolicyService, loggerFactory.CreateLogger<MatchingRemindersService>());
        
        var routineRepo = new RoutineRepository(_context);
        var routineReminderRepo = new RoutineReminderRepository(_context);
        _routineLearningService = new RoutineLearningService(
            routineRepo,
            routineReminderRepo,
            reminderRepo,
            eventRepo,
            configuration,
            loggerFactory.CreateLogger<RoutineLearningService>(),
            signalSelector,
            similarityEvaluator,
            signalPolicyService);
        
        _eventHandler = new IngestEventCommandHandler(
            eventRepo,
            transitionLearner,
            reminderScheduler,
            reminderRepo,
            mapper,
            mockExecutionHistoryService,
            configuration,
            matchingRemindersService,
            matchingPolicyService,
            _routineLearningService,
            signalSelector,
            signalPolicyService);
        
        var cooldownService = new CooldownService(_context, loggerFactory.CreateLogger<CooldownService>());
        _feedbackHandler = new SubmitFeedbackCommandHandler(
            reminderRepo,
            transitionRepo,
            cooldownService);
        
        _routineRepository = routineRepo;
        _routineReminderRepository = routineReminderRepo;
        _reminderRepository = reminderRepo;
        
        // Setup matching policies
        SetupMatchingPoliciesAsync().GetAwaiter().GetResult();
    }
    
    private async Task SetupMatchingPoliciesAsync()
    {
        var configRepo = new ConfigurationRepository(_context);
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
    
    public async Task BuildLifeDataAsync(int monthsToSimulate = 4)
    {
        Console.WriteLine($"=== Building {monthsToSimulate} months of life simulation data ===");
        Console.WriteLine($"Person IDs: {PiotrId}, {VictoriaId}, {AndriiId}");
        Console.WriteLine($"Start time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n");
        
        var startTime = DateTime.UtcNow.AddMonths(-monthsToSimulate);
        var random = new Random(12345); // Deterministic seed
        var totalDays = monthsToSimulate * 30;
        var eventCount = 0;
        var feedbackCount = 0;
        
        // Track reminders for feedback simulation
        var remindersForFeedback = new List<(string personId, Guid reminderId, string actionType, DateTime createdAt)>();
        var routineRemindersForFeedback = new List<(string personId, Guid routineId, Guid reminderId, string actionType, DateTime createdAt)>();
        
        for (int day = 0; day < totalDays; day++)
        {
            var currentDate = startTime.AddDays(day);
            var isWeekend = currentDate.DayOfWeek == DayOfWeek.Saturday || currentDate.DayOfWeek == DayOfWeek.Sunday;
            
            if (day % 30 == 0)
            {
                Console.WriteLine($"Progress: Day {day}/{totalDays} ({currentDate:yyyy-MM-dd}) - Events: {eventCount}, Feedback: {feedbackCount}");
            }
            
            // Piotr's daily patterns
            if (!isWeekend || random.NextDouble() < 0.3) // Sometimes active on weekends
            {
                // Piotr arrives home (evening, 6-9 PM)
                if (random.NextDouble() < 0.7) // 70% chance
                {
                    var arrivalTime = currentDate.AddHours(18 + random.Next(0, 4));
                    await LifeDataBuilderHelper.CreateArrivalHomeEventAsync(_eventHandler, PiotrId, arrivalTime, random);
                    eventCount++;
                    
                    // 80% chance: sits on couch
                    if (random.NextDouble() < 0.8)
                    {
                        var couchTime = arrivalTime.AddMinutes(30 + random.NextDouble() * 15);
                        await LifeDataBuilderHelper.CreateSitOnCouchEventAsync(_eventHandler, PiotrId, couchTime, random);
                        eventCount++;
                        
                        // 70% chance: plays music
                        if (random.NextDouble() < 0.7)
                        {
                            var musicTime = couchTime.AddMinutes(2 + random.NextDouble() * 5);
                            await LifeDataBuilderHelper.CreatePlayMusicEventAsync(_eventHandler, PiotrId, musicTime, random);
                            eventCount++;
                            
                            // Sometimes user rejects music (10% chance)
                            if (random.NextDouble() < 0.1)
                            {
                                await SimulateNegativeFeedbackAsync(PiotrId, "play_music", musicTime, false);
                                feedbackCount++;
                            }
                        }
                    }
                    
                    // 60% chance: boils kettle
                    if (random.NextDouble() < 0.6)
                    {
                        var kettleTime = arrivalTime.AddMinutes(40 + random.NextDouble() * 20);
                        await LifeDataBuilderHelper.CreateBoilKettleEventAsync(_eventHandler, PiotrId, kettleTime, random);
                        eventCount++;
                    }
                }
                
                // Piotr random daily events (2-4 per day)
                var randomEvents = random.Next(2, 5);
                var piotrActions = new[] { "laptop_active", "phone_screen_on", "tv_on", "turn_on_lights", "open_fridge", "use_microwave" };
                for (int i = 0; i < randomEvents; i++)
                {
                    var eventTime = currentDate.AddHours(6 + random.Next(0, 18)).AddMinutes(random.Next(0, 60));
                    await LifeDataBuilderHelper.CreateRandomDailyEventAsync(_eventHandler, PiotrId, eventTime, random, piotrActions);
                    eventCount++;
                }
            }
            
            // Victoria's daily patterns
            if (!isWeekend)
            {
                // Victoria morning routine (7-8 AM, weekdays only)
                var wakeTime = currentDate.AddHours(7).AddMinutes(15 + random.Next(-15, 15));
                await LifeDataBuilderHelper.CreateWakeUpEventAsync(_eventHandler, VictoriaId, wakeTime, random);
                eventCount++;
                
                // 90% chance: Kitchen routine
                if (random.NextDouble() < 0.9)
                {
                    var kitchenTime = wakeTime.AddMinutes(5 + random.NextDouble() * 5);
                    await LifeDataBuilderHelper.CreateKitchenRoutineEventsAsync(_eventHandler, VictoriaId, kitchenTime, random);
                    eventCount += 2; // Two events (lights + coffee)
                }
                
                // Victoria random events (1-3 per day)
                var victoriaEvents = random.Next(1, 4);
                var victoriaActions = new[] { "cook_on_stove", "use_microwave", "open_fridge", "turn_on_lights", "tv_on" };
                for (int i = 0; i < victoriaEvents; i++)
                {
                    var eventTime = currentDate.AddHours(8 + random.Next(0, 14)).AddMinutes(random.Next(0, 60));
                    await LifeDataBuilderHelper.CreateRandomDailyEventAsync(_eventHandler, VictoriaId, eventTime, random, victoriaActions);
                    eventCount++;
                }
            }
            
            // Andrii's random patterns
            var andriiEvents = random.Next(1, 4);
            var andriiActions = new[] { "andrii_random_action_1", "andrii_random_action_2", "andrii_random_action_3", "tv_on", "phone_active", "open_fridge" };
            for (int i = 0; i < andriiEvents; i++)
            {
                var eventTime = currentDate.AddHours(8 + random.Next(0, 15)).AddMinutes(random.Next(0, 60));
                await LifeDataBuilderHelper.CreateRandomDailyEventAsync(_eventHandler, AndriiId, eventTime, random, andriiActions);
                eventCount++;
            }
            
            // Simulate feedback on existing reminders (every 5-10 days)
            if (day % random.Next(5, 11) == 0 && day > 10)
            {
                await SimulateRandomFeedbackAsync(currentDate, random);
                feedbackCount++;
            }
        }
        
        Console.WriteLine($"\n=== Data building complete ===");
        Console.WriteLine($"Total events created: {eventCount}");
        Console.WriteLine($"Total feedback events: {feedbackCount}");
        
        // Show summary
        await PrintSummaryAsync();
    }
    
    private async Task SimulateNegativeFeedbackAsync(string personId, string actionType, DateTime eventTime, bool isRoutineReminder)
    {
        try
        {
            if (isRoutineReminder)
            {
                // For routine reminders, use routine learning service
                var routines = await _routineRepository.GetByPersonAsync(personId, CancellationToken.None);
                foreach (var routine in routines)
                {
                    var reminders = await _routineReminderRepository.GetByRoutineAsync(routine.Id, CancellationToken.None);
                    var reminder = reminders.FirstOrDefault(r => r.SuggestedAction == actionType);
                    if (reminder != null)
                    {
                        await _routineLearningService.HandleFeedbackAsync(
                            reminder.Id,
                            ProbabilityAction.Decrease,
                            0.1,
                            CancellationToken.None);
                        return;
                    }
                }
            }
            else
            {
                // For general reminders, submit feedback
                var reminders = await _reminderRepository.GetFilteredAsync(personId, null, null, null, 1, 100, CancellationToken.None);
                var reminder = reminders.FirstOrDefault(r => r.SuggestedAction == actionType && 
                    r.CreatedAtUtc >= eventTime.AddHours(-2) && 
                    r.CreatedAtUtc <= eventTime.AddHours(2));
                
                if (reminder != null)
                {
                    var feedback = new FeedbackDto
                    {
                        CandidateId = reminder.Id,
                        FeedbackType = "no"
                    };
                    await _feedbackHandler.Handle(new SubmitFeedbackCommand { Feedback = feedback }, CancellationToken.None);
                }
            }
        }
        catch (Exception ex)
        {
            // Silently continue if feedback can't be applied (reminder might not exist yet)
            Console.WriteLine($"  Note: Could not apply feedback for {personId}/{actionType}: {ex.Message}");
        }
    }
    
    private async Task SimulateRandomFeedbackAsync(DateTime date, Random random)
    {
        // Get some reminders and randomly provide negative feedback (15% chance)
        var allReminders = await _reminderRepository.GetFilteredAsync(null, "Scheduled", date.AddDays(-7), date, 1, 50, CancellationToken.None);
        
        if (allReminders.Count > 0 && random.NextDouble() < 0.15)
        {
            var reminder = allReminders[random.Next(allReminders.Count)];
            var feedback = new FeedbackDto
            {
                CandidateId = reminder.Id,
                FeedbackType = random.NextDouble() < 0.7 ? "no" : "yes" // 70% negative, 30% positive
            };
            
            try
            {
                await _feedbackHandler.Handle(new SubmitFeedbackCommand { Feedback = feedback }, CancellationToken.None);
            }
            catch
            {
                // Ignore errors
            }
        }
        
        // Also provide feedback on routine reminders
        var allRoutines = await _routineRepository.GetFilteredAsync(null, 1, 100, CancellationToken.None);
        foreach (var routine in allRoutines.Take(5)) // Limit to 5 routines
        {
            var reminders = await _routineReminderRepository.GetByRoutineAsync(routine.Id, CancellationToken.None);
            if (reminders.Count > 0 && random.NextDouble() < 0.1) // 10% chance
            {
                var reminder = reminders[random.Next(reminders.Count)];
                var action = random.NextDouble() < 0.6 ? ProbabilityAction.Decrease : ProbabilityAction.Increase;
                
                try
                {
                    await _routineLearningService.HandleFeedbackAsync(reminder.Id, action, 0.1, CancellationToken.None);
                }
                catch
                {
                    // Ignore errors
                }
            }
        }
    }
    
    private async Task PrintSummaryAsync()
    {
        Console.WriteLine("\n=== Database Summary ===");
        
        var piotrRoutines = await _routineRepository.GetByPersonAsync(PiotrId, CancellationToken.None);
        var victoriaRoutines = await _routineRepository.GetByPersonAsync(VictoriaId, CancellationToken.None);
        var andriiRoutines = await _routineRepository.GetByPersonAsync(AndriiId, CancellationToken.None);
        
        Console.WriteLine($"\nRoutines:");
        Console.WriteLine($"  Piotr: {piotrRoutines.Count}");
        foreach (var routine in piotrRoutines)
        {
            var reminders = await _routineReminderRepository.GetByRoutineAsync(routine.Id, CancellationToken.None);
            Console.WriteLine($"    - {routine.IntentType}: {reminders.Count} reminders");
        }
        
        Console.WriteLine($"  Victoria: {victoriaRoutines.Count}");
        foreach (var routine in victoriaRoutines)
        {
            var reminders = await _routineReminderRepository.GetByRoutineAsync(routine.Id, CancellationToken.None);
            Console.WriteLine($"    - {routine.IntentType}: {reminders.Count} reminders");
        }
        
        Console.WriteLine($"  Andrii: {andriiRoutines.Count}");
        
        var piotrReminders = await _reminderRepository.GetFilteredAsync(PiotrId, null, null, null, 1, 1000, CancellationToken.None);
        var victoriaReminders = await _reminderRepository.GetFilteredAsync(VictoriaId, null, null, null, 1, 1000, CancellationToken.None);
        var andriiReminders = await _reminderRepository.GetFilteredAsync(AndriiId, null, null, null, 1, 1000, CancellationToken.None);
        
        Console.WriteLine($"\nGeneral Reminders:");
        Console.WriteLine($"  Piotr: {piotrReminders.Count}");
        Console.WriteLine($"  Victoria: {victoriaReminders.Count}");
        Console.WriteLine($"  Andrii: {andriiReminders.Count}");
        
        var piotrEvents = await _context.ActionEvents.CountAsync(e => e.PersonId == PiotrId);
        var victoriaEvents = await _context.ActionEvents.CountAsync(e => e.PersonId == VictoriaId);
        var andriiEvents = await _context.ActionEvents.CountAsync(e => e.PersonId == AndriiId);
        
        Console.WriteLine($"\nEvents:");
        Console.WriteLine($"  Piotr: {piotrEvents}");
        Console.WriteLine($"  Victoria: {victoriaEvents}");
        Console.WriteLine($"  Andrii: {andriiEvents}");
        
        Console.WriteLine($"\nData is ready for inspection in the database!");
        Console.WriteLine($"Person IDs: {PiotrId}, {VictoriaId}, {AndriiId}");
    }
    
    public void Dispose()
    {
        // DO NOT CLEAN UP - data should remain in database
        _context?.Dispose();
    }
}

