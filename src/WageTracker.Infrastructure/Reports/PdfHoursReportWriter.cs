using QuestPDF.Fluent;
using QuestPDF.Helpers;
using WageTracker.Application.Abstractions;
using WageTracker.Application.Payroll;
using static WageTracker.Infrastructure.Reports.PdfLayout;

namespace WageTracker.Infrastructure.Reports;

/// <summary>
/// Writes the hours reports as PDFs: one row per employee with their compensated hours for the period,
/// either as a single total or split into regular and overtime alongside the hourly rate.
/// </summary>
public sealed class PdfHoursReportWriter(StorageOptions options) : IHoursReportWriter
{
    public Task<string> WriteAsync(PayrollReport report) =>
        Task.FromResult(PdfLayout.Write(options, "Hours", report, "Hours Summary", content => HoursTable(content, report,
            ("Compensated hours", s => Hours(s.CompensatedHours)))));

    public Task<string> WriteRegularAndOvertimeAsync(PayrollReport report) =>
        Task.FromResult(PdfLayout.Write(options, "Regular and overtime hours", report, "Regular and Overtime Hours", content => HoursTable(content, report,
            ("Hourly rate", s => Money(s.HourlyRate)),
            ("Regular", s => Hours(s.CompensatedRegularHours)),
            ("Overtime", s => Hours(s.CompensatedOvertimeHours)),
            ("Total hours", s => Hours(s.CompensatedHours)))));

    /// <summary>
    /// A table with each employee's name and the given columns. Salaried employees show the hours their salary
    /// covers, not the hours recorded, and their equivalent hourly rate.
    /// </summary>
    private static void HoursTable(ColumnDescriptor content, PayrollReport report, params (string Title, Func<PayStatementDto, string> Value)[] columns) =>
        content.Item().PaddingTop(8).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(3);
                foreach (var _ in columns)
                    c.RelativeColumn(1);
            });
            table.Header(h =>
            {
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Employee").Bold();
                foreach (var column in columns)
                    h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text(column.Title).Bold();
            });
            foreach (var s in report.Run.Statements)
            {
                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(s.EmployeeName);
                foreach (var column in columns)
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight()
                        .Text(column.Value(s));
            }
        });
}
