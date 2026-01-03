// Interface for routine learning service
namespace AIPatterner.Application.Services;

using AIPatterner.Domain.Entities;

public interface IRoutineLearningService
{
    Task<Routine> HandleIntentAsync(ActionEvent intentEvent, CancellationToken cancellationToken);
    Task ProcessObservedEventAsync(ActionEvent observedEvent, CancellationToken cancellationToken);
    Task HandleFeedbackAsync(Guid routineReminderId, ProbabilityAction action, double value, CancellationToken cancellationToken);
    Task<List<RoutineReminder>> GetRemindersForIntentAsync(string personId, string intentType, CancellationToken cancellationToken);
}

