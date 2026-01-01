// Query for getting reminders by source event ID
namespace AIPatterner.Application.Queries;

using AIPatterner.Application.DTOs;
using MediatR;
using System;

public class GetRemindersByEventIdQuery : IRequest<ReminderCandidateListResponse>
{
    public Guid EventId { get; set; }
}

