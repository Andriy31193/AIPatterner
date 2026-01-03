// Interface for routine reminder repository
namespace AIPatterner.Application.Handlers;

using AIPatterner.Domain.Entities;

public interface IRoutineReminderRepository
{
    Task<RoutineReminder?> GetByRoutineAndActionAsync(Guid routineId, string actionType, CancellationToken cancellationToken);
    Task<List<RoutineReminder>> GetByRoutineAsync(Guid routineId, CancellationToken cancellationToken);
    Task<RoutineReminder?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(RoutineReminder reminder, CancellationToken cancellationToken);
    Task UpdateAsync(RoutineReminder reminder, CancellationToken cancellationToken);
}

