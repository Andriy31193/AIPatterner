// MediatR handler for creating configurations
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Domain.Entities;
using MediatR;

public class CreateConfigurationCommandHandler : IRequestHandler<CreateConfigurationCommand, ConfigurationDto>
{
    private readonly IConfigurationRepository _repository;

    public CreateConfigurationCommandHandler(IConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ConfigurationDto> Handle(CreateConfigurationCommand request, CancellationToken cancellationToken)
    {
        // Check if configuration already exists
        var existing = await _repository.GetByKeyAndCategoryAsync(request.Key, request.Category, cancellationToken);

        if (existing != null)
        {
            // Update existing
            existing.UpdateValue(request.Value);
            if (request.Description != null)
            {
                existing.UpdateDescription(request.Description);
            }
            await _repository.UpdateAsync(existing, cancellationToken);
        }
        else
        {
            // Create new
            existing = new Configuration(request.Key, request.Value, request.Category, request.Description);
            await _repository.AddAsync(existing, cancellationToken);
        }

        return new ConfigurationDto
        {
            Id = existing.Id,
            Key = existing.Key,
            Value = existing.Value,
            Category = existing.Category,
            Description = existing.Description,
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = existing.UpdatedAtUtc
        };
    }
}

// Interface for configuration repository (to be implemented in Infrastructure)
public interface IConfigurationRepository
{
    Task AddAsync(Configuration configuration, CancellationToken cancellationToken);
    Task<Configuration?> GetByKeyAndCategoryAsync(string key, string category, CancellationToken cancellationToken);
    Task<List<Configuration>> GetByCategoryAsync(string? category, CancellationToken cancellationToken);
    Task UpdateAsync(Configuration configuration, CancellationToken cancellationToken);
}

