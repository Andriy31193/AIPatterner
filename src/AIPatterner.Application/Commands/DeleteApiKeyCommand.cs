// MediatR command for deleting API keys
namespace AIPatterner.Application.Commands;

using MediatR;

public class DeleteApiKeyCommand : IRequest<bool>
{
    public Guid ApiKeyId { get; set; }
}

