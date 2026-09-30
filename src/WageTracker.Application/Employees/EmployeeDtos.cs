using WageTracker.Domain.Employees;
using WageTracker.Domain.TimeOff;

namespace WageTracker.Application.Employees;

/// <param name="EndDate">The last day worked, or null while still employed.</param>
/// <param name="CompensationAmount">The hourly rate, or the annual salary.</param>
/// <param name="SalariedOvertimeEligible">Ignored for hourly employees, who always earn overtime.</param>
/// <param name="TimeRecording">Whether hours are entered each day or as one total per week.</param>
public sealed record EmployeeInput(
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    DateOnly HireDate,
    DateOnly? EndDate,
    EmploymentType EmploymentType,
    CompensationType CompensationType,
    decimal CompensationAmount,
    bool SalariedOvertimeEligible,
    decimal OvertimePercentage,
    int VacationDaysPermitted,
    TimeRecording TimeRecording = TimeRecording.Daily)
{
    internal Compensation ToCompensation() => CompensationType == CompensationType.Salary
        ? Compensation.Salary(CompensationAmount, SalariedOvertimeEligible)
        : Compensation.Hourly(CompensationAmount);
}

/// <param name="TimeOffHoursOverrides">The employee's own hours per day, keyed by time-off type ID.</param>
public sealed record EmployeeDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    DateOnly BirthDate,
    DateOnly HireDate,
    DateOnly? EndDate,
    EmploymentType EmploymentType,
    CompensationType CompensationType,
    decimal CompensationAmount,
    bool OvertimeEligible,
    decimal OvertimePercentage,
    int VacationDaysPermitted,
    TimeRecording TimeRecording,
    IReadOnlyDictionary<Guid, decimal> TimeOffHoursOverrides)
{
    internal static EmployeeDto From(Employee e) => new(
        e.Id,
        e.FirstName,
        e.LastName,
        e.FullName,
        e.BirthDate,
        e.HireDate,
        e.EndDate,
        e.EmploymentType,
        e.Compensation.Type,
        e.Compensation.Amount,
        e.Compensation.OvertimeEligible,
        e.OvertimePercentage,
        e.VacationDaysPermitted,
        e.TimeRecording,
        new Dictionary<Guid, decimal>(e.TimeOffHours.Entries));
}

public sealed record VacationBalanceDto(Guid EmployeeId, int Year, int Permitted, int Used, int Remaining)
{
    internal static VacationBalanceDto From(Guid employeeId, VacationBalance b) =>
        new(employeeId, b.Year, b.Permitted, b.Used, b.Remaining);
}
