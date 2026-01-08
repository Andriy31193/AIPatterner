// Service implementation for scheduling reminder candidates
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Application.Handlers;
using AIPatterner.Application.Helpers;
using AIPatterner.Application.Services;
using AIPatterner.Domain.Entities;
using AIPatterner.Domain.Services;
using AIPatterner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class ReminderScheduler : IReminderScheduler
{
    private readonly ApplicationDbContext _context;
    private readonly AIPatterner.Application.Handlers.ITransitionRepository _transitionRepository;
    private readonly IReminderPolicyEvaluator _policyEvaluator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReminderScheduler> _logger;
    private readonly IRoutineLearningService _routineLearningService;

    public ReminderScheduler(
        ApplicationDbContext context,
        AIPatterner.Application.Handlers.ITransitionRepository transitionRepository,
        IReminderPolicyEvaluator policyEvaluator,
        IConfiguration configuration,
        ILogger<ReminderScheduler> logger,
        IRoutineLearningService routineLearningService)
    {
        _context = context;
        _transitionRepository = transitionRepository;
        _policyEvaluator = policyEvaluator;
        _configuration = configuration;
        _logger = logger;
        _routineLearningService = routineLearningService;
    }

    public async Task<List<ReminderCandidate>> ScheduleCandidatesForEventAsync(
        ActionEvent actionEvent,
        CancellationToken cancellationToken)
    {
        // CRITICAL: StateChange events must NOT trigger reminder scheduling
        if (actionEvent.EventType == EventType.StateChange)
        {
            return new List<ReminderCandidate>();
        }

        // CRITICAL: Events within routine learning windows must NOT create general reminders
        // This provides defense in depth - even if called, we check here too
        var isWithinRoutineLearningWindow = await _routineLearningService.IsEventWithinRoutineLearningWindowAsync(
            actionEvent.PersonId,
            actionEvent.TimestampUtc,
            cancellationToken);
        
        if (isWithinRoutineLearningWindow)
        {
            _logger.LogInformation(
                "Skipping reminder scheduling for event {EventId} (Person: {PersonId}, Action: {Action}) - event is within routine learning window",
                actionEvent.Id, actionEvent.PersonId, actionEvent.ActionType);
            return new List<ReminderCandidate>();
        }

        var candidates = new List<ReminderCandidate>();

        var recentTransitions = await _transitionRepository.GetRecentTransitionsForPersonAsync(
            actionEvent.PersonId,
            actionEvent.ActionType,
            cancellationToken);

        var preferences = await _context.UserReminderPreferences
            .FirstOrDefaultAsync(p => p.PersonId == actionEvent.PersonId, cancellationToken);

        if (preferences == null)
        {
            preferences = new UserReminderPreferences(actionEvent.PersonId);
            await _context.UserReminderPreferences.AddAsync(preferences, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var defaultConfidence = _configuration.GetValue<double>("Policy:DefaultReminderConfidence", 0.5);
        var confidenceStep = _configuration.GetValue<double>("Policy:ConfidenceStepValue", 0.1);
        var timeOffsetMinutes = _configuration.GetValue<int>("Policy:ReminderMatchTimeOffsetMinutes", 30);

        foreach (var transition in recentTransitions)
        {
            var policyDecision = await _policyEvaluator.EvaluateAsync(
                transition,
                actionEvent.Context,
                preferences,
                cancellationToken);

            if (policyDecision?.ShouldSchedule == true)
            {
                // Check for existing matching reminder by person+action (to prevent duplicates)
                // Prefer reminders within time window, but accept any scheduled reminder for same person+action
                var existingReminder = await _context.ReminderCandidates
                    .Where(c => 
                        c.PersonId == actionEvent.PersonId &&
                        c.SuggestedAction == transition.ToAction &&
                        c.Status == ReminderCandidateStatus.Scheduled)
                    .OrderByDescending(c => 
                        // Prefer reminders within time window
                        Math.Abs((c.CheckAtUtc - policyDecision.SuggestedCheckAt).TotalMinutes) <= timeOffsetMinutes ? 1 : 0)
                    .ThenByDescending(c => c.CreatedAtUtc)
                    .FirstOrDefaultAsync(cancellationToken);

                if (existingReminder != null)
                {
                    // Increase confidence of existing reminder
                    existingReminder.IncreaseConfidence(confidenceStep);
                    
                    // Record evidence with context information
                    existingReminder.RecordEvidence(
                        actionEvent.TimestampUtc,
                        actionEvent.Context.TimeBucket,
                        actionEvent.Context.DayType);
                    
                    // Update pattern inference
                    var minDailyEvidence = _configuration.GetValue<int>("Policy:MinDailyEvidence", 3);
                    var minWeeklyEvidence = _configuration.GetValue<int>("Policy:MinWeeklyEvidence", 3);
                    existingReminder.UpdateInferredPattern(minDailyEvidence, minWeeklyEvidence);
                    
                    _context.ReminderCandidates.Update(existingReminder);
                    await _context.SaveChangesAsync(cancellationToken);
                    candidates.Add(existingReminder);
                    _logger.LogInformation(
                        "Increased confidence of existing reminder {CandidateId} for {PersonId}, action: {Action}, new confidence: {Confidence}",
                        existingReminder.Id, actionEvent.PersonId, transition.ToAction, existingReminder.Confidence);
                }
                else
                {
                    // Create new reminder with default confidence
                    // CheckAtUtc must be identical to Event TimestampUtc
                    var checkAtUtc = actionEvent.TimestampUtc;
                    
                    // Occurrence will be inferred gradually as evidence accumulates
                    var candidate = new ReminderCandidate(
                        actionEvent.PersonId,
                        transition.ToAction,
                        checkAtUtc, // Use event timestamp
                        policyDecision.Style,
                        transition.Id,
                        defaultConfidence,
                        occurrence: null, // Will be inferred from evidence
                        actionEvent.Id, // SourceEventId
                        actionEvent.CustomData); // Copy CustomData
                    
                    // Record the first evidence with context information
                    candidate.RecordEvidence(
                        actionEvent.TimestampUtc,
                        actionEvent.Context.TimeBucket,
                        actionEvent.Context.DayType);
                    
                    // Update pattern inference
                    var minDailyEvidence = _configuration.GetValue<int>("Policy:MinDailyEvidence", 3);
                    var minWeeklyEvidence = _configuration.GetValue<int>("Policy:MinWeeklyEvidence", 3);
                    candidate.UpdateInferredPattern(minDailyEvidence, minWeeklyEvidence);

                    await _context.ReminderCandidates.AddAsync(candidate, cancellationToken);
                    candidates.Add(candidate);
                    _logger.LogInformation(
                        "Scheduled reminder candidate {CandidateId} for {PersonId}, action: {Action}, check at: {CheckAt}, confidence: {Confidence}",
                        candidate.Id, actionEvent.PersonId, transition.ToAction, checkAtUtc, candidate.Confidence);
                }
            }
        }

        if (candidates.Any())
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return candidates;
    }
}

