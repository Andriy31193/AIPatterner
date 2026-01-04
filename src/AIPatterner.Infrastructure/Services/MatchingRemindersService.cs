// Service implementation for finding matching reminders
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Application.DTOs;
using AIPatterner.Application.Handlers;
using AIPatterner.Application.Mappings;
using AIPatterner.Application.Queries;
using AIPatterner.Application.Services;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class MatchingRemindersService : IMatchingRemindersService
{
    private readonly IEventRepository _eventRepository;
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly AIPatterner.Domain.Services.ISignalSelector _signalSelector;
    private readonly AIPatterner.Domain.Services.ISignalSimilarityEvaluator _similarityEvaluator;
    private readonly AIPatterner.Application.Services.ISignalPolicyService _signalPolicyService;
    private readonly Microsoft.Extensions.Logging.ILogger<MatchingRemindersService> _logger;

    public MatchingRemindersService(
        IEventRepository eventRepository,
        ApplicationDbContext context,
        IMapper mapper,
        AIPatterner.Domain.Services.ISignalSelector signalSelector,
        AIPatterner.Domain.Services.ISignalSimilarityEvaluator similarityEvaluator,
        AIPatterner.Application.Services.ISignalPolicyService signalPolicyService,
        Microsoft.Extensions.Logging.ILogger<MatchingRemindersService> logger)
    {
        _eventRepository = eventRepository;
        _context = context;
        _mapper = mapper;
        _signalSelector = signalSelector;
        _similarityEvaluator = similarityEvaluator;
        _signalPolicyService = signalPolicyService;
        _logger = logger;
    }

    public async Task<ReminderCandidateListResponse> FindMatchingRemindersAsync(
        Guid eventId,
        MatchingCriteria criteria,
        List<AIPatterner.Domain.ValueObjects.SignalState>? signalStates,
        CancellationToken cancellationToken)
    {
        // Get the event
        var actionEvent = await _eventRepository.GetByIdAsync(eventId, cancellationToken);
        if (actionEvent == null)
        {
            return new ReminderCandidateListResponse
            {
                Items = new List<ReminderCandidateDto>(),
                TotalCount = 0,
                Page = 1,
                PageSize = 20
            };
        }

        // CRITICAL: StateChange events (intents) must NEVER match general reminders
        // They only activate routines
        if (actionEvent.EventType == EventType.StateChange)
        {
            return new ReminderCandidateListResponse
            {
                Items = new List<ReminderCandidateDto>(),
                TotalCount = 0,
                Page = 1,
                PageSize = 20
            };
        }

        // Start with all scheduled reminders for this person
        var allReminders = await _context.ReminderCandidates
            .Where(r => r.PersonId == actionEvent.PersonId)
            .Where(r => r.Status == ReminderCandidateStatus.Scheduled)
            .ToListAsync(cancellationToken);

        var eventTime = actionEvent.TimestampUtc;
        var matchingReminders = new List<ReminderCandidate>();

        foreach (var reminder in allReminders)
        {
            var matches = true;

            // 1. STRICT: Match by action type if enabled
            if (criteria.MatchByActionType)
            {
                matches = matches && reminder.SuggestedAction == actionEvent.ActionType;
                if (!matches) continue;
            }

            // 2. STRICT: Time offset tolerance check using Policies.TimeOffsetMinutes
            // This applies to ALL reminders (both with and without TimeWindowCenter)
            if (reminder.TimeWindowCenter.HasValue)
            {
                // For reminders with TimeWindowCenter, check against the time window center
                var eventTimeOfDay = eventTime.TimeOfDay;
                var reminderTimeCenter = reminder.TimeWindowCenter.Value;
                
                // Calculate time difference, handling midnight wraparound
                var timeDiff = Math.Abs((eventTimeOfDay - reminderTimeCenter).TotalMinutes);
                if (timeDiff > 12 * 60) // More than 12 hours apart, check wraparound
                {
                    timeDiff = 24 * 60 - timeDiff;
                }
                
                // Use Policies.TimeOffsetMinutes as the tolerance (not reminder's TimeWindowSizeMinutes)
                matches = matches && timeDiff <= criteria.TimeOffsetMinutes;
                if (!matches) continue;
            }
            else
            {
                // Fallback for old reminders without TimeWindowCenter: use CheckAtUtc matching
                var timeDiff = Math.Abs((reminder.CheckAtUtc - eventTime).TotalMinutes);
                matches = matches && timeDiff <= criteria.TimeOffsetMinutes;
                if (!matches) continue;
            }

            // 3. STRICT: State signal matching when enabled
            // If MatchByStateSignals is enabled, check that all state signals in reminder's CustomData
            // are present in the event's Context.StateSignals
            if (criteria.MatchByStateSignals)
            {
                // Reminder's CustomData stores state signal conditions (key-value pairs)
                if (reminder.CustomData != null && reminder.CustomData.Count > 0)
                {
                    // Event must have matching state signals
                    if (actionEvent.Context.StateSignals == null || actionEvent.Context.StateSignals.Count == 0)
                    {
                        matches = false;
                        if (!matches) continue;
                    }
                    else
                    {
                        // All state signals in reminder must be present in event with matching values
                        foreach (var reminderSignal in reminder.CustomData)
                        {
                            if (!actionEvent.Context.StateSignals.ContainsKey(reminderSignal.Key) ||
                                actionEvent.Context.StateSignals[reminderSignal.Key] != reminderSignal.Value)
                            {
                                matches = false;
                                break;
                            }
                        }
                        if (!matches) continue;
                    }
                }
            }

            // 4. Context-based matching (other than state signals, which are handled above)
            // NEW BEHAVIOR: For evidence-based reminders (with TimeWindowCenter), 
            // we skip strict context matching to allow cross-day matching.
            // This enables gradual pattern learning where time-of-day dominates initially.
            // For old-style reminders (without TimeWindowCenter), we still use strict context matching.
            // Note: State signals are already checked above, so we exclude MatchByStateSignals here
            if (!reminder.TimeWindowCenter.HasValue && 
                (criteria.MatchByDayType || criteria.MatchByPeoplePresent || 
                 criteria.MatchByTimeBucket || criteria.MatchByLocation))
            {
                // Find events related to this reminder
                var relatedEvents = await _context.ActionEvents
                    .Where(e => e.RelatedReminderId == reminder.Id)
                    .ToListAsync(cancellationToken);

                // If no related events, check the source event that created this reminder
                if (!relatedEvents.Any() && reminder.SourceEventId.HasValue)
                {
                    var sourceEvent = await _eventRepository.GetByIdAsync(reminder.SourceEventId.Value, cancellationToken);
                    if (sourceEvent != null)
                    {
                        relatedEvents = new List<ActionEvent> { sourceEvent };
                    }
                }

                // If still no events found, allow matching based on time and action type alone
                // This handles the case where a reminder was just created and hasn't been linked to events yet
                if (!relatedEvents.Any())
                {
                    // Time and action type already matched, so allow this match
                    // Context matching will be skipped for newly created reminders
                }
                else
                {
                    // Check if any related event has matching context
                    var hasMatchingContext = relatedEvents.Any(evt =>
                    {
                        var contextMatches = true;

                        if (criteria.MatchByDayType)
                        {
                            contextMatches = contextMatches && evt.Context.DayType == actionEvent.Context.DayType;
                        }

                        if (criteria.MatchByTimeBucket)
                        {
                            contextMatches = contextMatches && evt.Context.TimeBucket == actionEvent.Context.TimeBucket;
                        }

                        if (criteria.MatchByLocation)
                        {
                            contextMatches = contextMatches && evt.Context.Location == actionEvent.Context.Location;
                        }

                        if (criteria.MatchByPeoplePresent)
                        {
                            contextMatches = contextMatches && 
                                evt.Context.PresentPeople != null && 
                                actionEvent.Context.PresentPeople != null &&
                                evt.Context.PresentPeople.Count == actionEvent.Context.PresentPeople.Count &&
                                evt.Context.PresentPeople.OrderBy(p => p).SequenceEqual(actionEvent.Context.PresentPeople.OrderBy(p => p));
                        }

                        // State signals are already checked above for all reminders, skip here

                        return contextMatches;
                    });

                    if (!hasMatchingContext)
                    {
                        continue; // Context doesn't match, skip this reminder
                    }
                }
            }
            // For evidence-based reminders (with TimeWindowCenter), we skip context matching
            // to allow gradual pattern learning across different days/contexts

            // 5. Signal similarity check (if signal selection is enabled)
            var isSignalSelectionEnabled = await _signalPolicyService.IsSignalSelectionEnabledAsync(cancellationToken);
            if (isSignalSelectionEnabled && signalStates != null && signalStates.Count > 0)
            {
                var reminderBaseline = reminder.GetSignalProfile();
                
                if (reminderBaseline != null && reminderBaseline.Signals != null && reminderBaseline.Signals.Count > 0)
                {
                    // Reminder has a baseline - check similarity
                    var selectionLimit = await _signalPolicyService.GetSignalSelectionLimitAsync(cancellationToken);
                    var eventProfile = _signalSelector.SelectAndNormalizeSignals(signalStates, selectionLimit);
                    var similarity = _similarityEvaluator.CalculateSimilarity(reminderBaseline, eventProfile);
                    var threshold = await _signalPolicyService.GetSignalSimilarityThresholdAsync(cancellationToken);
                    
                    if (similarity < threshold)
                    {
                        // Signal mismatch - skip this reminder
                        _logger.LogInformation(
                            "Reminder {ReminderId} skipped due to signal mismatch: similarity {Similarity} < threshold {Threshold}",
                            reminder.Id, similarity, threshold);
                        continue;
                    }
                    
                    // Similarity >= threshold - proceed with matching
                    _logger.LogDebug(
                        "Reminder {ReminderId} passed signal similarity check: similarity {Similarity} >= threshold {Threshold}",
                        reminder.Id, similarity, threshold);
                }
                // If reminder has no baseline yet, allow normal behavior (will be created on first match)
            }

            // All criteria matched
            matchingReminders.Add(reminder);
        }

        // Sort by confidence (highest first), then by check time (earliest first)
        var sortedReminders = matchingReminders
            .OrderByDescending(r => r.Confidence)
            .ThenBy(r => r.CheckAtUtc)
            .ToList();

        return new ReminderCandidateListResponse
        {
            Items = _mapper.Map<List<ReminderCandidateDto>>(sortedReminders),
            TotalCount = sortedReminders.Count,
            Page = 1,
            PageSize = sortedReminders.Count
        };
    }
}

