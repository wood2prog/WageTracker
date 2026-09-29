using WageTracker.Application.Abstractions;
using WageTracker.Application.Common;
using WageTracker.Domain.TimeTracking;

namespace WageTracker.Application.TimeTracking;

public sealed record TimeEntryDto(Guid Id, Guid EmployeeId, DateTime Start, DateTime End, decimal Hours)
{
    internal static TimeEntryDto From(TimeEntry e) => new(e.Id, e.EmployeeId, e.Start, e.End, e.Hours);
}

public sealed class TimeEntryService(
    ITimeEntryRepository entries,
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
    /// edge counts only its part inside.
    /// </summary>
    public async Task<decimal> HoursWorkedAsync(Guid employeeId, DateOnly from, DateOnly to)
    {
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return (await entries.ListForEmployeeAsync(employeeId, start, end)).Sum(e => e.HoursWithin(start, end));
    }

    public async Task<TimeEntryDto> RecordAsync(Guid employeeId, DateTime start, DateTime end)
    {
        var employee = await employees.RequireAsync(employeeId);
        await runs.EnsureCanChangeAsync(start, end);

        var neighbors = await entries.ListForEmployeeAsync(employeeId, start, end);
        var entry = TimeEntry.Record(employee, start, end, neighbors, clock.Now);

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
        entry.Reschedule(employee, start, end, neighbors, clock.Now);

        await entries.UpdateAsync(entry);
        return TimeEntryDto.From(entry);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entry = await RequireAsync(id);
        await runs.EnsureCanChangeAsync(entry.Start, entry.End);
        await entries.RemoveAsync(id);
    }

    private async Task<TimeEntry> RequireAsync(Guid id) =>
        await entries.GetAsync(id) ?? throw new NotFoundException("That time entry no longer exists.");
}
