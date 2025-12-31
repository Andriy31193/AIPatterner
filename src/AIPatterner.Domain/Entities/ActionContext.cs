// Value object representing the context in which an action occurred
namespace AIPatterner.Domain.Entities;

public class ActionContext
{
    public string TimeBucket { get; private set; }
    public string DayType { get; private set; }
    public string? Location { get; private set; }
    public List<string> PresentPeople { get; private set; }
    public Dictionary<string, string> StateSignals { get; private set; }

    private ActionContext() { } // EF Core

    public ActionContext(
        string timeBucket,
        string dayType,
        string? location = null,
        List<string>? presentPeople = null,
        Dictionary<string, string>? stateSignals = null)
    {
        if (string.IsNullOrWhiteSpace(timeBucket))
            throw new ArgumentException("TimeBucket cannot be null or empty", nameof(timeBucket));
        if (string.IsNullOrWhiteSpace(dayType))
            throw new ArgumentException("DayType cannot be null or empty", nameof(dayType));

        TimeBucket = timeBucket;
        DayType = dayType;
        Location = location;
        PresentPeople = presentPeople ?? new List<string>();
        StateSignals = stateSignals ?? new Dictionary<string, string>();
    }
}

