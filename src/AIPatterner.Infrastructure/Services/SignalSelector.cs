// Service implementation for signal selection and normalization
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Domain.Services;
using AIPatterner.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for selecting and normalizing sensor signals.
/// </summary>
public class SignalSelector : ISignalSelector
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SignalSelector> _logger;
    
    // Default importance mapping for common sensor types
    private static readonly Dictionary<string, double> DefaultImportanceMap = new()
    {
        { "presence", 1.0 },
        { "light", 0.3 },
        { "audio", 0.6 },
        { "temp", 0.2 },
        { "humidity", 0.1 },
        { "motion", 0.8 },
        { "door", 0.7 },
        { "window", 0.5 }
    };

    public SignalSelector(IConfiguration configuration, ILogger<SignalSelector> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public SignalProfile SelectAndNormalizeSignals(List<SignalState> signalStates, int topK)
    {
        if (signalStates == null || signalStates.Count == 0)
        {
            return new SignalProfile { Signals = new Dictionary<string, SignalProfileEntry>() };
        }

        // Step 1: Normalize values and compute importance for each signal
        var normalizedSignals = new List<(string SensorId, double NormalizedValue, double Importance)>();
        
        foreach (var signal in signalStates)
        {
            if (string.IsNullOrWhiteSpace(signal.SensorId))
            {
                continue;
            }

            var normalizedValue = NormalizeValue(signal.SensorId, signal.Value);
            var defaultImportance = GetDefaultImportance(signal.SensorId);
            var rawImportance = signal.RawImportance ?? 1.0;
            
            // Final importance = normalized(rawImportance) * defaultImportance
            // Normalize rawImportance to [0, 1] range (assuming it's in [0, 1] or positive)
            var normalizedRawImportance = Math.Max(0.0, Math.Min(1.0, rawImportance));
            var finalImportance = normalizedRawImportance * defaultImportance;
            
            normalizedSignals.Add((signal.SensorId, normalizedValue, finalImportance));
        }

        // Step 2: Select top-K by importance
        var topSignals = normalizedSignals
            .OrderByDescending(s => s.Importance)
            .Take(topK)
            .ToList();

        if (topSignals.Count == 0)
        {
            return new SignalProfile { Signals = new Dictionary<string, SignalProfileEntry>() };
        }

        // Step 3: Normalize weights so sum(weights) = 1 (L2 normalization for cosine similarity)
        var totalWeight = topSignals.Sum(s => s.Importance);
        var l2Norm = Math.Sqrt(topSignals.Sum(s => s.Importance * s.Importance));
        
        var profile = new SignalProfile { Signals = new Dictionary<string, SignalProfileEntry>() };
        
        foreach (var signal in topSignals)
        {
            // Use L2-normalized weight for cosine similarity
            var weight = l2Norm > 0 ? signal.Importance / l2Norm : 0.0;
            
            profile.Signals[signal.SensorId] = new SignalProfileEntry
            {
                Weight = weight,
                NormalizedValue = signal.NormalizedValue
            };
        }

        return profile;
    }

    /// <summary>
    /// Normalizes a sensor value to a numeric feature in range [-1, 1] or [0, 1].
    /// </summary>
    private double NormalizeValue(string sensorId, object value)
    {
        if (value == null)
        {
            return 0.0;
        }

        // Boolean -> {false:0, true:1}
        if (value is bool boolValue)
        {
            return boolValue ? 1.0 : 0.0;
        }

        // Numeric -> scale to [0, 1] or [-1, 1] based on sensor type
        if (value is double doubleValue)
        {
            return NormalizeNumericValue(sensorId, doubleValue);
        }
        
        if (value is int intValue)
        {
            return NormalizeNumericValue(sensorId, (double)intValue);
        }
        
        if (value is float floatValue)
        {
            return NormalizeNumericValue(sensorId, (double)floatValue);
        }

        // String -> enum-like mapping
        if (value is string stringValue)
        {
            return NormalizeStringValue(sensorId, stringValue);
        }

        // Try to parse as number
        if (double.TryParse(value.ToString(), out var parsedValue))
        {
            return NormalizeNumericValue(sensorId, parsedValue);
        }

        // Unknown type: default to 0.5
        _logger.LogWarning("Unknown value type for sensor {SensorId}: {ValueType}", sensorId, value.GetType());
        return 0.5;
    }

    /// <summary>
    /// Normalizes a numeric value based on sensor type.
    /// </summary>
    private double NormalizeNumericValue(string sensorId, double value)
    {
        // Get configured min/max for this sensor type, or use defaults
        var sensorType = GetSensorType(sensorId);
        
        // Try to get configured range from policies
        var minKey = $"Policies:SignalValueNormalizers:{sensorType}:Min";
        var maxKey = $"Policies:SignalValueNormalizers:{sensorType}:Max";
        var min = _configuration.GetValue<double?>(minKey);
        var max = _configuration.GetValue<double?>(maxKey);

        // Default ranges for common sensor types
        if (!min.HasValue || !max.HasValue)
        {
            switch (sensorType.ToLower())
            {
                case "temp":
                case "temperature":
                    min = 0.0;
                    max = 100.0; // Celsius
                    break;
                case "humidity":
                    min = 0.0;
                    max = 100.0; // Percentage
                    break;
                case "light":
                case "brightness":
                    min = 0.0;
                    max = 1000.0; // Lux
                    break;
                default:
                    // Use z-score normalization if no range specified
                    // For now, assume [0, 100] as default
                    min = 0.0;
                    max = 100.0;
                    break;
            }
        }

        // Scale to [0, 1]
        if (max.Value > min.Value)
        {
            var normalized = (value - min.Value) / (max.Value - min.Value);
            // Clip to [0, 1]
            return Math.Max(0.0, Math.Min(1.0, normalized));
        }

        return 0.5;
    }

    /// <summary>
    /// Normalizes a string value (enum-like) to numeric.
    /// </summary>
    private double NormalizeStringValue(string sensorId, string value)
    {
        var sensorType = GetSensorType(sensorId);
        
        // Common enum mappings
        switch (sensorType.ToLower())
        {
            case "presence":
            case "occupancy":
                return value.ToLower() switch
                {
                    "home" or "present" or "on" or "true" => 1.0,
                    "away" or "absent" or "off" or "false" => 0.0,
                    _ => 0.5
                };
            
            case "door":
            case "window":
                return value.ToLower() switch
                {
                    "open" or "opened" => 1.0,
                    "closed" or "shut" => 0.0,
                    _ => 0.5
                };
            
            case "music":
            case "audio":
                return value.ToLower() switch
                {
                    "playing" or "on" => 1.0,
                    "stopped" or "off" or "paused" => 0.0,
                    _ => 0.5
                };
            
            default:
                // Try to get configured mapping from policies
                var mappingKey = $"Policies:SignalValueNormalizers:{sensorType}:Mappings:{value}";
                var mappedValue = _configuration.GetValue<double?>(mappingKey);
                if (mappedValue.HasValue)
                {
                    return mappedValue.Value;
                }
                
                // Unknown enum: default to 0.5
                return 0.5;
        }
    }

    /// <summary>
    /// Gets the sensor type from sensor ID (e.g., "sensor.presence.kitchen" -> "presence").
    /// </summary>
    private string GetSensorType(string sensorId)
    {
        if (string.IsNullOrWhiteSpace(sensorId))
        {
            return "unknown";
        }

        // Try to extract type from ID (e.g., "sensor.presence.kitchen" -> "presence")
        var parts = sensorId.Split('.');
        if (parts.Length >= 2)
        {
            return parts[1].ToLower();
        }

        // If no dots, use the whole ID
        return sensorId.ToLower();
    }

    /// <summary>
    /// Gets the default importance for a sensor type.
    /// </summary>
    private double GetDefaultImportance(string sensorId)
    {
        var sensorType = GetSensorType(sensorId);
        
        // Try to get from configuration first
        var configKey = $"Policies:SignalImportanceDefault:{sensorType}";
        var configuredImportance = _configuration.GetValue<double?>(configKey);
        if (configuredImportance.HasValue)
        {
            return configuredImportance.Value;
        }

        // Use default mapping
        return DefaultImportanceMap.GetValueOrDefault(sensorType, 0.5);
    }
}

