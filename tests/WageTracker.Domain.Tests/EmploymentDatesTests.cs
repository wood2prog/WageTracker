using WageTracker.Domain.Common;
using WageTracker.Domain.Payroll;
using WageTracker.Domain.TimeOff;
using WageTracker.Domain.TimeTracking;
using static WageTracker.Domain.Tests.TestData;

namespace WageTracker.Domain.Tests;

public class EmploymentDatesTests
{
    private static readonly DateOnly Wednesday = Sunday.AddDays(3);
    private static readonly DateTime Now = new(2026, 9, 29, 17, 0, 0);

    [Fact]
    public void End_date_cannot_be_before_hire_date() =>
        Assert.Throws<DomainException>(() => Hourly(hired: Wednesday, ended: Wednesday.AddDays(-1)));

    [Fact]
    public void Employed_on_hire_date_and_end_date_inclusive()
    {
        var employee = Hourly(hired: Wednesday, ended: Wednesday.AddDays(2));

        Assert.False(employee.IsEmployedOn(Wednesday.AddDays(-1)));
        Assert.True(employee.IsEmployedOn(Wednesday));
        Assert.True(employee.IsEmployedOn(Wednesday.AddDays(2)));
        Assert.False(employee.IsEmployedOn(Wednesday.AddDays(3)));
    }

    [Fact]
    public void Time_cannot_be_recorded_outside_employment()
    {
        var employee = Hourly(hired: Wednesday);
        var tuesday = Sunday.AddDays(2).ToDateTime(new TimeOnly(8, 0));

        Assert.Throws<DomainException>(() => TimeEntry.Record(employee, tuesday, tuesday.AddHours(8), [], Now));
        TimeEntry.Record(employee, tuesday.AddDays(1), tuesday.AddDays(1).AddHours(8), [], Now);
    }

    [Fact]
    public void An_entry_ending_at_midnight_after_the_last_day_is_allowed()
    {
        var employee = Hourly(ended: Wednesday);
        var evening = Wednesday.ToDateTime(new TimeOnly(16, 0));

        TimeEntry.Record(employee, evening, evening.AddHours(8), [], Now);
        Assert.Throws<DomainException>(() => TimeEntry.Record(employee, evening, evening.AddHours(9), [], Now));
    }

    [Fact]
    public void Time_off_cannot_be_booked_outside_employment()
    {
        var employee = Hourly(ended: Wednesday);

        Assert.Throws<DomainException>(() =>
            employee.BookTimeOff(Wednesday.AddDays(1), Settings().VacationType, [], new HolidayCalendar()));
    }

    [Fact]
    public void Salary_is_prorated_by_weekdays_employed()
    {
        var employee = Salaried(52_000m, hired: Wednesday);
        var settings = Settings();

        var statement = PayStatement.Calculate(employee, settings.PeriodContaining(Sunday), settings, [], [], null);

        Assert.Equal(600m, statement.BasePay); // Wednesday–Friday: 3/5 of 1000
    }

    [Fact]
    public void Holidays_before_the_hire_date_are_not_credited()
    {
        var employee = Hourly(hired: Wednesday);
        var settings = Settings();
        settings.Holidays.Add("Labor Day", HolidayRule.NthWeekday(9, DayOfWeek.Monday, 1)); // Monday Sept 7

        var statement = PayStatement.Calculate(employee, settings.PeriodContaining(Sunday), settings, [], [], null);

        Assert.Equal(0m, statement.TimeOffHours);
    }

    [Fact]
    public void Unused_vacation_is_paid_in_the_final_period()
    {
        var employee = Hourly(20m, vacationDays: 10, ended: Wednesday);
        var settings = Settings();
        var used = new[] { DayOff(employee, new DateOnly(2026, 3, 2), settings.VacationType), DayOff(employee, new DateOnly(2026, 3, 3), settings.VacationType) };

        var statement = PayStatement.Calculate(employee, settings.PeriodContaining(Sunday), settings, [], used, null);

        Assert.Equal(8, statement.VacationDaysPaidOut);
        Assert.Equal(8m * 10m * 20m, statement.VacationPayout);
    }

    [Fact]
    public void Salaried_overtime_is_paid_in_the_final_period_instead_of_deferred()
    {
        var employee = Salaried(52_000m, overtimeEligible: true, ended: Sunday.AddDays(6));
        var settings = Settings();

        var statement = PayStatement.Calculate(employee, settings.PeriodContaining(Sunday), settings, WorkWeek(employee, 9m), [], null);

        Assert.Equal(0m, statement.DeferredOvertimePay);
        Assert.Equal(187.50m, statement.OvertimePay);
    }
}
