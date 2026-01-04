// Value object representing a sensor signal state
namespace AIPatterner.Domain.ValueObjects;

/// <summary>
/// Represents a single sensor signal state from an event.
/// </summary>
public class SignalState
{
    public string SensorId { get; set; } = string.Empty;
    public object Value { get; set; } = null!; // Can be string, number, or boolean
    public double? RawImportance { get; set; } // Optional importance hint from AI/HA
}

/// <summary>
/// Represents a normalized signal entry in a signal profile vector.
/// </summary>
public class SignalProfileEntry
{
    public double Weight { get; set; }
    public double NormalizedValue { get; set; }
}

/// <summary>
/// Represents a signal profile (baseline vector) for a reminder.
/// Maps sensorId -> SignalProfileEntry
/// </summary>
public class SignalProfile
{
    public Dictionary<string, SignalProfileEntry> Signals { get; set; } = new();
}

