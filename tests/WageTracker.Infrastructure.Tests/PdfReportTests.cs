using WageTracker.Application.Payroll;
using WageTracker.Domain.Employees;
using WageTracker.Infrastructure.Reports;

namespace WageTracker.Infrastructure.Tests;

public sealed class PdfReportTests : IDisposable
{
    private readonly TempDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static PayStatementDto Statement(string name, int weeks, decimal deferred = 0m, int vacationDays = 0) => new(
        Guid.NewGuid(), name, CompensationType.Hourly, 20m, 1.5m, 40m * weeks, 8m, 2m, 800m * weeks, 60m, deferred,
        vacationDays, vacationDays * 200m, 800m * weeks + 60m + vacationDays * 200m,
        Enumerable.Range(0, weeks).Select(i => new WeekDto(
            new DateOnly(2026, 9, 6).AddDays(7 * i), new DateOnly(2026, 9, 12).AddDays(7 * i), 40m, 2m, 40m, 2m)).ToList());

    [Fact]
    public async Task Writes_a_pdf_named_for_the_period()
    {
        var run = new PayrollRunDto(new DateOnly(2026, 9, 6), new DateOnly(2026, 10, 3), 4, new DateOnly(2026, 10, 15),
            true, new DateTime(2026, 10, 5, 9, 0, 0),
            [Statement("Ada Lovelace", 4, vacationDays: 3), Statement("Grace Hopper", 4, deferred: 187.5m)]);

        var path = await new PdfPayrollReportWriter(_db.Options).WriteAsync(new PayrollReport(run, null, null));

        Assert.Equal(Path.Combine(_db.Options.ReportsFolder, "Payroll 2026-09-06 to 2026-10-03.pdf"), path);
        Assert.True(new FileInfo(path).Length > 1000);
    }

    [Fact]
    public async Task Shows_the_company_name_and_logo()
    {
        var run = new PayrollRunDto(new DateOnly(2026, 9, 6), new DateOnly(2026, 9, 12), 1, new DateOnly(2026, 10, 10),
            true, new DateTime(2026, 9, 14, 9, 0, 0), [Statement("Ada Lovelace", 1)]);

        var path = await new PdfPayrollReportWriter(_db.Options).WriteAsync(new PayrollReport(run, "Acme Tools", TempDatabase.Png));

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Handles_many_employees_across_pages()
    {
        var run = new PayrollRunDto(new DateOnly(2026, 9, 6), new DateOnly(2026, 10, 3), 5, new DateOnly(2026, 10, 15),
            true, new DateTime(2026, 10, 5, 9, 0, 0),
            Enumerable.Range(1, 40).Select(i => Statement($"Employee {i:00}", 5)).ToList());

        var path = await new PdfPayrollReportWriter(_db.Options).WriteAsync(new PayrollReport(run, null, null));

        Assert.True(File.Exists(path));
    }
}
