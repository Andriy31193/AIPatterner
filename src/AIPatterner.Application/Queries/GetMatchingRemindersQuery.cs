// MediatR query for finding matching reminders for an event
namespace AIPatterner.Application.Queries;

using AIPatterner.Application.DTOs;
using MediatR;

public class GetMatchingRemindersQuery : IRequest<ReminderCandidateListResponse>
{
    public Guid EventId { get; set; }
    public MatchingCriteria Criteria { get; set; } = new MatchingCriteria();
}

public class MatchingCriteria
{
    public bool MatchByActionType { get; set; } = true;
    public bool MatchByDayType { get; set; } = true;
    public bool MatchByPeoplePresent { get; set; } = true;
    public bool MatchByStateSignals { get; set; } = true;
    public bool MatchByTimeBucket { get; set; } = false;
    public bool MatchByLocation { get; set; } = false;
    public int TimeOffsetMinutes { get; set; } = 45;
}


