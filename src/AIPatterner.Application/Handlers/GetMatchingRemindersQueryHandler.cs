// MediatR handler for finding matching reminders for an event
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.Queries;
using AIPatterner.Application.Services;
using MediatR;

public class GetMatchingRemindersQueryHandler : IRequestHandler<GetMatchingRemindersQuery, DTOs.ReminderCandidateListResponse>
{
    private readonly IMatchingRemindersService _matchingService;

    public GetMatchingRemindersQueryHandler(IMatchingRemindersService matchingService)
    {
        _matchingService = matchingService;
    }

    public async Task<DTOs.ReminderCandidateListResponse> Handle(GetMatchingRemindersQuery request, CancellationToken cancellationToken)
    {
        return await _matchingService.FindMatchingRemindersAsync(
            request.EventId,
            request.Criteria,
            cancellationToken);
    }
}

