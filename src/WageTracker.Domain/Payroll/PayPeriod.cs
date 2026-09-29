using WageTracker.Domain.Calendar;
using WageTracker.Domain.Common;

namespace WageTracker.Domain.Payroll;

/// <summary>
/// A run of one or more whole work weeks, from a Sunday through a Saturday.
/// </summary>
public sealed record PayPeriod
{
    public PayPeriod(WorkWeek firstWeek, int weekCount)
    {
        if (weekCount < 1)
            throw new DomainException("A pay period must contain at least one week.");
        Start = firstWeek.Start;
        WeekCount = weekCount;
    }

    /// <summary>The Sunday the period starts on.</summary>
    public DateOnly Start { get; }

    public int WeekCount { get; }

    /// <summary>The Saturday the period ends on (inclusive).</summary>
    public DateOnly End => Start.AddDays(WeekCount * 7 - 1);

    public DateTime StartsAt => Start.ToDateTime(TimeOnly.MinValue);

    /// <summary>The Sunday 00:00 after the period (exclusive).</summary>
    public DateTime EndsAt => End.AddDays(1).ToDateTime(TimeOnly.MinValue);

    public IEnumerable<WorkWeek> Weeks
    {
        get
        {
            var week = new WorkWeek(Start);
            for (var i = 0; i < WeekCount; i++, week = week.Next())
                yield return week;
        }
    }

    public bool Contains(DateOnly date) => date >= Start && date <= End;

    /// <summary>
    /// The year this period is the last pay period of (the period that contains December 31), or null.
    /// </summary>
    public int? ClosesYear => Contains(new DateOnly(Start.Year, 12, 31)) ? Start.Year : null;

    public override string ToString() => $"{Start:yyyy-MM-dd} – {End:yyyy-MM-dd}";
}
