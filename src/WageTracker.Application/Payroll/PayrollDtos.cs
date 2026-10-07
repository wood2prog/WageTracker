using WageTracker.Domain.Employees;
using WageTracker.Domain.Payroll;

namespace WageTracker.Application.Payroll;

/// <param name="CompensatedRegularHours">The hours the base pay covers; for salary, the hours the week's salary covers.</param>
/// <param name="CompensatedOvertimeHours">Overtime hours that earn overtime pay, whenever it is paid.</param>
public sealed record WeekDto(
    DateOnly Start, DateOnly End, decimal WorkedHours, decimal TimeOffHours, decimal RegularHours, decimal OvertimeHours,
    decimal CompensatedRegularHours, decimal CompensatedOvertimeHours)
{
    /// <summary>All hours paid for the week: for salary, the hours the salary covers rather than the hours recorded.</summary>
    public decimal TotalHours => CompensatedRegularHours + CompensatedOvertimeHours;

    internal static WeekDto From(WeekSummary w, PayStatement s) =>
        new(w.Week.Start, w.Week.End, w.WorkedHours, w.TimeOffHours, w.RegularHours, w.OvertimeHours,
            w.CompensatedRegularHours, s.CompensatedOvertimeHoursIn(w));
}

/// <param name="OvertimePay">Overtime paid in this period, including salaried overtime carried from the previous one.</param>
/// <param name="DeferredOvertimePay">Salaried overtime earned in this period and paid in the next.</param>
/// <param name="CompensatedRegularHours">The hours the base pay covers; for salary, the equivalent at the hourly rate.</param>
/// <param name="CompensatedOvertimeHours">Overtime hours that earn overtime pay, whenever it is paid.</param>
public sealed record PayStatementDto(
    Guid EmployeeId,
    string EmployeeName,
    CompensationType CompensationType,
    decimal HourlyRate,
    decimal OvertimeMultiplier,
    decimal WorkedHours,
    decimal TimeOffHours,
    decimal OvertimeHours,
    decimal CompensatedRegularHours,
    decimal CompensatedOvertimeHours,
    decimal BasePay,
    decimal OvertimePay,
    decimal DeferredOvertimePay,
    int VacationDaysPaidOut,
    decimal VacationPayout,
    decimal GrossPay,
    IReadOnlyList<WeekDto> Weeks)
{
    /// <summary>All hours paid for: for salary, the hours the salary covers rather than the hours recorded.</summary>
    public decimal CompensatedHours => CompensatedRegularHours + CompensatedOvertimeHours;

    internal static PayStatementDto From(PayStatement s, string employeeName) => new(
        s.EmployeeId,
        employeeName,
        s.CompensationType,
        s.HourlyRate,
        s.OvertimeMultiplier,
        s.WorkedHours,
        s.TimeOffHours,
        s.OvertimeHours,
        s.CompensatedRegularHours,
        s.CompensatedOvertimeHours,
        s.BasePay,
        s.OvertimePay,
        s.DeferredOvertimePay,
        s.VacationDaysPaidOut,
        s.VacationPayout,
        s.GrossPay,
        s.Weeks.Select(w => WeekDto.From(w, s)).ToList());
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

/// <summary>What goes into the payroll accountant's report.</summary>
/// <param name="CompanyLogo">A PNG or JPEG image, or null.</param>
public sealed record PayrollReport(PayrollRunDto Run, string? CompanyName, byte[]? CompanyLogo);

/// <param name="ReportLocation">Where the payroll accountant's report was written.</param>
public sealed record FinalizeResult(PayrollRunDto Run, string ReportLocation);
