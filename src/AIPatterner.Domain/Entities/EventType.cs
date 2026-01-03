// Enum for event types
namespace AIPatterner.Domain.Entities;

public enum EventType
{
    /// <summary>
    /// Regular action event (matches general reminders)
    /// </summary>
    Action,
    
    /// <summary>
    /// State change event (intent anchor for routines, does NOT match general reminders)
    /// </summary>
    StateChange
}

