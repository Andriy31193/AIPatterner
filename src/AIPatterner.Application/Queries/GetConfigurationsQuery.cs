// MediatR query for getting configurations
namespace AIPatterner.Application.Queries;

using AIPatterner.Application.DTOs;
using MediatR;

public class GetConfigurationsQuery : IRequest<List<ConfigurationDto>>
{
    public string? Category { get; set; }
}


