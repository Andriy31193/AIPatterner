// Domain service interface for incremental learning of action transitions
namespace AIPatterner.Domain.Services;

using AIPatterner.Domain.Entities;

public interface ITransitionLearner
{
    Task UpdateTransitionsAsync(ActionEvent actionEvent, CancellationToken cancellationToken = default);
}

