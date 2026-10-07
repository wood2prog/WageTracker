using WageTracker.Application.Abstractions;
using WageTracker.Application.Payroll;

namespace WageTracker.Application.Reports;

/// <summary>Each employee's total compensated hours for the period. An open period gives a preview.</summary>
public sealed class HoursSummaryReport(PayrollService payroll, IHoursReportWriter writer) : IPeriodReport
{
    public string Name => "Hours summary";

    public bool RequiresFinalizedPeriod => false;

    public async Task<string> CreateAsync(DateOnly date) => await writer.WriteAsync(await payroll.GetReportAsync(date));
}
