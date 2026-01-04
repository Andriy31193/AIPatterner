// Simulation engine for generating realistic life events with rich sensor data
namespace AIPatterner.Tests.Integration;

using AIPatterner.Application.Commands;
using AIPatterner.Application.DTOs;
using AIPatterner.Application.Handlers;
using AIPatterner.Domain.Entities;
using System.Text.Json;
using Xunit.Abstractions;

public class SimulationEngine
{
    private readonly IngestEventCommandHandler _eventHandler;
    private readonly ITestOutputHelper _output;

    public SimulationEngine(IngestEventCommandHandler eventHandler, ITestOutputHelper output)
    {
        _eventHandler = eventHandler;
        _output = output;
    }

    #region Piotr Simulation Methods

    public async Task SimulatePiotrArrivesHome(DateTime timestamp, Random random, bool isWinter = false)
    {
        const string personId = "life_sim_piotr";
        var signalStates = GenerateArrivalHomeSignals(personId, isWinter);
        
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
                PresentPeople = new List<string> { "life_sim_piotr" },
                StateSignals = new Dictionary<string, string>()
            },
            SignalStates = signalStates
        };

        await _eventHandler.Handle(new IngestEventCommand { Event = eventDto }, CancellationToken.None);
    }

    public async Task SimulatePiotrSitsOnCouch(DateTime timestamp, Random random)
    {
        var signalStates = GenerateCouchSignals("life_sim_piotr", occupied: true);
        
        var eventDto = new ActionEventDto
        {
            PersonId = "life_sim_piotr",
            ActionType = "sit_on_couch",
            TimestampUtc = timestamp,
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = GetTimeBucket(timestamp),
                DayType = GetDayType(timestamp),
                Location = "living_room",
                PresentPeople = new List<string> { "life_sim_piotr" },
                StateSignals = new Dictionary<string, string>
                {
                    { "couch_pressure_sensor", "occupied" },
                    { "living_room_motion", "active" }
                }
            },
            SignalStates = signalStates
        };

        await _eventHandler.Handle(new IngestEventCommand { Event = eventDto }, CancellationToken.None);
    }

    public async Task SimulatePiotrPlaysMusic(DateTime timestamp, Random random)
    {
        var volume = 30 + random.Next(0, 40); // 30-70% volume
        
        var signalStates = GenerateMusicSystemSignals(on: true, volume: volume);
        
        var eventDto = new ActionEventDto
        {
            PersonId = "life_sim_piotr",
            ActionType = "play_music",
            TimestampUtc = timestamp,
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = GetTimeBucket(timestamp),
                DayType = GetDayType(timestamp),
                Location = "living_room",
                PresentPeople = new List<string> { "life_sim_piotr" },
                StateSignals = new Dictionary<string, string>
                {
                    { "music_system", "on" },
                    { "music_volume", volume.ToString() }
                }
            },
            SignalStates = signalStates
        };

        await _eventHandler.Handle(new IngestEventCommand { Event = eventDto }, CancellationToken.None);
    }

    public async Task SimulatePiotrBoilsKettle(DateTime timestamp, Random random)
    {
        var signalStates = GenerateKettleSignals(heating: true);
        
        var eventDto = new ActionEventDto
        {
            PersonId = "life_sim_piotr",
            ActionType = "boil_kettle",
            TimestampUtc = timestamp,
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = GetTimeBucket(timestamp),
                DayType = GetDayType(timestamp),
                Location = "kitchen",
                PresentPeople = new List<string> { "life_sim_piotr" },
                StateSignals = new Dictionary<string, string>
                {
                    { "kettle", "heating" },
                    { "kitchen_motion", "active" }
                }
            },
            SignalStates = signalStates
        };

        await _eventHandler.Handle(new IngestEventCommand { Event = eventDto }, CancellationToken.None);
    }

    public async Task SimulatePiotrRandomDailyEvent(DateTime timestamp, Random random)
    {
        var actions = new[]
        {
            "laptop_active",
            "phone_screen_on",
            "tv_on",
            "turn_on_lights",
            "open_fridge",
            "use_microwave"
        };
        
        var action = actions[random.Next(actions.Length)];
        var signalStates = GenerateRandomDailySignals("life_sim_piotr", action, random);
        
        var eventDto = new ActionEventDto
        {
            PersonId = "life_sim_piotr",
            ActionType = action,
            TimestampUtc = timestamp,
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = GetTimeBucket(timestamp),
                DayType = GetDayType(timestamp),
                Location = GetLocationForAction(action),
                PresentPeople = new List<string> { "life_sim_piotr" },
                StateSignals = new Dictionary<string, string>()
            },
            SignalStates = signalStates
        };

        await _eventHandler.Handle(new IngestEventCommand { Event = eventDto }, CancellationToken.None);
    }

    #endregion

    #region Victoria Simulation Methods

    public async Task SimulateVictoriaWakesUp(DateTime timestamp, Random random)
    {
        var signalStates = GenerateWakeUpSignals("life_sim_victoria");
        
        var eventDto = new ActionEventDto
        {
            PersonId = "life_sim_victoria",
            ActionType = "WakeUp",
            TimestampUtc = timestamp,
            EventType = EventType.StateChange,
            Context = new ActionContextDto
            {
                TimeBucket = "morning",
                DayType = GetDayType(timestamp),
                Location = "bedroom",
                PresentPeople = new List<string> { "life_sim_victoria" },
                StateSignals = new Dictionary<string, string>
                {
                    { "bed_pressure_sensor", "unoccupied" },
                    { "bedroom_motion", "active" }
                }
            },
            SignalStates = signalStates
        };

        await _eventHandler.Handle(new IngestEventCommand { Event = eventDto }, CancellationToken.None);
    }

    public async Task SimulateVictoriaKitchenRoutine(DateTime timestamp, Random random)
    {
        // Kitchen lights
        var lightsEvent = new ActionEventDto
        {
            PersonId = "life_sim_victoria",
            ActionType = "kitchen_lights_on",
            TimestampUtc = timestamp,
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = "morning",
                DayType = GetDayType(timestamp),
                Location = "kitchen",
                PresentPeople = new List<string> { "life_sim_victoria" },
                StateSignals = new Dictionary<string, string>
                {
                    { "kitchen_lights", "on" },
                    { "kitchen_motion", "active" }
                }
            },
            SignalStates = GenerateKitchenLightsSignals(on: true)
        };
        await _eventHandler.Handle(new IngestEventCommand { Event = lightsEvent }, CancellationToken.None);
        await Task.Delay(50);

        // Coffee machine
        var coffeeEvent = new ActionEventDto
        {
            PersonId = "life_sim_victoria",
            ActionType = "coffee_machine_on",
            TimestampUtc = timestamp.AddMinutes(1 + random.NextDouble() * 2),
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = "morning",
                DayType = GetDayType(timestamp),
                Location = "kitchen",
                PresentPeople = new List<string> { "life_sim_victoria" },
                StateSignals = new Dictionary<string, string>
                {
                    { "coffee_machine", "brewing" },
                    { "kitchen_motion", "active" }
                }
            },
            SignalStates = GenerateCoffeeMachineSignals(brewing: true)
        };
        await _eventHandler.Handle(new IngestEventCommand { Event = coffeeEvent }, CancellationToken.None);
    }

    public async Task SimulateVictoriaRandomDailyEvent(DateTime timestamp, Random random)
    {
        var actions = new[]
        {
            "cook_on_stove",
            "use_microwave",
            "open_fridge",
            "turn_on_lights",
            "tv_on"
        };
        
        var action = actions[random.Next(actions.Length)];
        var signalStates = GenerateRandomDailySignals("life_sim_victoria", action, random);
        
        var eventDto = new ActionEventDto
        {
            PersonId = "life_sim_victoria",
            ActionType = action,
            TimestampUtc = timestamp,
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = GetTimeBucket(timestamp),
                DayType = GetDayType(timestamp),
                Location = GetLocationForAction(action),
                PresentPeople = new List<string> { "life_sim_victoria" },
                StateSignals = new Dictionary<string, string>()
            },
            SignalStates = signalStates
        };

        await _eventHandler.Handle(new IngestEventCommand { Event = eventDto }, CancellationToken.None);
    }

    #endregion

    #region Andrii Simulation Methods

    public async Task SimulateAndriiSitsOnCouch(DateTime timestamp, Random random)
    {
        var signalStates = GenerateCouchSignals("life_sim_andrii", occupied: true);
        
        var eventDto = new ActionEventDto
        {
            PersonId = "life_sim_andrii",
            ActionType = "sit_on_couch",
            TimestampUtc = timestamp,
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = GetTimeBucket(timestamp),
                DayType = GetDayType(timestamp),
                Location = "living_room",
                PresentPeople = new List<string> { "life_sim_andrii" },
                StateSignals = new Dictionary<string, string>
                {
                    { "couch_pressure_sensor", "occupied" },
                    { "living_room_motion", "active" }
                }
            },
            SignalStates = signalStates
        };

        await _eventHandler.Handle(new IngestEventCommand { Event = eventDto }, CancellationToken.None);
    }

    public async Task SimulateAndriiRandomAction(DateTime timestamp, Random random)
    {
        var actions = new[]
        {
            "andrii_random_action_1",
            "andrii_random_action_2",
            "andrii_random_action_3",
            "tv_on",
            "phone_active",
            "open_fridge"
        };
        
        var action = actions[random.Next(actions.Length)];
        var signalStates = GenerateRandomDailySignals("life_sim_andrii", action, random);
        
        var eventDto = new ActionEventDto
        {
            PersonId = "life_sim_andrii",
            ActionType = action,
            TimestampUtc = timestamp,
            EventType = EventType.Action,
            Context = new ActionContextDto
            {
                TimeBucket = GetTimeBucket(timestamp),
                DayType = GetDayType(timestamp),
                Location = GetLocationForAction(action),
                PresentPeople = new List<string> { "life_sim_andrii" },
                StateSignals = new Dictionary<string, string>()
            },
            SignalStates = signalStates
        };

        await _eventHandler.Handle(new IngestEventCommand { Event = eventDto }, CancellationToken.None);
    }

    #endregion

    #region Signal State Generators

    private List<SignalStateDto> GenerateArrivalHomeSignals(string personId, bool isWinter)
    {
        var signals = new List<SignalStateDto>();
        
        // Front door opens
        signals.Add(new SignalStateDto
        {
            SensorId = "front_door_sensor",
            Value = JsonSerializer.SerializeToElement("open")
        });
        
        // Phone connects to Wi-Fi
        signals.Add(new SignalStateDto
        {
            SensorId = $"{personId}_phone_wifi",
            Value = JsonSerializer.SerializeToElement("connected")
        });
        
        // Hallway motion
        signals.Add(new SignalStateDto
        {
            SensorId = "hallway_motion",
            Value = JsonSerializer.SerializeToElement("active")
        });
        
        // Lighting based on season
        if (isWinter)
        {
            signals.Add(new SignalStateDto
            {
                SensorId = "living_room_lights",
                Value = JsonSerializer.SerializeToElement("on")
            });
        }
        
        return signals;
    }

    private List<SignalStateDto> GenerateCouchSignals(string personId, bool occupied)
    {
        var signals = new List<SignalStateDto>();
        
        signals.Add(new SignalStateDto
        {
            SensorId = "couch_pressure_sensor",
            Value = JsonSerializer.SerializeToElement(occupied ? "occupied" : "vacant")
        });
        
        signals.Add(new SignalStateDto
        {
            SensorId = "living_room_motion",
            Value = JsonSerializer.SerializeToElement(occupied ? "active" : "inactive")
        });
        
        if (occupied)
        {
            signals.Add(new SignalStateDto
            {
                SensorId = $"{personId}_presence",
                Value = JsonSerializer.SerializeToElement("living_room")
            });
        }
        
        return signals;
    }

    private List<SignalStateDto> GenerateMusicSystemSignals(bool on, int volume = 50)
    {
        var signals = new List<SignalStateDto>();
        
        signals.Add(new SignalStateDto
        {
            SensorId = "music_system",
            Value = JsonSerializer.SerializeToElement(on ? "on" : "off")
        });
        
        if (on)
        {
            signals.Add(new SignalStateDto
            {
                SensorId = "music_volume",
                Value = JsonSerializer.SerializeToElement(volume)
            });
        }
        
        return signals;
    }

    private List<SignalStateDto> GenerateKettleSignals(bool heating)
    {
        var signals = new List<SignalStateDto>();
        
        signals.Add(new SignalStateDto
        {
            SensorId = "kettle",
            Value = JsonSerializer.SerializeToElement(heating ? "heating" : "idle")
        });
        
        signals.Add(new SignalStateDto
        {
            SensorId = "kitchen_motion",
            Value = JsonSerializer.SerializeToElement(heating ? "active" : "inactive")
        });
        
        return signals;
    }

    private List<SignalStateDto> GenerateWakeUpSignals(string personId)
    {
        var signals = new List<SignalStateDto>();
        
        signals.Add(new SignalStateDto
        {
            SensorId = "bed_pressure_sensor",
            Value = JsonSerializer.SerializeToElement("unoccupied")
        });
        
        signals.Add(new SignalStateDto
        {
            SensorId = "bedroom_motion",
            Value = JsonSerializer.SerializeToElement("active")
        });
        
        signals.Add(new SignalStateDto
        {
            SensorId = $"{personId}_phone_screen",
            Value = JsonSerializer.SerializeToElement("on")
        });
        
        return signals;
    }

    private List<SignalStateDto> GenerateKitchenLightsSignals(bool on)
    {
        var signals = new List<SignalStateDto>();
        
        signals.Add(new SignalStateDto
        {
            SensorId = "kitchen_lights",
            Value = JsonSerializer.SerializeToElement(on ? "on" : "off")
        });
        
        signals.Add(new SignalStateDto
        {
            SensorId = "kitchen_motion",
            Value = JsonSerializer.SerializeToElement(on ? "active" : "inactive")
        });
        
        return signals;
    }

    private List<SignalStateDto> GenerateCoffeeMachineSignals(bool brewing)
    {
        var signals = new List<SignalStateDto>();
        
        signals.Add(new SignalStateDto
        {
            SensorId = "coffee_machine",
            Value = JsonSerializer.SerializeToElement(brewing ? "brewing" : "idle")
        });
        
        return signals;
    }

    private List<SignalStateDto> GenerateRandomDailySignals(string personId, string action, Random random)
    {
        var signals = new List<SignalStateDto>();
        
        // Add person presence
        signals.Add(new SignalStateDto
        {
            SensorId = $"{personId}_presence",
            Value = JsonSerializer.SerializeToElement(GetLocationForAction(action))
        });
        
        // Add action-specific signals
        switch (action)
        {
            case "laptop_active":
                signals.Add(new SignalStateDto
                {
                    SensorId = $"{personId}_laptop",
                    Value = JsonSerializer.SerializeToElement("active")
                });
                break;
            case "phone_screen_on":
                signals.Add(new SignalStateDto
                {
                    SensorId = $"{personId}_phone_screen",
                    Value = JsonSerializer.SerializeToElement("on")
                });
                break;
            case "tv_on":
                signals.Add(new SignalStateDto
                {
                    SensorId = "tv",
                    Value = JsonSerializer.SerializeToElement("on")
                });
                break;
            case "open_fridge":
                signals.Add(new SignalStateDto
                {
                    SensorId = "fridge_door",
                    Value = JsonSerializer.SerializeToElement("open")
                });
                break;
        }
        
        return signals;
    }

    #endregion

    #region Helper Methods

    private string GetTimeBucket(DateTime timestamp)
    {
        var hour = timestamp.Hour;
        if (hour >= 5 && hour < 12) return "morning";
        if (hour >= 12 && hour < 17) return "afternoon";
        if (hour >= 17 && hour < 22) return "evening";
        return "night";
    }

    private string GetDayType(DateTime timestamp)
    {
        return timestamp.DayOfWeek == DayOfWeek.Saturday || timestamp.DayOfWeek == DayOfWeek.Sunday
            ? "weekend"
            : "weekday";
    }

    private string GetLocationForAction(string action)
    {
        return action switch
        {
            "sit_on_couch" or "tv_on" or "play_music" => "living_room",
            "kitchen_lights_on" or "coffee_machine_on" or "boil_kettle" or "cook_on_stove" or "use_microwave" or "open_fridge" => "kitchen",
            "WakeUp" or "wake_up" => "bedroom",
            _ => "home"
        };
    }

    #endregion
}

