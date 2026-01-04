// Domain entity representing user preferences for reminder behavior
namespace AIPatterner.Domain.Entities;

public class UserReminderPreferences
{
    public Guid Id { get; private set; }
    public string PersonId { get; private set; }
    public ReminderStyle DefaultStyle { get; private set; }
    public int DailyLimit { get; private set; }
    public TimeSpan MinimumInterval { get; private set; }
    public bool Enabled { get; private set; }
    public bool AllowAutoExecute { get; private set; } = false; // Default false - user must opt-in
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private UserReminderPreferences() { } // EF Core

    public UserReminderPreferences(
        string personId,
        ReminderStyle defaultStyle = ReminderStyle.Ask,
        int dailyLimit = 10,
        TimeSpan? minimumInterval = null)
    {
        if (string.IsNullOrWhiteSpace(personId))
            throw new ArgumentException("PersonId cannot be null or empty", nameof(personId));
        if (dailyLimit < 0)
            throw new ArgumentException("DailyLimit cannot be negative", nameof(dailyLimit));

        Id = Guid.NewGuid();
        PersonId = personId;
        DefaultStyle = defaultStyle;
        DailyLimit = dailyLimit;
        MinimumInterval = minimumInterval ?? TimeSpan.FromMinutes(15);
        Enabled = true;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Update(
        ReminderStyle? defaultStyle = null,
        int? dailyLimit = null,
        TimeSpan? minimumInterval = null,
        bool? enabled = null,
        bool? allowAutoExecute = null)
    {
        if (dailyLimit.HasValue && dailyLimit.Value < 0)
            throw new ArgumentException("DailyLimit cannot be negative", nameof(dailyLimit));

        if (defaultStyle.HasValue)
            DefaultStyle = defaultStyle.Value;
        if (dailyLimit.HasValue)
            DailyLimit = dailyLimit.Value;
        if (minimumInterval.HasValue)
            MinimumInterval = minimumInterval.Value;
        if (enabled.HasValue)
            Enabled = enabled.Value;
        if (allowAutoExecute.HasValue)
            AllowAutoExecute = allowAutoExecute.Value;

        UpdatedAtUtc = DateTime.UtcNow;
    }
}

