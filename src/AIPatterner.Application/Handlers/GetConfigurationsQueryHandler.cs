// MediatR handler for getting configurations
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.DTOs;
using AIPatterner.Application.Queries;
using MediatR;

public class GetConfigurationsQueryHandler : IRequestHandler<GetConfigurationsQuery, List<ConfigurationDto>>
{
    private readonly IConfigurationRepository _repository;

    public GetConfigurationsQueryHandler(IConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<ConfigurationDto>> Handle(GetConfigurationsQuery request, CancellationToken cancellationToken)
    {
        var configs = await _repository.GetByCategoryAsync(request.Category, cancellationToken);

        return configs.Select(c => new ConfigurationDto
        {
            Id = c.Id,
            Key = c.Key,
            Value = c.Value,
            Category = c.Category,
            Description = c.Description,
            CreatedAtUtc = c.CreatedAtUtc,
            UpdatedAtUtc = c.UpdatedAtUtc
        }).ToList();
    }
}

