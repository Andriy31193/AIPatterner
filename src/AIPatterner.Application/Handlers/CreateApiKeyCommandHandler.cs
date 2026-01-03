// MediatR handler for creating API keys
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Domain.Entities;
using MediatR;

public class CreateApiKeyCommandHandler : IRequestHandler<CreateApiKeyCommand, CreateApiKeyResponse>
{
    private readonly IApiKeyRepository _repository;
    private readonly IApiKeyService _apiKeyService;

    public CreateApiKeyCommandHandler(IApiKeyRepository repository, IApiKeyService apiKeyService)
    {
        _repository = repository;
        _apiKeyService = apiKeyService;
    }

    public async Task<CreateApiKeyResponse> Handle(CreateApiKeyCommand request, CancellationToken cancellationToken)
    {
        // Generate API key
        var fullKey = _apiKeyService.GenerateApiKey();
        var keyHash = _apiKeyService.HashApiKey(fullKey);
        var keyPrefix = _apiKeyService.GetKeyPrefix(fullKey);

        // Create entity
        var apiKey = new ApiKey(
            request.Name,
            keyHash,
            keyPrefix,
            request.Role,
            request.UserId,
            request.PersonId,
            request.ExpiresAtUtc);

        await _repository.AddAsync(apiKey, cancellationToken);

        // Map to DTO
        var dto = new ApiKeyDto
        {
            Id = apiKey.Id,
            Name = apiKey.Name,
            KeyPrefix = apiKey.KeyPrefix,
            Role = apiKey.Role,
            UserId = apiKey.UserId,
            PersonId = apiKey.PersonId,
            ExpiresAtUtc = apiKey.ExpiresAtUtc,
            LastUsedAtUtc = apiKey.LastUsedAtUtc,
            CreatedAtUtc = apiKey.CreatedAtUtc,
            IsActive = apiKey.IsActive
        };

        return new CreateApiKeyResponse
        {
            ApiKey = dto,
            FullKey = fullKey // Only returned once
        };
    }
}

// Interface for API key repository (to be implemented in Infrastructure)
public interface IApiKeyRepository
{
    Task AddAsync(ApiKey apiKey, CancellationToken cancellationToken);
    Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<List<ApiKey>> GetByUserIdAsync(Guid? userId, CancellationToken cancellationToken);
    Task DeleteAsync(ApiKey apiKey, CancellationToken cancellationToken);
}

// Interface for API key service (to be implemented in Infrastructure)
public interface IApiKeyService
{
    string GenerateApiKey();
    string HashApiKey(string apiKey);
    bool VerifyApiKey(string apiKey, string keyHash);
    string GetKeyPrefix(string apiKey);
}

