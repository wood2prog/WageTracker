using WageTracker.Application.Abstractions;
using WageTracker.Application.Payroll;

namespace WageTracker.Application.Reports;

/// <summary>Each employee's compensated hours for the period, split into regular and overtime. An open period gives a preview.</summary>
public sealed class RegularAndOvertimeHoursReport(PayrollService payroll, IHoursReportWriter writer) : IPeriodReport
{
    public string Name => "Regular and overtime hours";

    public bool RequiresFinalizedPeriod => false;

    public async Task<string> CreateAsync(DateOnly date) => await writer.WriteRegularAndOvertimeAsync(await payroll.GetReportAsync(date));
}
