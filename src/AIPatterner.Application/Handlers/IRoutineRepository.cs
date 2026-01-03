// Interface for routine repository
namespace AIPatterner.Application.Handlers;

using AIPatterner.Domain.Entities;

public interface IRoutineRepository
{
    Task<Routine?> GetByPersonAndIntentAsync(string personId, string intentType, CancellationToken cancellationToken);
    Task<Routine?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<List<Routine>> GetByPersonAsync(string personId, CancellationToken cancellationToken);
    Task<List<Routine>> GetFilteredAsync(string? personId, int page, int pageSize, CancellationToken cancellationToken);
    Task<int> GetCountAsync(string? personId, CancellationToken cancellationToken);
    Task<List<Routine>> GetActiveRoutinesAsync(string personId, DateTime currentTime, CancellationToken cancellationToken);
    Task AddAsync(Routine routine, CancellationToken cancellationToken);
    Task UpdateAsync(Routine routine, CancellationToken cancellationToken);
    Task UpdateMultipleAsync(List<Routine> routines, CancellationToken cancellationToken);
}

