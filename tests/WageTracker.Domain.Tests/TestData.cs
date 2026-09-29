using WageTracker.Domain.Employees;
using WageTracker.Domain.Payroll;
using WageTracker.Domain.TimeOff;
using WageTracker.Domain.TimeTracking;

namespace WageTracker.Domain.Tests;

internal static class TestData
{
    /// <summary>Sunday, September 6, 2026.</summary>
    public static readonly DateOnly Sunday = new(2026, 9, 6);

    public static Employee Hourly(decimal rate = 20m, EmploymentType type = EmploymentType.FullTime, int vacationDays = 10) =>
        new("Ada", "Lovelace", new DateOnly(1990, 12, 10), type, Compensation.Hourly(rate), 50m, vacationDays);

    public static Employee Salaried(decimal annual = 52_000m, bool overtimeEligible = false) =>
        new("Grace", "Hopper", new DateOnly(1985, 12, 9), EmploymentType.FullTime,
            Compensation.Salary(annual, overtimeEligible), 50m, 15);

    /// <summary>Weekly schedule, payout on the 10th, Holiday 8 h, Vacation 10 h, Sick 8 h.</summary>
    public static PayrollSettings Settings(PayPeriodSchedule? schedule = null) =>
        PayrollSettings.CreateDefault(schedule ?? PayPeriodSchedule.Weekly(), payoutDayOfMonth: 10);

    public static TimeOffType Type(this PayrollSettings settings, string name) =>
        settings.TimeOffTypes.Single(t => t.Name == name);

    public static TimeEntry Shift(Employee e, DateOnly day, decimal hours, int startHour = 8)
    {
        var start = day.ToDateTime(new TimeOnly(startHour, 0));
        return new TimeEntry(Guid.NewGuid(), e.Id, start, start.AddHours((double)hours));
    }

    /// <summary>Monday through Friday of the week starting <paramref name="sunday"/>.</summary>
    public static List<TimeEntry> WorkWeek(Employee e, decimal hoursPerDay, DateOnly? sunday = null) =>
        Enumerable.Range(1, 5).Select(i => Shift(e, (sunday ?? Sunday).AddDays(i), hoursPerDay)).ToList();

    public static CompensatedTimeOff DayOff(Employee e, DateOnly date, TimeOffType type) =>
        new(Guid.NewGuid(), e.Id, date, type.Id);
}
