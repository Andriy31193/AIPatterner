// Domain service interface for evaluating reminder policies based on transitions and context
namespace AIPatterner.Domain.Services;

using AIPatterner.Domain.Entities;

public interface IReminderPolicyEvaluator
{
    Task<ReminderPolicyDecision?> EvaluateAsync(
        ActionTransition transition,
        ActionContext currentContext,
        UserReminderPreferences preferences,
        CancellationToken cancellationToken = default);
}

public class ReminderPolicyDecision
{
    public bool ShouldSchedule { get; set; }
    public ReminderStyle Style { get; set; }
    public DateTime SuggestedCheckAt { get; set; }
    public string Reason { get; set; }
}

