// MediatR query for getting API keys
namespace AIPatterner.Application.Queries;

using AIPatterner.Application.DTOs;
using MediatR;

public class GetApiKeysQuery : IRequest<List<ApiKeyDto>>
{
    public Guid? UserId { get; set; }
}

