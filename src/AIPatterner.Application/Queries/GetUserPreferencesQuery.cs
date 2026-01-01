// MediatR query for getting user reminder preferences
namespace AIPatterner.Application.Queries;

using AIPatterner.Application.DTOs;
using MediatR;

public class GetUserPreferencesQuery : IRequest<UserReminderPreferencesDto?>
{
    public string PersonId { get; set; } = string.Empty;
}

