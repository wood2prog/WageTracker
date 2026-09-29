using WageTracker.Domain.Common;

namespace WageTracker.Domain.Calendar;

/// <summary>
/// A work week runs from Sunday 00:00 up to, but not including, the next Sunday 00:00.
/// </summary>
public readonly record struct WorkWeek
{
    public WorkWeek(DateOnly sunday)
    {
        if (sunday.DayOfWeek != DayOfWeek.Sunday)
            throw new DomainException($"A work week must start on a Sunday; {sunday:yyyy-MM-dd} is a {sunday.DayOfWeek}.");
        Start = sunday;
    }

    /// <summary>The Sunday the week starts on.</summary>
    public DateOnly Start { get; }

    /// <summary>The Saturday the week ends on (inclusive).</summary>
    public DateOnly End => Start.AddDays(6);

    /// <summary>Sunday 00:00 at the start of the week (inclusive).</summary>
    public DateTime StartsAt => Start.ToDateTime(TimeOnly.MinValue);

    /// <summary>The next Sunday 00:00 (exclusive).</summary>
    public DateTime EndsAt => Start.AddDays(7).ToDateTime(TimeOnly.MinValue);

    public static WorkWeek Containing(DateOnly date) => new(date.AddDays(-(int)date.DayOfWeek));

    public static WorkWeek Containing(DateTime moment) => Containing(DateOnly.FromDateTime(moment));

    public WorkWeek Next() => new(Start.AddDays(7));

    public WorkWeek Previous() => new(Start.AddDays(-7));

    public bool Contains(DateOnly date) => date >= Start && date <= End;

    public override string ToString() => $"{Start:yyyy-MM-dd} – {End:yyyy-MM-dd}";
}
