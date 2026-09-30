using WageTracker.Application.Abstractions;
using WageTracker.Application.Common;
using WageTracker.Domain.Calendar;
using WageTracker.Domain.TimeTracking;

namespace WageTracker.Application.TimeTracking;

public sealed record TimeEntryDto(Guid Id, Guid EmployeeId, DateTime Start, DateTime End, decimal Hours)
{
    internal static TimeEntryDto From(TimeEntry e) => new(e.Id, e.EmployeeId, e.Start, e.End, e.Hours);
}

/// <param name="WeekStart">The Sunday the week starts on.</param>
public sealed record WeeklyHoursDto(Guid EmployeeId, DateOnly WeekStart, decimal Hours)
{
    internal static WeeklyHoursDto From(WeeklyHours w) => new(w.EmployeeId, w.Week.Start, w.Hours);
}

/// <summary>Hours worked, entered as time entries each day or as one total per week, depending on the employee.</summary>
public sealed class TimeEntryService(
    ITimeEntryRepository entries,
    IWeeklyHoursRepository weeklyTotals,
    IEmployeeRepository employees,
    IPayrollRunRepository runs,
    IClock clock)
{
    /// <summary>The employee's entries that overlap <paramref name="from"/> through <paramref name="to"/>, oldest first.</summary>
    public async Task<IReadOnlyList<TimeEntryDto>> ListAsync(Guid employeeId, DateOnly from, DateOnly to) =>
        (await entries.ListForEmployeeAsync(employeeId, from.ToDateTime(TimeOnly.MinValue), to.AddDays(1).ToDateTime(TimeOnly.MinValue)))
            .OrderBy(e => e.Start)
            .Select(TimeEntryDto.From)
            .ToList();

    /// <summary>
    /// Hours the employee worked from <paramref name="from"/> through <paramref name="to"/>. An entry that crosses either
    /// edge counts only its part inside. A weekly total counts when its week starts within the dates.
    /// </summary>
    public async Task<decimal> HoursWorkedAsync(Guid employeeId, DateOnly from, DateOnly to)
    {
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return (await entries.ListForEmployeeAsync(employeeId, start, end)).Sum(e => e.HoursWithin(start, end))
            + (await weeklyTotals.ListForEmployeeAsync(employeeId, from, to)).Sum(w => w.Hours);
    }

    /// <summary>The employee's total for the week starting <paramref name="weekStart"/>, or null if none was entered.</summary>
    public async Task<WeeklyHoursDto?> GetWeeklyHoursAsync(Guid employeeId, DateOnly weekStart) =>
        await weeklyTotals.GetAsync(employeeId, new WorkWeek(weekStart).Start) is { } total ? WeeklyHoursDto.From(total) : null;

    /// <summary>Enters the employee's total for the week starting <paramref name="weekStart"/>, replacing any earlier total.</summary>
    public async Task<WeeklyHoursDto> SetWeeklyHoursAsync(Guid employeeId, DateOnly weekStart, decimal hours)
    {
        var week = new WorkWeek(weekStart);
        var employee = await employees.RequireAsync(employeeId);
        await runs.EnsureCanChangeAsync(week.StartsAt, week.EndsAt);

        var entriesInWeek = await entries.ListForEmployeeAsync(employeeId, week.StartsAt, week.EndsAt);
        var total = WeeklyHours.Record(employee, week, hours, entriesInWeek, clock.Today);

        await weeklyTotals.SaveAsync(total);
        return WeeklyHoursDto.From(total);
    }

    public async Task ClearWeeklyHoursAsync(Guid employeeId, DateOnly weekStart)
    {
        var week = new WorkWeek(weekStart);
        await runs.EnsureCanChangeAsync(week.StartsAt, week.EndsAt);
        await weeklyTotals.RemoveAsync(employeeId, week.Start);
    }

    public async Task<TimeEntryDto> RecordAsync(Guid employeeId, DateTime start, DateTime end)
    {
        var employee = await employees.RequireAsync(employeeId);
        await runs.EnsureCanChangeAsync(start, end);

        var neighbors = await entries.ListForEmployeeAsync(employeeId, start, end);
        var entry = TimeEntry.Record(employee, start, end, neighbors, await WeeklyTotalsAroundAsync(employeeId, start, end), clock.Now);

        await entries.AddAsync(entry);
        return TimeEntryDto.From(entry);
    }

    public async Task<TimeEntryDto> RescheduleAsync(Guid id, DateTime start, DateTime end)
    {
        var entry = await RequireAsync(id);
        var employee = await employees.RequireAsync(entry.EmployeeId);
        await runs.EnsureCanChangeAsync(entry.Start, entry.End);
        await runs.EnsureCanChangeAsync(start, end);

        var neighbors = await entries.ListForEmployeeAsync(entry.EmployeeId, start, end);
        entry.Reschedule(employee, start, end, neighbors, await WeeklyTotalsAroundAsync(entry.EmployeeId, start, end), clock.Now);

        await entries.UpdateAsync(entry);
        return TimeEntryDto.From(entry);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entry = await RequireAsync(id);
        await runs.EnsureCanChangeAsync(entry.Start, entry.End);
        await entries.RemoveAsync(id);
    }

    /// <summary>The employee's weekly totals for the weeks that [start, end) touches.</summary>
    private async Task<IReadOnlyList<WeeklyHours>> WeeklyTotalsAroundAsync(Guid employeeId, DateTime start, DateTime end) =>
        end <= start
            ? []
            : await weeklyTotals.ListForEmployeeAsync(employeeId, WorkWeek.Containing(start).Start, WorkWeek.Containing(end.AddTicks(-1)).Start);

    private async Task<TimeEntry> RequireAsync(Guid id) =>
        await entries.GetAsync(id) ?? throw new NotFoundException("That time entry no longer exists.");
}
