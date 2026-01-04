// Helper methods for creating events in LifeDataBuilder (reusable event creation logic)
namespace AIPatterner.Tests.Integration;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Application.Handlers;
using AIPatterner.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public static class LifeDataBuilderHelper
{
    public static async Task CreateArrivalHomeEventAsync(
        IngestEventCommandHandler eventHandler,
        string personId,
        DateTime timestamp,
        Random random,
        bool isWinter = false)
    {
        var signalStates = new List<SignalStateDto>
        {
            new SignalStateDto { SensorId = "front_door_sensor", Value = JsonSerializer.SerializeToElement("open") },
            new SignalStateDto { SensorId = $"{personId}_phone_wifi", Value = JsonSerializer.SerializeToElement("connected") },
            new SignalStateDto { SensorId = "hallway_motion", Value = JsonSerializer.SerializeToElement("active") }
        };
        
        if (isWinter)
        {
            signalStates.Add(new SignalStateDto { SensorId = "living_room_lights", Value = JsonSerializer.SerializeToElement("on") });
        }
        
        var eventDto = new ActionEventDto
        {
            PersonId = personId,
            ActionType = "I'm home",
            TimestampUtc = timestamp,
            EventType = EventType.StateChange,
            Context = new ActionContextDto
            {
                TimeBucket = GetTimeBucket(timestamp),
                DayType = GetDayType(timestamp),
                Location = "home",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>()
            },
            SignalStates = signalStates
        };
        
        await eventHandler.Handle(new IngestEventCommand { Event = eventDto }, CancellationToken.None);
    }
    
    public static async Task CreateSitOnCouchEventAsync(
        IngestEventCommandHandler eventHandler,
        string personId,
        DateTime timestamp,
        Random random)
    {
        var signalStates = new List<SignalStateDto>
        {
            new SignalStateDto { SensorId = "couch_pressure_sensor", Value = JsonSerializer.SerializeToElement("occupied") },
            new SignalStateDto { SensorId = "living_room_motion", Value = JsonSerializer.SerializeToElement("active") },
            new SignalStateDto { SensorId = $"{personId}_presence", Value = JsonSerializer.SerializeToElement("living_room") }
        };
        
        var eventDto = new ActionEventDto
        {
            PersonId = personId,
            ActionType = "sit_on_couch",
            TimestampUtc = timestamp,
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = GetTimeBucket(timestamp),
                DayType = GetDayType(timestamp),
                Location = "living_room",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>
                {
                    { "couch_pressure_sensor", "occupied" },
                    { "living_room_motion", "active" }
                }
            },
            SignalStates = signalStates
        };
        
        await eventHandler.Handle(new IngestEventCommand { Event = eventDto }, CancellationToken.None);
    }
    
    public static async Task CreatePlayMusicEventAsync(
        IngestEventCommandHandler eventHandler,
        string personId,
        DateTime timestamp,
        Random random)
    {
        var volume = 30 + random.Next(0, 40);
        var signalStates = new List<SignalStateDto>
        {
            new SignalStateDto { SensorId = "music_system", Value = JsonSerializer.SerializeToElement("on") },
            new SignalStateDto { SensorId = "music_volume", Value = JsonSerializer.SerializeToElement(volume) }
        };
        
        var eventDto = new ActionEventDto
        {
            PersonId = personId,
            ActionType = "play_music",
            TimestampUtc = timestamp,
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = GetTimeBucket(timestamp),
                DayType = GetDayType(timestamp),
                Location = "living_room",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>
                {
                    { "music_system", "on" },
                    { "music_volume", volume.ToString() }
                }
            },
            SignalStates = signalStates
        };
        
        await eventHandler.Handle(new IngestEventCommand { Event = eventDto }, CancellationToken.None);
    }
    
    public static async Task CreateBoilKettleEventAsync(
        IngestEventCommandHandler eventHandler,
        string personId,
        DateTime timestamp,
        Random random)
    {
        var signalStates = new List<SignalStateDto>
        {
            new SignalStateDto { SensorId = "kettle", Value = JsonSerializer.SerializeToElement("heating") },
            new SignalStateDto { SensorId = "kitchen_motion", Value = JsonSerializer.SerializeToElement("active") }
        };
        
        var eventDto = new ActionEventDto
        {
            PersonId = personId,
            ActionType = "boil_kettle",
            TimestampUtc = timestamp,
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = GetTimeBucket(timestamp),
                DayType = GetDayType(timestamp),
                Location = "kitchen",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>
                {
                    { "kettle", "heating" },
                    { "kitchen_motion", "active" }
                }
            },
            SignalStates = signalStates
        };
        
        await eventHandler.Handle(new IngestEventCommand { Event = eventDto }, CancellationToken.None);
    }
    
    public static async Task CreateWakeUpEventAsync(
        IngestEventCommandHandler eventHandler,
        string personId,
        DateTime timestamp,
        Random random)
    {
        var signalStates = new List<SignalStateDto>
        {
            new SignalStateDto { SensorId = "bed_pressure_sensor", Value = JsonSerializer.SerializeToElement("unoccupied") },
            new SignalStateDto { SensorId = "bedroom_motion", Value = JsonSerializer.SerializeToElement("active") },
            new SignalStateDto { SensorId = $"{personId}_phone_screen", Value = JsonSerializer.SerializeToElement("on") }
        };
        
        var eventDto = new ActionEventDto
        {
            PersonId = personId,
            ActionType = "WakeUp",
            TimestampUtc = timestamp,
            EventType = EventType.StateChange,
            Context = new ActionContextDto
            {
                TimeBucket = "morning",
                DayType = GetDayType(timestamp),
                Location = "bedroom",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>
                {
                    { "bed_pressure_sensor", "unoccupied" },
                    { "bedroom_motion", "active" }
                }
            },
            SignalStates = signalStates
        };
        
        await eventHandler.Handle(new IngestEventCommand { Event = eventDto }, CancellationToken.None);
    }
    
    public static async Task CreateKitchenRoutineEventsAsync(
        IngestEventCommandHandler eventHandler,
        string personId,
        DateTime timestamp,
        Random random)
    {
        // Kitchen lights
        var lightsSignalStates = new List<SignalStateDto>
        {
            new SignalStateDto { SensorId = "kitchen_lights", Value = JsonSerializer.SerializeToElement("on") },
            new SignalStateDto { SensorId = "kitchen_motion", Value = JsonSerializer.SerializeToElement("active") }
        };
        
        var lightsEvent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = "kitchen_lights_on",
            TimestampUtc = timestamp,
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = "morning",
                DayType = GetDayType(timestamp),
                Location = "kitchen",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>
                {
                    { "kitchen_lights", "on" },
                    { "kitchen_motion", "active" }
                }
            },
            SignalStates = lightsSignalStates
        };
        await eventHandler.Handle(new IngestEventCommand { Event = lightsEvent }, CancellationToken.None);
        
        // Coffee machine
        var coffeeTime = timestamp.AddMinutes(1 + random.NextDouble() * 2);
        var coffeeSignalStates = new List<SignalStateDto>
        {
            new SignalStateDto { SensorId = "coffee_machine", Value = JsonSerializer.SerializeToElement("brewing") }
        };
        
        var coffeeEvent = new ActionEventDto
        {
            PersonId = personId,
            ActionType = "coffee_machine_on",
            TimestampUtc = coffeeTime,
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = "morning",
                DayType = GetDayType(timestamp),
                Location = "kitchen",
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>
                {
                    { "coffee_machine", "brewing" },
                    { "kitchen_motion", "active" }
                }
            },
            SignalStates = coffeeSignalStates
        };
        await eventHandler.Handle(new IngestEventCommand { Event = coffeeEvent }, CancellationToken.None);
    }
    
    public static async Task CreateRandomDailyEventAsync(
        IngestEventCommandHandler eventHandler,
        string personId,
        DateTime timestamp,
        Random random,
        string[] actions)
    {
        var action = actions[random.Next(actions.Length)];
        var location = GetLocationForAction(action);
        
        var signalStates = new List<SignalStateDto>
        {
            new SignalStateDto { SensorId = $"{personId}_presence", Value = JsonSerializer.SerializeToElement(location) }
        };
        
        var eventDto = new ActionEventDto
        {
            PersonId = personId,
            ActionType = action,
            TimestampUtc = timestamp,
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = GetTimeBucket(timestamp),
                DayType = GetDayType(timestamp),
                Location = location,
                PresentPeople = new List<string> { personId },
                StateSignals = new Dictionary<string, string>()
            },
            SignalStates = signalStates
        };
        
        await eventHandler.Handle(new IngestEventCommand { Event = eventDto }, CancellationToken.None);
    }
    
    private static string GetTimeBucket(DateTime timestamp)
    {
        var hour = timestamp.Hour;
        if (hour >= 5 && hour < 12) return "morning";
        if (hour >= 12 && hour < 17) return "afternoon";
        if (hour >= 17 && hour < 22) return "evening";
        return "night";
    }
    
    private static string GetDayType(DateTime timestamp)
    {
        return timestamp.DayOfWeek == DayOfWeek.Saturday || timestamp.DayOfWeek == DayOfWeek.Sunday
            ? "weekend"
            : "weekday";
    }
    
    private static string GetLocationForAction(string action)
    {
        return action switch
        {
            "sit_on_couch" or "tv_on" or "play_music" => "living_room",
            "kitchen_lights_on" or "coffee_machine_on" or "boil_kettle" or "cook_on_stove" or "use_microwave" or "open_fridge" => "kitchen",
            "WakeUp" or "wake_up" => "bedroom",
            _ => "home"
        };
    }
}

