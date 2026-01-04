// DTOs for signal states
namespace AIPatterner.Application.DTOs;

using System.Text.Json;

/// <summary>
/// DTO for a signal state in an event.
/// </summary>
public class SignalStateDto
{
    public string SensorId { get; set; } = string.Empty;
    public JsonElement Value { get; set; } // Can be string, number, or boolean
    public double? RawImportance { get; set; }
}

/// <summary>
/// DTO for signal profile entry.
/// </summary>
public class SignalProfileEntryDto
{
    public double Weight { get; set; }
    public double NormalizedValue { get; set; }
}

/// <summary>
/// DTO for signal profile (baseline).
/// </summary>
public class SignalProfileDto
{
    public Dictionary<string, SignalProfileEntryDto> Signals { get; set; } = new();
}

