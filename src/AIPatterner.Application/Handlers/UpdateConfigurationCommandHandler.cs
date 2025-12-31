// MediatR handler for updating configurations
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using MediatR;

public class UpdateConfigurationCommandHandler : IRequestHandler<UpdateConfigurationCommand, ConfigurationDto>
{
    private readonly IConfigurationRepository _repository;

    public UpdateConfigurationCommandHandler(IConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ConfigurationDto> Handle(UpdateConfigurationCommand request, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByKeyAndCategoryAsync(request.Key, request.Category, cancellationToken);

        if (config == null)
        {
            throw new InvalidOperationException($"Configuration with key '{request.Key}' and category '{request.Category}' not found");
        }

        config.UpdateValue(request.Value);
        if (request.Description != null)
        {
            config.UpdateDescription(request.Description);
        }

        await _repository.UpdateAsync(config, cancellationToken);

        return new ConfigurationDto
        {
            Id = config.Id,
            Key = config.Key,
            Value = config.Value,
            Category = config.Category,
            Description = config.Description,
            CreatedAtUtc = config.CreatedAtUtc,
            UpdatedAtUtc = config.UpdatedAtUtc
        };
    }
}

