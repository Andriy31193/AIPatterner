// Service implementation for evaluating execution actions
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Application.Services;

/// <summary>
/// Evaluates the appropriate execution action (Suggest/Ask/Execute) for reminders.
/// </summary>
public class ExecutionActionEvaluator : IExecutionActionEvaluator
{
    public ExecutionAction Evaluate(
        double confidence,
        bool isSafeToAutoExecute,
        bool userAllowsAutoExecute,
        double executeAutoThreshold = 0.95)
    {
        // Auto-detection algorithm:
        // confidence < 0.50 → Suggest
        // 0.50 ≤ confidence < executeAutoThreshold → Ask
        // confidence >= executeAutoThreshold AND isSafeToAutoExecute AND userAllowsAutoExecute → Execute
        // Otherwise → Ask (even if confidence is high, if safety or opt-in is missing)

        if (confidence < 0.50)
        {
            return ExecutionAction.Suggest;
        }

        if (confidence >= executeAutoThreshold && isSafeToAutoExecute && userAllowsAutoExecute)
        {
            return ExecutionAction.Execute;
        }

        // Default to Ask for medium-high confidence
        return ExecutionAction.Ask;
    }
}

