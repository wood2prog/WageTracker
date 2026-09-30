using WageTracker.Domain.Calendar;
using WageTracker.Domain.Common;
using WageTracker.Domain.Employees;

namespace WageTracker.Domain.TimeTracking;

/// <summary>
/// The total hours an employee worked in one work week, for employees whose hours are entered weekly
/// (<see cref="TimeRecording.Weekly"/>). An employee has at most one per week, and a week with a total can't
/// also have time entries. New totals are created with <see cref="Record"/>, which enforces the rules; the
/// constructor is for loading saved totals.
/// </summary>
public sealed class WeeklyHours
{
    public WeeklyHours(Guid employeeId, WorkWeek week, decimal hours)
    {
        if (hours <= 0)
            throw new DomainException("Weekly hours must be more than zero. Clear the week to remove its hours.");
        if (!Rounding.HasAtMostTwoPlaces(hours))
            throw new DomainException("Weekly hours can have at most two decimal places.");
        EmployeeId = employeeId;
        Week = week;
        Hours = hours;
    }

    public Guid EmployeeId { get; }

    public WorkWeek Week { get; }

    public decimal Hours { get; }

    /// <summary>
    /// Creates the week's total, replacing any earlier total for the same week. The employee's hours must be
    /// entered weekly, and the week must have started. The hours can be at most 24 for each day of the week
    /// the employee was employed, and the week must not already have time entries.
    /// </summary>
    /// <param name="entriesInWeek">The employee's time entries that overlap the week.</param>
    public static WeeklyHours Record(
        Employee employee, WorkWeek week, decimal hours, IEnumerable<TimeEntry> entriesInWeek, DateOnly today)
    {
        var total = new WeeklyHours(employee.Id, week, hours);

        if (employee.TimeRecording != TimeRecording.Weekly)
            throw new DomainException($"{employee.FullName}'s hours are entered by the day, not the week.");
        if (week.Start > today)
            throw new DomainException("Hours cannot be entered for a week that hasn't started.");

        var daysEmployed = Enumerable.Range(0, 7).Count(i => employee.IsEmployedOn(week.Start.AddDays(i)));
        if (daysEmployed == 0)
            throw new DomainException($"{employee.FullName} is not employed during the week of {week}.");
        if (hours > 24m * daysEmployed)
            throw new DomainException(
                $"{employee.FullName} was employed {daysEmployed} day(s) that week, so at most {24 * daysEmployed} hours can be entered.");

        if (entriesInWeek.Any(e => e.EmployeeId == employee.Id && e.Start < week.EndsAt && week.StartsAt < e.End))
            throw new DomainException(
                $"The week of {week} already has time entries. Delete them before entering a weekly total.");

        return total;
    }
}
