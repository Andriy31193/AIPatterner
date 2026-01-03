// MediatR handler for getting API keys
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.DTOs;
using AIPatterner.Application.Queries;
using MediatR;

public class GetApiKeysQueryHandler : IRequestHandler<GetApiKeysQuery, List<ApiKeyDto>>
{
    private readonly IApiKeyRepository _repository;

    public GetApiKeysQueryHandler(IApiKeyRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<ApiKeyDto>> Handle(GetApiKeysQuery request, CancellationToken cancellationToken)
    {
        var apiKeys = await _repository.GetByUserIdAsync(request.UserId, cancellationToken);

        return apiKeys.Select(k => new ApiKeyDto
        {
            Id = k.Id,
            Name = k.Name,
            KeyPrefix = k.KeyPrefix,
            Role = k.Role,
            UserId = k.UserId,
            PersonId = k.PersonId,
            ExpiresAtUtc = k.ExpiresAtUtc,
            LastUsedAtUtc = k.LastUsedAtUtc,
            CreatedAtUtc = k.CreatedAtUtc,
            IsActive = k.IsActive
        }).ToList();
    }
}

