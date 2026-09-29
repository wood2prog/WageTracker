using WageTracker.Domain.Calendar;

namespace WageTracker.Domain.Payroll;

/// <summary>
/// Divides the calendar into consecutive, non-overlapping pay periods made of whole work weeks.
/// </summary>
public abstract class PayPeriodSchedule
{
    public abstract PayFrequency Frequency { get; }

    /// <summary>For a two-week schedule, the Sunday periods repeat from; otherwise null.</summary>
    public virtual DateOnly? Anchor => null;

    public abstract PayPeriod PeriodContaining(DateOnly date);

    public PayPeriod Next(PayPeriod period) => PeriodContaining(period.End.AddDays(1));

    public PayPeriod Previous(PayPeriod period) => PeriodContaining(period.Start.AddDays(-1));

    public static PayPeriodSchedule Weekly() => new WeeklySchedule();

    /// <param name="anchorSunday">Any Sunday that starts a pay period; periods repeat every 14 days from it.</param>
    public static PayPeriodSchedule BiWeekly(DateOnly anchorSunday) => new BiWeeklySchedule(new WorkWeek(anchorSunday));

    public static PayPeriodSchedule Monthly() => new MonthlySchedule();

    private sealed class WeeklySchedule : PayPeriodSchedule
    {
        public override PayFrequency Frequency => PayFrequency.Weekly;

        public override PayPeriod PeriodContaining(DateOnly date) => new(WorkWeek.Containing(date), 1);
    }

    private sealed class BiWeeklySchedule(WorkWeek anchor) : PayPeriodSchedule
    {
        public override PayFrequency Frequency => PayFrequency.BiWeekly;

        public override DateOnly? Anchor => anchor.Start;

        public override PayPeriod PeriodContaining(DateOnly date)
        {
            var daysFromAnchor = date.DayNumber - anchor.Start.DayNumber;
            var periodIndex = (int)Math.Floor(daysFromAnchor / 14.0);
            return new PayPeriod(new WorkWeek(anchor.Start.AddDays(periodIndex * 14)), 2);
        }
    }

    /// <summary>
    /// A week belongs to the month its Sunday falls in. The period for a month runs from the
    /// month's first Sunday through the Saturday after its last Sunday, which can be in the next month.
    /// </summary>
    private sealed class MonthlySchedule : PayPeriodSchedule
    {
        public override PayFrequency Frequency => PayFrequency.Monthly;

        public override PayPeriod PeriodContaining(DateOnly date)
        {
            var sunday = WorkWeek.Containing(date).Start;
            return ForMonth(sunday.Year, sunday.Month);
        }

        private static PayPeriod ForMonth(int year, int month)
        {
            var firstOfMonth = new DateOnly(year, month, 1);
            var firstSunday = firstOfMonth.AddDays((7 - (int)firstOfMonth.DayOfWeek) % 7);
            var lastOfMonth = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            var lastSunday = WorkWeek.Containing(lastOfMonth).Start;
            var weekCount = (lastSunday.DayNumber - firstSunday.DayNumber) / 7 + 1;
            return new PayPeriod(new WorkWeek(firstSunday), weekCount);
        }
    }
}
