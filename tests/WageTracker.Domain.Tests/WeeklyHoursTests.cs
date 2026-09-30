using WageTracker.Domain.Calendar;
using WageTracker.Domain.Common;
using WageTracker.Domain.Employees;
using WageTracker.Domain.Payroll;
using WageTracker.Domain.TimeTracking;
using static WageTracker.Domain.Tests.TestData;

namespace WageTracker.Domain.Tests;

public class WeeklyHoursTests
{
    private static readonly WorkWeek Week = new(Sunday);
    private static readonly DateOnly Today = new(2026, 9, 29);
    private static readonly DateTime Now = new(2026, 9, 29, 17, 0, 0);

    private static Employee WeeklyWorker(DateOnly? hired = null)
    {
        var employee = Hourly(hired: hired);
        employee.ChangeTimeRecording(TimeRecording.Weekly);
        return employee;
    }

    [Fact]
    public void Records_a_weekly_total()
    {
        var employee = WeeklyWorker();

        var total = WeeklyHours.Record(employee, Week, 42.5m, [], Today);

        Assert.Equal(employee.Id, total.EmployeeId);
        Assert.Equal(Week, total.Week);
        Assert.Equal(42.5m, total.Hours);
    }

    [Fact]
    public void Employees_whose_hours_are_entered_daily_cannot_have_a_weekly_total()
    {
        Assert.Throws<DomainException>(() => WeeklyHours.Record(Hourly(), Week, 40m, [], Today));
    }

    [Fact]
    public void The_week_must_have_started()
    {
        var thisWeek = new WorkWeek(new DateOnly(2026, 9, 27));

        WeeklyHours.Record(WeeklyWorker(), thisWeek, 30m, [], Today);
        Assert.Throws<DomainException>(() => WeeklyHours.Record(WeeklyWorker(), thisWeek.Next(), 30m, [], Today));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(40.125)]
    [InlineData(168.01)]
    public void Hours_must_be_positive_with_two_places_and_fit_in_the_week(decimal hours)
    {
        Assert.Throws<DomainException>(() => WeeklyHours.Record(WeeklyWorker(), Week, hours, [], Today));
    }

    [Fact]
    public void Hours_are_limited_to_24_for_each_day_employed()
    {
        // Hired Thursday, so employed Thursday through Saturday: at most 72 hours.
        var employee = WeeklyWorker(hired: Sunday.AddDays(4));

        WeeklyHours.Record(employee, Week, 72m, [], Today);
        Assert.Throws<DomainException>(() => WeeklyHours.Record(employee, Week, 72.5m, [], Today));
        Assert.Throws<DomainException>(() => WeeklyHours.Record(employee, Week.Previous(), 8m, [], Today));
    }

    [Fact]
    public void A_week_with_time_entries_cannot_also_have_a_total()
    {
        var employee = WeeklyWorker();
        var entry = Shift(employee, Sunday.AddDays(1), 8m);

        Assert.Throws<DomainException>(() => WeeklyHours.Record(employee, Week, 40m, [entry], Today));
        WeeklyHours.Record(employee, Week.Next(), 40m, [entry], Today);
    }

    [Fact]
    public void Time_entries_cannot_be_recorded_for_employees_whose_hours_are_entered_weekly()
    {
        var monday8 = Sunday.AddDays(1).ToDateTime(new TimeOnly(8, 0));

        Assert.Throws<DomainException>(() => TimeEntry.Record(WeeklyWorker(), monday8, monday8.AddHours(8), [], [], Now));
    }

    [Fact]
    public void Time_entries_cannot_go_in_a_week_that_has_a_total()
    {
        var employee = WeeklyWorker();
        var total = WeeklyHours.Record(employee, Week, 40m, [], Today);
        employee.ChangeTimeRecording(TimeRecording.Daily);
        var saturday10pm = Sunday.AddDays(6).ToDateTime(new TimeOnly(22, 0));
        var nextMonday = Sunday.AddDays(8).ToDateTime(new TimeOnly(8, 0));

        Assert.Throws<DomainException>(() => TimeEntry.Record(employee, saturday10pm, saturday10pm.AddHours(4), [], [total], Now));
        TimeEntry.Record(employee, nextMonday, nextMonday.AddHours(8), [], [total], Now);
    }

    [Fact]
    public void Weekly_totals_count_as_worked_hours_and_earn_overtime()
    {
        var employee = WeeklyWorker();
        var total = new WeeklyHours(employee.Id, Week, 45m);
        var settings = Settings();

        var statement = PayStatement.Calculate(
            employee, settings.PeriodContaining(Sunday), settings, [], [], previous: null, weeklyTotals: [total]);

        Assert.Equal(45m, statement.WorkedHours);
        Assert.Equal(5m, statement.OvertimeHours);
        Assert.Equal(40m * 20m + 5m * 30m, statement.GrossPay);
    }
}
