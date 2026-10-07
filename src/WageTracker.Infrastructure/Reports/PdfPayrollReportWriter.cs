using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WageTracker.Application.Abstractions;
using WageTracker.Application.Payroll;
using WageTracker.Domain.Employees;
using static WageTracker.Infrastructure.Reports.PdfLayout;

namespace WageTracker.Infrastructure.Reports;

/// <summary>
/// Writes the payroll accountant's report as a PDF: for each employee, the pay lines and a week-by-week
/// table of hours with a total row, followed by totals for the period.
/// </summary>
public sealed class PdfPayrollReportWriter(StorageOptions options) : IPayrollReportWriter
{
    public Task<string> WriteAsync(PayrollReport report) =>
        Task.FromResult(PdfLayout.Write(options, "Payroll", report, "Payroll Report", content =>
        {
            content.Spacing(18);
            foreach (var statement in report.Run.Statements)
                content.Item().ShowEntire().Element(e => EmployeeSection(e, statement));
            content.Item().ShowEntire().Element(e => Totals(e, report.Run));
        }));

    private static void EmployeeSection(IContainer container, PayStatementDto s) => container.Column(section =>
    {
        section.Spacing(6);
        section.Item().BorderBottom(1).PaddingBottom(2).Row(row =>
        {
            row.RelativeItem().Text(s.EmployeeName).FontSize(13).Bold();
            row.AutoItem().AlignBottom().Text(s.CompensationType == CompensationType.Hourly
                ? $"Hourly, {Money(s.HourlyRate)}/h"
                : $"Salary ({Money(s.HourlyRate)}/h equivalent)");
        });

        section.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(3);
                c.RelativeColumn(1);
            });
            PayLine(table, "Base pay", s.BasePay);
            PayLine(table, $"Overtime pay (at {s.OvertimeMultiplier.ToString("0.##", Culture)}×)", s.OvertimePay);
            if (s.VacationDaysPaidOut > 0)
                PayLine(table, $"Unused vacation paid out ({s.VacationDaysPaidOut} {(s.VacationDaysPaidOut == 1 ? "day" : "days")})", s.VacationPayout);
            table.Cell().BorderTop(0.5f).PaddingTop(2).Text("Gross pay").Bold();
            table.Cell().BorderTop(0.5f).PaddingTop(2).AlignRight().Text(Money(s.GrossPay)).Bold();
        });

        if (s.DeferredOvertimePay > 0)
            section.Item().Text($"Overtime earned this period, paid next period: {Money(s.DeferredOvertimePay)}").Italic();

        section.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(3);
                for (var i = 0; i < 5; i++)
                    c.RelativeColumn(1);
            });
            table.Header(h =>
            {
                foreach (var title in new[] { "Week", "Worked", "Time off", "Regular", "Overtime", "Total hours" })
                    h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Element(c => title == "Week" ? c : c.AlignRight()).Text(title).Bold();
            });
            foreach (var w in s.Weeks)
            {
                table.Cell().Padding(3).Text($"{Date(w.Start)} – {Date(w.End)}");
                HoursCell(table, w.WorkedHours);
                HoursCell(table, w.TimeOffHours);
                HoursCell(table, w.RegularHours);
                HoursCell(table, w.OvertimeHours);
                HoursCell(table, w.TotalHours);
            }

            table.Cell().BorderTop(0.5f).Padding(3).Text("Total").Bold();
            TotalCell(table, s.Weeks.Sum(w => w.WorkedHours));
            TotalCell(table, s.Weeks.Sum(w => w.TimeOffHours));
            TotalCell(table, s.Weeks.Sum(w => w.RegularHours));
            TotalCell(table, s.Weeks.Sum(w => w.OvertimeHours));
            TotalCell(table, s.Weeks.Sum(w => w.TotalHours));
        });
    });

    private static void Totals(IContainer container, PayrollRunDto run) => container.Column(totals =>
    {
        totals.Spacing(4);
        totals.Item().BorderBottom(1).PaddingBottom(2).Text("Totals").FontSize(13).Bold();
        totals.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(3);
                c.RelativeColumn(1);
            });
            Line(table, "Employees paid", run.Statements.Count.ToString(Culture));
            Line(table, "Hours worked", Hours(run.Statements.Sum(s => s.WorkedHours)));
            Line(table, "Time-off hours", Hours(run.Statements.Sum(s => s.TimeOffHours)));
            Line(table, "Overtime hours", Hours(run.Statements.Sum(s => s.OvertimeHours)));
            Line(table, "Overtime deferred to next period", Money(run.Statements.Sum(s => s.DeferredOvertimePay)));
            table.Cell().BorderTop(0.5f).PaddingTop(2).Text("Total gross pay").Bold();
            table.Cell().BorderTop(0.5f).PaddingTop(2).AlignRight().Text(Money(run.TotalGrossPay)).Bold();
        });
    });

    private static void PayLine(TableDescriptor table, string label, decimal amount) => Line(table, label, Money(amount));

    private static void Line(TableDescriptor table, string label, string value)
    {
        table.Cell().Text(label);
        table.Cell().AlignRight().Text(value);
    }

    private static void HoursCell(TableDescriptor table, decimal hours) =>
        table.Cell().Padding(3).AlignRight().Text(Hours(hours));

    private static void TotalCell(TableDescriptor table, decimal hours) =>
        table.Cell().BorderTop(0.5f).Padding(3).AlignRight().Text(Hours(hours)).Bold();
}
