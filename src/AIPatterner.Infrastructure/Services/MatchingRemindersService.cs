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

            // 2. STRICT: Match by time offset - reminder's CheckAtUtc must be within offset of event timestamp
            var timeDiff = Math.Abs((reminder.CheckAtUtc - eventTime).TotalMinutes);
            matches = matches && timeDiff <= criteria.TimeOffsetMinutes;
            if (!matches) continue;

            // 3. STRICT: For context-based matching, find the event(s) that created/updated this reminder
            // and check if any of those events have matching context
            if (criteria.MatchByDayType || criteria.MatchByPeoplePresent || criteria.MatchByStateSignals || 
                criteria.MatchByTimeBucket || criteria.MatchByLocation)
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
                // This fixes the issue where reminders with small time offsets weren't being matched
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

