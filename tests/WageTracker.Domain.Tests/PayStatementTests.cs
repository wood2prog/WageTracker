using WageTracker.Domain.Common;
using WageTracker.Domain.Employees;
using WageTracker.Domain.Payroll;
using WageTracker.Domain.TimeOff;
using WageTracker.Domain.TimeTracking;
using static WageTracker.Domain.Tests.TestData;

namespace WageTracker.Domain.Tests;

public class PayStatementTests
{
    private static PayStatement Calculate(
        Employee employee,
        PayrollSettings settings,
        IEnumerable<TimeEntry>? entries = null,
        IEnumerable<CompensatedTimeOff>? timeOff = null,
        DateOnly? date = null,
        PayStatement? previous = null) =>
        PayStatement.Calculate(employee, settings.Schedule.PeriodContaining(date ?? Sunday), settings,
            entries ?? [], timeOff ?? [], previous);

    [Fact]
    public void Hours_over_the_threshold_are_paid_at_the_overtime_rate()
    {
        var employee = Hourly();

        var statement = Calculate(employee, Settings(), WorkWeek(employee, 9m));

        Assert.Equal(45m, statement.WorkedHours);
        Assert.Equal(5m, statement.OvertimeHours);
        Assert.Equal(800m, statement.BasePay);
        Assert.Equal(150m, statement.OvertimePay);
        Assert.Equal(950m, statement.GrossPay);
    }

    [Fact]
    public void Time_off_counts_toward_overtime_by_default()
    {
        var employee = Hourly();
        var settings = Settings();
        var sick = DayOff(employee, Sunday.AddDays(6), settings.Type("Sick"));

        var statement = Calculate(employee, settings, WorkWeek(employee, 8m), [sick]);

        Assert.Equal(8m, statement.OvertimeHours);
        Assert.Equal(40m * 20m + 8m * 30m, statement.GrossPay);
    }

    [Fact]
    public void Hours_over_the_threshold_are_overtime_even_when_they_all_come_from_time_off()
    {
        var employee = Hourly();
        var settings = Settings();
        var vacation = Enumerable.Range(1, 5).Select(i => DayOff(employee, Sunday.AddDays(i), settings.VacationType));

        var statement = Calculate(employee, settings, timeOff: vacation);

        Assert.Equal(50m, statement.TimeOffHours);
        Assert.Equal(10m, statement.OvertimeHours);
    }

    [Fact]
    public void Time_off_can_be_excluded_from_overtime()
    {
        var employee = Hourly();
        var settings = Settings();
        settings.TimeOffCountsTowardOvertime = false;
        var sick = DayOff(employee, Sunday.AddDays(6), settings.Type("Sick"));

        var statement = Calculate(employee, settings, WorkWeek(employee, 8m), [sick]);

        Assert.Equal(0m, statement.OvertimeHours);
        Assert.Equal(48m * 20m, statement.GrossPay);
    }

    [Fact]
    public void The_overtime_threshold_can_be_changed()
    {
        var employee = Hourly();
        var settings = Settings();
        settings.SetOvertimeThresholdHours(37.5m);

        var statement = Calculate(employee, settings, WorkWeek(employee, 8m));

        Assert.Equal(2.5m, statement.OvertimeHours);
    }

    [Fact]
    public void Employee_time_off_hours_override_the_type_default()
    {
        var employee = Hourly();
        var settings = Settings();
        employee.TimeOffHours.Set(settings.Type("Holiday").Id, 6m);

        Assert.Equal(6m, employee.ResolveTimeOffHours(settings.Type("Holiday")));
        Assert.Equal(10m, employee.ResolveTimeOffHours(settings.VacationType));
    }

    [Fact]
    public void An_entry_crossing_Saturday_midnight_is_split_between_weeks()
    {
        var employee = Hourly();
        var settings = Settings(PayPeriodSchedule.BiWeekly(Sunday));
        var saturdayNight = new TimeEntry(Guid.NewGuid(), employee.Id, new DateTime(2026, 9, 12, 22, 0, 0), new DateTime(2026, 9, 13, 3, 0, 0));

        var statement = Calculate(employee, settings, [saturdayNight]);

        Assert.Equal([2m, 3m], statement.Weeks.Select(w => w.WorkedHours));
    }

    [Fact]
    public void Overtime_is_figured_per_week_not_per_period()
    {
        var employee = Hourly();
        var settings = Settings(PayPeriodSchedule.BiWeekly(Sunday));

        var statement = Calculate(employee, settings, WorkWeek(employee, 10m));

        Assert.Equal(50m, statement.WorkedHours);
        Assert.Equal(10m, statement.OvertimeHours);
    }

    [Fact]
    public void Time_entry_hours_are_rounded_to_two_places()
    {
        var entry = new TimeEntry(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 9, 7, 9, 0, 0), new DateTime(2026, 9, 7, 9, 20, 0));

