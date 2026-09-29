using WageTracker.Application.Abstractions;
using WageTracker.Application.Common;
using WageTracker.Domain.Employees;

namespace WageTracker.Application.Employees;

public sealed class EmployeeService(
    IEmployeeRepository employees,
    ITimeOffRepository timeOff,
    IPayrollSettingsRepository settings)
{
    public async Task<IReadOnlyList<EmployeeDto>> ListAsync() =>
        (await employees.ListAsync())
            .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
            .Select(EmployeeDto.From)
            .ToList();

    public async Task<EmployeeDto> GetAsync(Guid id) => EmployeeDto.From(await employees.RequireAsync(id));

    public async Task<EmployeeDto> CreateAsync(EmployeeInput input)
    {
        var employee = new Employee(
            Guid.NewGuid(),
            input.FirstName,
            input.LastName,
            input.BirthDate,
            input.HireDate,
            input.EndDate,
            input.EmploymentType,
            input.ToCompensation(),
            input.OvertimePercentage,
            input.VacationDaysPermitted);
        await employees.AddAsync(employee);
        return EmployeeDto.From(employee);
    }

    public async Task<EmployeeDto> UpdateAsync(Guid id, EmployeeInput input)
    {
        var employee = await employees.RequireAsync(id);
        employee.Rename(input.FirstName, input.LastName);
        employee.ChangeBirthDate(input.BirthDate);
        employee.ChangeEmploymentDates(input.HireDate, input.EndDate);
        employee.ChangeEmploymentType(input.EmploymentType);
        employee.ChangeCompensation(input.ToCompensation());
        employee.SetOvertimePercentage(input.OvertimePercentage);
        employee.SetVacationDaysPermitted(input.VacationDaysPermitted);
        await employees.UpdateAsync(employee);
        return EmployeeDto.From(employee);
    }

    /// <summary>Sets the employee's own hours for a time-off type, or clears it when <paramref name="hoursPerDay"/> is null.</summary>
    public async Task<EmployeeDto> SetTimeOffHoursAsync(Guid employeeId, Guid timeOffTypeId, decimal? hoursPerDay)
    {
        var employee = await employees.RequireAsync(employeeId);
        var type = (await settings.RequireAsync()).GetTimeOffType(timeOffTypeId);

        if (hoursPerDay is { } hours)
            employee.TimeOffHours.Set(type.Id, hours);
        else
            employee.TimeOffHours.Clear(type.Id);

        await employees.UpdateAsync(employee);
        return EmployeeDto.From(employee);
    }

    public async Task<VacationBalanceDto> GetVacationBalanceAsync(Guid employeeId, int year)
    {
        var employee = await employees.RequireAsync(employeeId);
        var vacationType = (await settings.RequireAsync()).VacationType;
        var days = await timeOff.ListForEmployeeAsync(employeeId, new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
        return VacationBalanceDto.From(employeeId, employee.VacationBalance(year, vacationType, days));
    }
}
