using WageTracker.Domain.Common;
using WageTracker.Domain.TimeOff;

namespace WageTracker.Domain.Tests;

public class HolidayCalendarTests
{
    [Fact]
    public void Fixed_rule_gives_the_same_date_each_year() =>
        Assert.Equal(new DateOnly(2026, 12, 25), HolidayRule.Fixed(12, 25).DateIn(2026));

    [Fact]
    public void Fixed_rule_rejects_dates_missing_in_some_years() =>
        Assert.Throws<DomainException>(() => HolidayRule.Fixed(2, 29));

    [Theory]
    [InlineData(2026, 26)]
    [InlineData(2027, 25)]
    public void Nth_weekday_rule_finds_Thanksgiving(int year, int day) =>
        Assert.Equal(new DateOnly(year, 11, day), HolidayRule.NthWeekday(11, DayOfWeek.Thursday, 4).DateIn(year));

    [Theory]
    [InlineData(2026, 25)]
    [InlineData(2027, 31)]
    public void Last_weekday_rule_finds_Memorial_Day(int year, int day) =>
        Assert.Equal(new DateOnly(year, 5, day), HolidayRule.LastWeekday(5, DayOfWeek.Monday).DateIn(year));

    [Fact]
    public void An_observed_date_replaces_the_rule_date_for_that_year_only()
    {
        var calendar = new HolidayCalendar();
        var christmas = calendar.Add("Christmas", HolidayRule.Fixed(12, 25));

        calendar.SetObservedDate(christmas.Id, 2027, new DateOnly(2027, 12, 24)); // Dec 25, 2027 is a Saturday

        Assert.Equal(new DateOnly(2027, 12, 24), christmas.DateIn(2027));
        Assert.Equal(new DateOnly(2026, 12, 25), christmas.DateIn(2026));
    }

    [Fact]
    public void An_observed_date_can_move_into_the_previous_year()
    {
        var calendar = new HolidayCalendar();
        var newYear = calendar.Add("New Year's Day", HolidayRule.Fixed(1, 1));
        calendar.SetObservedDate(newYear.Id, 2028, new DateOnly(2027, 12, 31)); // Jan 1, 2028 is a Saturday

        var inDecember = calendar.Between(new DateOnly(2027, 12, 26), new DateOnly(2028, 1, 1));

        Assert.Equal([new DateOnly(2027, 12, 31)], inDecember.Select(h => h.Date));
    }

    [Fact]
    public void An_observed_date_must_be_near_the_rule_date()
    {
        var calendar = new HolidayCalendar();
        var christmas = calendar.Add("Christmas", HolidayRule.Fixed(12, 25));

        Assert.Throws<DomainException>(() => calendar.SetObservedDate(christmas.Id, 2027, new DateOnly(2027, 12, 1)));
    }

    [Fact]
    public void Holiday_names_must_be_unique()
    {
        var calendar = new HolidayCalendar();
        calendar.Add("Christmas", HolidayRule.Fixed(12, 25));

        Assert.Throws<DomainException>(() => calendar.Add("christmas", HolidayRule.Fixed(12, 26)));
    }
}
