using WageTracker.Domain.Employees;
using WageTracker.Domain.Payroll;

namespace WageTracker.Application.Payroll;

public sealed record WeekDto(DateOnly Start, DateOnly End, decimal WorkedHours, decimal TimeOffHours, decimal RegularHours, decimal OvertimeHours)
{
    internal static WeekDto From(WeekSummary w) =>
        new(w.Week.Start, w.Week.End, w.WorkedHours, w.TimeOffHours, w.RegularHours, w.OvertimeHours);
}

/// <param name="OvertimePay">Overtime paid in this period, including salaried overtime carried from the previous one.</param>
/// <param name="DeferredOvertimePay">Salaried overtime earned in this period and paid in the next.</param>
public sealed record PayStatementDto(
    Guid EmployeeId,
    string EmployeeName,
    CompensationType CompensationType,
    decimal HourlyRate,
    decimal OvertimeMultiplier,
    decimal WorkedHours,
    decimal TimeOffHours,
    decimal OvertimeHours,
    decimal BasePay,
    decimal OvertimePay,
    decimal DeferredOvertimePay,
    int VacationDaysPaidOut,
    decimal VacationPayout,
    decimal GrossPay,
    IReadOnlyList<WeekDto> Weeks)
{
    internal static PayStatementDto From(PayStatement s, string employeeName) => new(
        s.EmployeeId,
        employeeName,
        s.CompensationType,
        s.HourlyRate,
        s.OvertimeMultiplier,
        s.WorkedHours,
        s.TimeOffHours,
        s.OvertimeHours,
        s.BasePay,
        s.OvertimePay,
        s.DeferredOvertimePay,
        s.VacationDaysPaidOut,
        s.VacationPayout,
        s.GrossPay,
        s.Weeks.Select(WeekDto.From).ToList());
}

/// <summary>A pay period's payroll. While it is open, the statements are a live preview; once locked, they are the stored snapshot.</summary>
public sealed record PayrollRunDto(
    DateOnly Start,
    DateOnly End,
    int WeekCount,
    DateOnly PayoutDate,
    bool IsLocked,
    DateTime? LockedAt,
    IReadOnlyList<PayStatementDto> Statements)
{
    public decimal TotalGrossPay => Statements.Sum(s => s.GrossPay);
}

/// <param name="ReportLocation">Where the payroll accountant's report was written.</param>
public sealed record FinalizeResult(PayrollRunDto Run, string ReportLocation);