        Assert.Equal(0.33m, entry.Hours);
    }

    [Fact]
    public void Salaried_pay_is_a_52nd_of_annual_salary_per_week()
    {
        var settings = Settings(PayPeriodSchedule.Monthly());

        var statement = Calculate(Salaried(), settings, date: new DateOnly(2026, 9, 15));

        Assert.Equal(4, statement.Period.WeekCount);
        Assert.Equal(4000m, statement.GrossPay);
    }

    [Fact]
    public void Salaried_employees_without_overtime_eligibility_earn_no_overtime()
    {
        var employee = Salaried(overtimeEligible: false);

        var statement = Calculate(employee, Settings(), WorkWeek(employee, 10m));

        Assert.Equal(10m, statement.OvertimeHours);
        Assert.Equal(0m, statement.DeferredOvertimePay);
        Assert.Equal(1000m, statement.GrossPay);
    }

    [Fact]
    public void Salaried_employees_are_compensated_for_the_threshold_hours_each_week_without_time_entries()
    {
        var statement = Calculate(Salaried(), Settings(PayPeriodSchedule.Monthly()), date: new DateOnly(2026, 9, 15));

        Assert.Equal(0m, statement.WorkedHours);
        Assert.Equal(160m, statement.CompensatedRegularHours);
        Assert.Equal(0m, statement.CompensatedOvertimeHours);
        Assert.Equal(160m, statement.CompensatedHours);
    }

    [Fact]
    public void A_salaried_partial_week_is_compensated_a_fifth_of_the_threshold_per_weekday_employed()
    {
        // Hired Wednesday: Wednesday, Thursday, and Friday are paid.
        var statement = Calculate(Salaried(hired: Sunday.AddDays(3)), Settings());

        Assert.Equal(24m, statement.CompensatedRegularHours);
    }

    [Fact]
    public void Salaried_overtime_is_compensated_only_when_eligible()
    {
        var eligible = Salaried(overtimeEligible: true);
        var ineligible = Salaried(overtimeEligible: false);

        var paid = Calculate(eligible, Settings(), WorkWeek(eligible, 9m));
        var unpaid = Calculate(ineligible, Settings(), WorkWeek(ineligible, 9m));

        Assert.Equal((40m, 5m, 45m), (paid.CompensatedRegularHours, paid.CompensatedOvertimeHours, paid.CompensatedHours));
        Assert.Equal((40m, 0m, 40m), (unpaid.CompensatedRegularHours, unpaid.CompensatedOvertimeHours, unpaid.CompensatedHours));
    }

    [Fact]
    public void Hourly_compensated_hours_are_the_regular_and_overtime_hours()
    {
        var employee = Hourly();

        var statement = Calculate(employee, Settings(), WorkWeek(employee, 9m));

        Assert.Equal((40m, 5m, 45m), (statement.CompensatedRegularHours, statement.CompensatedOvertimeHours, statement.CompensatedHours));
    }

    [Fact]
    public void Salaried_overtime_is_paid_in_the_next_period()
    {
        var employee = Salaried(52_000m, overtimeEligible: true);
        var settings = Settings();

        var first = Calculate(employee, settings, WorkWeek(employee, 9m));
        var second = Calculate(employee, settings, date: Sunday.AddDays(7), previous: first);

        // Weekly salary 1000, over a 40-hour threshold = 25/h; overtime at 1.5x = 37.50/h; 5 h = 187.50.
        Assert.Equal(25m, first.HourlyRate);
        Assert.Equal(187.50m, first.DeferredOvertimePay);
        Assert.Equal(0m, first.OvertimePay);
        Assert.Equal(1000m, first.GrossPay);
        Assert.Equal(187.50m, second.OvertimePay);
        Assert.Equal(1187.50m, second.GrossPay);
    }

    [Fact]
    public void Previous_statement_must_be_for_the_period_just_before()
    {
        var employee = Salaried(overtimeEligible: true);
        var settings = Settings();
        var first = Calculate(employee, settings);

        Assert.Throws<DomainException>(() => Calculate(employee, settings, date: Sunday.AddDays(14), previous: first));
    }

    [Fact]
    public void Unused_vacation_is_paid_out_in_the_last_period_of_the_year()
    {
        var employee = Hourly(20m, vacationDays: 10);
        var settings = Settings();
        var used = new[] { 3, 4, 5 }.Select(m => DayOff(employee, new DateOnly(2026, m, 10), settings.VacationType));
        var lastWeek = new DateOnly(2026, 12, 31);

        var statement = Calculate(employee, settings, timeOff: used, date: lastWeek);

        Assert.Equal(2026, statement.Period.ClosesYear);
        Assert.Equal(7, statement.VacationDaysPaidOut);
        Assert.Equal(7m * 10m * 20m, statement.VacationPayout);
    }

    [Fact]
    public void Unused_vacation_is_not_paid_out_before_the_end_of_the_year()
    {
        var statement = Calculate(Hourly(), Settings(), date: new DateOnly(2026, 12, 20));

        Assert.Equal(0, statement.VacationDaysPaidOut);
    }

    [Fact]
    public void Part_time_employees_get_no_vacation_payout()
    {
        var employee = Hourly(type: EmploymentType.PartTime);

        var statement = Calculate(employee, Settings(), date: new DateOnly(2026, 12, 31));

        Assert.Equal(0m, statement.VacationPayout);
    }

    [Fact]
    public void Full_time_employees_are_credited_for_company_holidays()
    {
        var employee = Hourly();
        var settings = Settings();
        settings.Holidays.Add("Labor Day", HolidayRule.NthWeekday(9, DayOfWeek.Monday, 1));
        employee.TimeOffHours.Set(settings.HolidayType.Id, 6m);

        var statement = Calculate(employee, settings, WorkWeek(employee, 8m));

        Assert.Equal(6m, statement.TimeOffHours);
        Assert.Equal(6m, statement.OvertimeHours);
    }

    [Fact]
    public void Part_time_employees_get_no_holiday_hours()
    {
        var employee = Hourly(type: EmploymentType.PartTime);
        var settings = Settings();
        settings.Holidays.Add("Labor Day", HolidayRule.NthWeekday(9, DayOfWeek.Monday, 1));

        var statement = Calculate(employee, settings);

        Assert.Equal(0m, statement.TimeOffHours);
    }

    [Fact]
    public void Entries_for_another_employee_are_rejected() =>
        Assert.Throws<DomainException>(() => Calculate(Hourly(), Settings(), WorkWeek(Hourly(), 8m)));
}
