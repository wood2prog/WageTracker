using WageTracker.Domain.Common;

namespace WageTracker.Domain.TimeOff;

/// <summary>How a company holiday's date is found each year.</summary>
public abstract record HolidayRule
{
    private protected HolidayRule(int month)
    {
        if (month is < 1 or > 12)
            throw new DomainException("Month must be between 1 and 12.");
        Month = month;
    }

    public int Month { get; }

    public abstract DateOnly DateIn(int year);

    /// <summary>The same date every year, such as December 25.</summary>
    public static HolidayRule Fixed(int month, int day) => new FixedDateRule(month, day);

    /// <summary>The nth weekday of a month, such as the 4th Thursday of November.</summary>
    public static HolidayRule NthWeekday(int month, DayOfWeek dayOfWeek, int occurrence) =>
        new NthWeekdayRule(month, dayOfWeek, occurrence);

    /// <summary>The last weekday of a month, such as the last Monday of May.</summary>
    public static HolidayRule LastWeekday(int month, DayOfWeek dayOfWeek) => new LastWeekdayRule(month, dayOfWeek);
}

public sealed record FixedDateRule : HolidayRule
{
    public FixedDateRule(int month, int day) : base(month)
    {
        // Checked against a non-leap year so the date exists every year.
        if (day < 1 || day > DateTime.DaysInMonth(2001, month))
            throw new DomainException("That day does not exist in that month every year.");
        Day = day;
    }

    public int Day { get; }

    public override DateOnly DateIn(int year) => new(year, Month, Day);
}

public sealed record NthWeekdayRule : HolidayRule
{
    public NthWeekdayRule(int month, DayOfWeek dayOfWeek, int occurrence) : base(month)
    {
        if (occurrence is < 1 or > 4)
            throw new DomainException("Occurrence must be 1 to 4. Use a last-weekday rule for the last one.");
        DayOfWeek = dayOfWeek;
        Occurrence = occurrence;
    }

    public DayOfWeek DayOfWeek { get; }

    public int Occurrence { get; }

    public override DateOnly DateIn(int year)
    {
        var first = new DateOnly(year, Month, 1);
        var firstMatch = first.AddDays(((int)DayOfWeek - (int)first.DayOfWeek + 7) % 7);
        return firstMatch.AddDays((Occurrence - 1) * 7);
    }
}

public sealed record LastWeekdayRule : HolidayRule
{
    public LastWeekdayRule(int month, DayOfWeek dayOfWeek) : base(month) => DayOfWeek = dayOfWeek;

    public DayOfWeek DayOfWeek { get; }

    public override DateOnly DateIn(int year)
    {
        var last = new DateOnly(year, Month, DateTime.DaysInMonth(year, Month));
        return last.AddDays(-(((int)last.DayOfWeek - (int)DayOfWeek + 7) % 7));
    }
}
