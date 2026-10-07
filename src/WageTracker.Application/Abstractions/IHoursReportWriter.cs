using WageTracker.Application.Payroll;

namespace WageTracker.Application.Abstractions;

/// <summary>Produces the hours reports: each employee's compensated hours for a pay period. The format is up to Infrastructure.</summary>
public interface IHoursReportWriter
{
    /// <summary>Each employee's total compensated hours.</summary>
    /// <returns>Where the report was written, such as a file path, to show the user.</returns>
    Task<string> WriteAsync(PayrollReport report);

    /// <summary>Each employee's hourly rate and compensated hours split into regular and overtime, with the total.</summary>
    /// <returns>Where the report was written, such as a file path, to show the user.</returns>
    Task<string> WriteRegularAndOvertimeAsync(PayrollReport report);
}
