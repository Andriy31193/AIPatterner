// MediatR query for retrieving transitions for a person
namespace AIPatterner.Application.Queries;

using AIPatterner.Application.DTOs;
using MediatR;

public class GetTransitionsQuery : IRequest<TransitionListResponse>
{
    public string PersonId { get; set; } = string.Empty;
}

