// Handler for getting reminders by source event ID
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.DTOs;
using AIPatterner.Application.Mappings;
using AIPatterner.Application.Queries;
using AIPatterner.Domain.Entities;
using AutoMapper;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

public class GetRemindersByEventIdQueryHandler : IRequestHandler<GetRemindersByEventIdQuery, ReminderCandidateListResponse>
{
    private readonly IReminderCandidateRepository _repository;
    private readonly IMapper _mapper;

    public GetRemindersByEventIdQueryHandler(
        IReminderCandidateRepository repository,
        IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<ReminderCandidateListResponse> Handle(GetRemindersByEventIdQuery request, CancellationToken cancellationToken)
    {
        var reminders = await _repository.GetBySourceEventIdAsync(request.EventId, cancellationToken);
        
        return new ReminderCandidateListResponse
        {
            Items = _mapper.Map<List<ReminderCandidateDto>>(reminders),
            TotalCount = reminders.Count,
            Page = 1,
            PageSize = reminders.Count
        };
    }
}

