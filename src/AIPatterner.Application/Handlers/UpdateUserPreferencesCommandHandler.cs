// MediatR handler for updating user reminder preferences
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.Commands;
using AIPatterner.Domain.Entities;
using MediatR;

public class UpdateUserPreferencesCommandHandler : IRequestHandler<UpdateUserPreferencesCommand, bool>
{
    private readonly IUserPreferencesRepository _repository;

    public UpdateUserPreferencesCommandHandler(IUserPreferencesRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(UpdateUserPreferencesCommand request, CancellationToken cancellationToken)
    {
        var preferences = await _repository.GetByPersonIdAsync(request.PersonId, cancellationToken);

        if (preferences == null)
        {
            preferences = new UserReminderPreferences(
                request.PersonId,
                request.DefaultStyle ?? ReminderStyle.Ask,
                request.DailyLimit ?? 10,
                request.MinimumInterval);
            await _repository.AddAsync(preferences, cancellationToken);
        }
        else
        {
            preferences.Update(
                request.DefaultStyle,
                request.DailyLimit,
                request.MinimumInterval,
                request.Enabled);
            await _repository.UpdateAsync(preferences, cancellationToken);
        }

        return true;
    }
}

