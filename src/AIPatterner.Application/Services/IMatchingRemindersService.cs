// Service interface for finding matching reminders
namespace AIPatterner.Application.Services;

using AIPatterner.Application.DTOs;
using AIPatterner.Application.Queries;

public interface IMatchingRemindersService
{
    Task<ReminderCandidateListResponse> FindMatchingRemindersAsync(
        Guid eventId,
        MatchingCriteria criteria,
        CancellationToken cancellationToken);
}


