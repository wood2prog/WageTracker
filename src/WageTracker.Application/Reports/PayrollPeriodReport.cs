using WageTracker.Application.Payroll;

namespace WageTracker.Application.Reports;

/// <summary>The payroll accountant's report, written again from a finalized period's stored statements.</summary>
public sealed class PayrollPeriodReport(PayrollService payroll) : IPeriodReport
{
    public string Name => "Payroll report";

    public bool RequiresFinalizedPeriod => true;

    public Task<string> CreateAsync(DateOnly date) => payroll.ExportReportAsync(date);
}
