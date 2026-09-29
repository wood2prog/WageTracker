using WageTracker.Domain.Calendar;
using WageTracker.Domain.Common;
using WageTracker.Domain.Payroll;

namespace WageTracker.Domain.Tests;

public class PayPeriodScheduleTests
{
    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    [Theory]
    [InlineData(2026, 9, 6)]   // Sunday
    [InlineData(2026, 9, 9)]   // Wednesday
    [InlineData(2026, 9, 12)]  // Saturday
    public void Work_week_runs_Sunday_through_Saturday(int y, int m, int d)
    {
        var week = WorkWeek.Containing(D(y, m, d));

        Assert.Equal(D(2026, 9, 6), week.Start);
        Assert.Equal(D(2026, 9, 12), week.End);
        Assert.Equal(new DateTime(2026, 9, 13), week.EndsAt);
    }

    [Fact]
    public void Work_week_rejects_a_start_that_is_not_Sunday() =>
        Assert.Throws<DomainException>(() => new WorkWeek(D(2026, 9, 7)));

    [Fact]
    public void Weekly_period_is_the_containing_week()
    {
        var period = PayPeriodSchedule.Weekly().PeriodContaining(D(2026, 9, 29));

        Assert.Equal(D(2026, 9, 27), period.Start);
        Assert.Equal(D(2026, 10, 3), period.End);
    }

    [Fact]
    public void Monthly_period_for_September_2026_runs_Sept_6_to_Oct_3()
    {
        var period = PayPeriodSchedule.Monthly().PeriodContaining(D(2026, 9, 15));

        Assert.Equal(D(2026, 9, 6), period.Start);
        Assert.Equal(D(2026, 10, 3), period.End);
        Assert.Equal(4, period.WeekCount);
    }

    [Fact]
    public void Days_before_the_first_Sunday_belong_to_the_previous_months_period()
    {
        var period = PayPeriodSchedule.Monthly().PeriodContaining(D(2026, 9, 3));

        Assert.Equal(D(2026, 8, 2), period.Start);
        Assert.Equal(D(2026, 9, 5), period.End);
        Assert.Equal(5, period.WeekCount);
    }

    [Fact]
    public void Days_in_the_trailing_week_belong_to_the_month_of_its_Sunday()
    {
        var period = PayPeriodSchedule.Monthly().PeriodContaining(D(2026, 10, 2));

        Assert.Equal(D(2026, 9, 6), period.Start);
    }

    [Fact]
    public void Monthly_periods_are_contiguous_across_a_year()
    {
        var schedule = PayPeriodSchedule.Monthly();
        var period = schedule.PeriodContaining(D(2026, 1, 15));

        for (var i = 0; i < 12; i++)
        {
            var next = schedule.Next(period);
            Assert.Equal(period.End.AddDays(1), next.Start);
            period = next;
        }
    }

    [Fact]
    public void BiWeekly_periods_repeat_every_14_days_from_the_anchor_in_both_directions()
    {
        var schedule = PayPeriodSchedule.BiWeekly(D(2026, 9, 6));

        Assert.Equal(D(2026, 9, 6), schedule.PeriodContaining(D(2026, 9, 19)).Start);
        Assert.Equal(D(2026, 9, 20), schedule.PeriodContaining(D(2026, 9, 20)).Start);
        Assert.Equal(D(2026, 8, 23), schedule.PeriodContaining(D(2026, 9, 5)).Start);
        Assert.Equal(D(2026, 9, 5), schedule.PeriodContaining(D(2026, 9, 5)).End);
    }
}
