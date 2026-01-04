// Interface for evaluating execution action (Suggest/Ask/Execute) for reminders
namespace AIPatterner.Application.Services;

using AIPatterner.Domain.Entities;

/// <summary>
/// Evaluates the appropriate execution action (Suggest/Ask/Execute) for a reminder
/// based on confidence, safety, and user preferences.
/// </summary>
public interface IExecutionActionEvaluator
{
    /// <summary>
    /// Evaluates the execution action for a reminder candidate.
    /// </summary>
    /// <param name="confidence">The confidence level (0.0 to 1.0)</param>
    /// <param name="isSafeToAutoExecute">Whether the reminder is marked as safe to auto-execute</param>
    /// <param name="userAllowsAutoExecute">Whether the user has opted in for auto-execution</param>
    /// <param name="executeAutoThreshold">The confidence threshold for auto-execution (default 0.95)</param>
    /// <returns>The recommended execution action</returns>
    ExecutionAction Evaluate(
        double confidence,
        bool isSafeToAutoExecute,
        bool userAllowsAutoExecute,
        double executeAutoThreshold = 0.95);
}

/// <summary>
/// Represents the execution action for a reminder.
/// </summary>
public enum ExecutionAction
{
    /// <summary>
    /// Suggest the action to the user (low confidence)
    /// </summary>
    Suggest,
    
    /// <summary>
    /// Ask the user before executing (medium confidence)
    /// </summary>
    Ask,
    
    /// <summary>
    /// Execute automatically (high confidence + safety + opt-in)
    /// </summary>
    Execute
}

