using WageTracker.Application.Payroll;

namespace WageTracker.Application.Abstractions;

/// <summary>Produces the report for the payroll accountant. The format is up to Infrastructure.</summary>
public interface IPayrollReportWriter
{
    /// <returns>Where the report was written, such as a file path, to show the user.</returns>
    Task<string> WriteAsync(PayrollReport report);
}
