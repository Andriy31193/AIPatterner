// MediatR handler for deleting API keys
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.Commands;
using MediatR;

public class DeleteApiKeyCommandHandler : IRequestHandler<DeleteApiKeyCommand, bool>
{
    private readonly IApiKeyRepository _repository;

    public DeleteApiKeyCommandHandler(IApiKeyRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(DeleteApiKeyCommand request, CancellationToken cancellationToken)
    {
        var apiKey = await _repository.GetByIdAsync(request.ApiKeyId, cancellationToken);

        if (apiKey == null)
            return false;

        await _repository.DeleteAsync(apiKey, cancellationToken);

        return true;
    }
}

