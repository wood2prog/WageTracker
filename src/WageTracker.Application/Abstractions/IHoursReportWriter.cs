using WageTracker.Application.Payroll;

namespace WageTracker.Application.Abstractions;

/// <summary>Produces the hours summary: each employee's total compensated hours for a pay period. The format is up to Infrastructure.</summary>
public interface IHoursReportWriter
{
    /// <returns>Where the report was written, such as a file path, to show the user.</returns>
    Task<string> WriteAsync(PayrollReport report);
}
