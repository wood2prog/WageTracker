using WageTracker.Application.Abstractions;
using WageTracker.Application.Common;
using WageTracker.Domain.Employees;
using WageTracker.Domain.Payroll;
using WageTracker.Domain.TimeOff;

namespace WageTracker.Application.TimeOff;

/// <param name="Hours">What the day is worth for this employee under the current settings.</param>
public sealed record TimeOffDto(Guid Id, Guid EmployeeId, DateOnly Date, Guid TimeOffTypeId, string TypeName, decimal Hours)
{
    internal static TimeOffDto From(CompensatedTimeOff t, Employee employee, PayrollSettings settings)
    {
        var type = settings.GetTimeOffType(t.TimeOffTypeId);
        return new(t.Id, t.EmployeeId, t.Date, type.Id, type.Name, employee.ResolveTimeOffHours(type));
    }
}

public sealed class TimeOffService(
    ITimeOffRepository timeOff,
    IEmployeeRepository employees,
    IPayrollSettingsRepository settings,
    IPayrollRunRepository runs)
{
    /// <summary>The employee's booked days off from <paramref name="from"/> through <paramref name="to"/>. Company holidays are listed by <see cref="Settings.HolidayService"/>.</summary>
    public async Task<IReadOnlyList<TimeOffDto>> ListAsync(Guid employeeId, DateOnly from, DateOnly to)
    {
        var employee = await employees.RequireAsync(employeeId);
        var current = await settings.RequireAsync();
        return (await timeOff.ListForEmployeeAsync(employeeId, from, to))
            .OrderBy(t => t.Date)
            .Select(t => TimeOffDto.From(t, employee, current))
            .ToList();
    }

    public async Task<TimeOffDto> BookAsync(Guid employeeId, DateOnly date, Guid timeOffTypeId)
    {
        var employee = await employees.RequireAsync(employeeId);
        var current = await settings.RequireAsync();
        await runs.EnsureCanChangeAsync(date);

        var sameYear = await timeOff.ListForEmployeeAsync(employeeId, new DateOnly(date.Year, 1, 1), new DateOnly(date.Year, 12, 31));
        var booked = employee.BookTimeOff(date, current.GetTimeOffType(timeOffTypeId), sameYear, current.Holidays);

        await timeOff.AddAsync(booked);
        return TimeOffDto.From(booked, employee, current);
    }

    public async Task CancelAsync(Guid id)
    {
        var day = await timeOff.GetAsync(id) ?? throw new NotFoundException("That day off no longer exists.");
        await runs.EnsureCanChangeAsync(day.Date);
        await timeOff.RemoveAsync(id);
    }
}
