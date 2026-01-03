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

public class MatchingRemindersService : IMatchingRemindersService
{
    private readonly IEventRepository _eventRepository;
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public MatchingRemindersService(
        IEventRepository eventRepository,
        ApplicationDbContext context,
        IMapper mapper)
    {
        _eventRepository = eventRepository;
        _context = context;
        _mapper = mapper;
    }

    public async Task<ReminderCandidateListResponse> FindMatchingRemindersAsync(
        Guid eventId,
        MatchingCriteria criteria,
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

            // 2. NEW BEHAVIOR: Match by time-of-day window, ignoring day/dayOfWeek
            // This allows the same reminder to match events across different days,
            // which is how humans form habits (time-of-day first, day-of-week later)
            if (reminder.TimeWindowCenter.HasValue)
            {
                // Use the reminder's time window center and size
                var eventTimeOfDay = eventTime.TimeOfDay;
                var reminderTimeCenter = reminder.TimeWindowCenter.Value;
                var windowSize = reminder.TimeWindowSizeMinutes;
                
                // Calculate time difference, handling midnight wraparound
                var timeDiff = Math.Abs((eventTimeOfDay - reminderTimeCenter).TotalMinutes);
                if (timeDiff > 12 * 60) // More than 12 hours apart, check wraparound
                {
                    timeDiff = 24 * 60 - timeDiff;
                }
                
                matches = matches && timeDiff <= windowSize;
                if (!matches) continue;
            }
            else
            {
                // Fallback for old reminders without TimeWindowCenter: use CheckAtUtc matching
                // This maintains backward compatibility
                var timeDiff = Math.Abs((reminder.CheckAtUtc - eventTime).TotalMinutes);
                matches = matches && timeDiff <= criteria.TimeOffsetMinutes;
                if (!matches) continue;
            }

            // 3. Context-based matching
            // NEW BEHAVIOR: For evidence-based reminders (with TimeWindowCenter), 
            // we skip strict context matching to allow cross-day matching.
            // This enables gradual pattern learning where time-of-day dominates initially.
            // For old-style reminders (without TimeWindowCenter), we still use strict context matching.
            if (!reminder.TimeWindowCenter.HasValue && 
                (criteria.MatchByDayType || criteria.MatchByPeoplePresent || criteria.MatchByStateSignals || 
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

                        if (criteria.MatchByStateSignals)
                        {
                            contextMatches = contextMatches && 
                                evt.Context.StateSignals != null && 
                                actionEvent.Context.StateSignals != null &&
                                evt.Context.StateSignals.Count == actionEvent.Context.StateSignals.Count &&
                                evt.Context.StateSignals.Keys.All(k => 
                                    actionEvent.Context.StateSignals.ContainsKey(k) && 
                                    actionEvent.Context.StateSignals[k] == evt.Context.StateSignals[k]) &&
                                actionEvent.Context.StateSignals.Keys.All(k => 
                                    evt.Context.StateSignals.ContainsKey(k) && 
                                    evt.Context.StateSignals[k] == actionEvent.Context.StateSignals[k]);
                        }

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

