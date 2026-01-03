// MediatR query for retrieving routines
namespace AIPatterner.Application.Queries;

using AIPatterner.Application.DTOs;
using MediatR;

public class GetRoutinesQuery : IRequest<RoutineListResponse>
{
    public string? PersonId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

