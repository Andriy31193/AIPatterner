// MediatR handler for querying transitions
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.DTOs;
using AIPatterner.Application.Mappings;
using AIPatterner.Application.Queries;
using AIPatterner.Domain.Entities;
using AutoMapper;
using MediatR;

public class GetTransitionsQueryHandler : IRequestHandler<GetTransitionsQuery, TransitionListResponse>
{
    private readonly ITransitionRepository _repository;
    private readonly IMapper _mapper;

    public GetTransitionsQueryHandler(ITransitionRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<TransitionListResponse> Handle(GetTransitionsQuery request, CancellationToken cancellationToken)
    {
        var transitions = await _repository.GetByPersonIdAsync(request.PersonId, cancellationToken);
        return new TransitionListResponse
        {
            Transitions = _mapper.Map<List<TransitionDto>>(transitions)
        };
    }
}

// Interface for transition repository (to be implemented in Infrastructure)
public interface ITransitionRepository
{
    Task<List<ActionTransition>> GetByPersonIdAsync(string personId, CancellationToken cancellationToken);
    Task<ActionTransition?> GetByKeyAsync(
        string personId,
        string fromAction,
        string toAction,
        string contextBucket,
        CancellationToken cancellationToken);
    Task AddAsync(ActionTransition transition, CancellationToken cancellationToken);
    Task UpdateAsync(ActionTransition transition, CancellationToken cancellationToken);
    Task<List<ActionTransition>> GetRecentTransitionsForPersonAsync(
        string personId,
        string fromAction,
        CancellationToken cancellationToken);
    Task<ActionTransition?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}


