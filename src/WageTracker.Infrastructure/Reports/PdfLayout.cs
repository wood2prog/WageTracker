using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WageTracker.Application.Payroll;

namespace WageTracker.Infrastructure.Reports;

/// <summary>
/// The page every pay period report shares: a header with the company, the title, and the period, and page
/// numbers at the foot. Also the number and date formats the reports use.
/// </summary>
internal static class PdfLayout
{
    static PdfLayout() => QuestPDF.Settings.License = LicenseType.Community;

    public static CultureInfo Culture => CultureInfo.CurrentCulture;

    /// <summary>Writes the report to the reports folder as "<paramref name="name"/> start to end.pdf".</summary>
    /// <returns>The file path.</returns>
    public static string Write(StorageOptions options, string name, PayrollReport report, string title, Action<ColumnDescriptor> content)
    {
        Directory.CreateDirectory(options.ReportsFolder);
        var path = Path.Combine(options.ReportsFolder, $"{name} {report.Run.Start:yyyy-MM-dd} to {report.Run.End:yyyy-MM-dd}.pdf");
        Document(report, title, content).GeneratePdf(path);
        return path;
    }

    private static Document Document(PayrollReport report, string title, Action<ColumnDescriptor> content) =>
        QuestPDF.Fluent.Document.Create(container => container.Page(page =>
        {
            var run = report.Run;
            page.Size(PageSizes.Letter);
            page.Margin(40);
            page.DefaultTextStyle(x => x.FontSize(10));

            page.Header().PaddingBottom(10).Row(header =>
            {
                if (report.CompanyLogo is { } logo)
                    header.ConstantItem(80).PaddingRight(12).MaxHeight(60).Image(logo).FitArea();

                header.RelativeItem().Column(text =>
                {
                    if (report.CompanyName is { } company)
                        text.Item().Text(company).FontSize(14).SemiBold();
                    text.Item().Text(title).FontSize(20).Bold();
                    text.Item().Text($"Pay period: {Date(run.Start)} – {Date(run.End)} ({run.WeekCount} {(run.WeekCount == 1 ? "week" : "weeks")})");
                    text.Item().Text($"Payout date: {Date(run.PayoutDate)}");
                    if (run.LockedAt is { } lockedAt)
                        text.Item().Text($"Finalized: {lockedAt.ToString("g", Culture)}");
                    else
                        text.Item().Text("Preview: this period is not finalized, so the figures may still change.")
                            .Italic().FontColor(Colors.Orange.Darken3);
                });
            });

            page.Content().Column(content);

            page.Footer().AlignCenter().Text(t =>
            {
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" of ");
                t.TotalPages();
            });
        }));

    public static string Money(decimal amount) => amount.ToString("C", Culture);

    public static string Hours(decimal hours) => hours.ToString("0.00", Culture);

    public static string Date(DateOnly date) => date.ToString("MMM d, yyyy", Culture);
}
