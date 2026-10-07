using QuestPDF.Fluent;
using QuestPDF.Helpers;
using WageTracker.Application.Abstractions;
using WageTracker.Application.Payroll;
using static WageTracker.Infrastructure.Reports.PdfLayout;

namespace WageTracker.Infrastructure.Reports;

/// <summary>Writes the hours summary as a PDF: each employee's total compensated hours for the period.</summary>
public sealed class PdfHoursReportWriter(StorageOptions options) : IHoursReportWriter
{
    public Task<string> WriteAsync(PayrollReport report) =>
        Task.FromResult(PdfLayout.Write(options, "Hours", report, "Hours Summary", content =>
        {
            content.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(1);
                });
                table.Header(h =>
                {
                    h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Employee").Bold();
                    h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Compensated hours").Bold();
                });
                foreach (var s in report.Run.Statements)
                {
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(s.EmployeeName);
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(Hours(s.Weeks.Sum(w => w.TotalHours)));
                }
            });
        }));
}
