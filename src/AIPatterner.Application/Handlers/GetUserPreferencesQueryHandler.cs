// MediatR handler for getting user reminder preferences
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.DTOs;
using AIPatterner.Application.Queries;
using AutoMapper;
using MediatR;

public class GetUserPreferencesQueryHandler : IRequestHandler<GetUserPreferencesQuery, UserReminderPreferencesDto?>
{
    private readonly IUserPreferencesRepository _repository;
    private readonly IMapper _mapper;

    public GetUserPreferencesQueryHandler(IUserPreferencesRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<UserReminderPreferencesDto?> Handle(GetUserPreferencesQuery request, CancellationToken cancellationToken)
    {
        var preferences = await _repository.GetByPersonIdAsync(request.PersonId, cancellationToken);

        if (preferences == null)
        {
            return null;
        }

        return _mapper.Map<UserReminderPreferencesDto>(preferences);
    }
}

// Interface for user preferences repository (to be implemented in Infrastructure)
public interface IUserPreferencesRepository
{
    Task<Domain.Entities.UserReminderPreferences?> GetByPersonIdAsync(string personId, CancellationToken cancellationToken);
    Task AddAsync(Domain.Entities.UserReminderPreferences preferences, CancellationToken cancellationToken);
    Task UpdateAsync(Domain.Entities.UserReminderPreferences preferences, CancellationToken cancellationToken);
}

