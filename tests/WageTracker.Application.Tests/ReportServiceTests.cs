using WageTracker.Application.Common;
using WageTracker.Application.Reports;
using WageTracker.Domain.Common;
using static WageTracker.Application.Tests.TestApp;

namespace WageTracker.Application.Tests;

public class ReportServiceTests
{
    [Fact]
    public void Reports_are_listed_in_registration_order()
    {
        var service = new ReportService([new StubReport("First"), new StubReport("Second", requiresFinalizedPeriod: true)]);

        Assert.Equal([new ReportDto("First", false), new ReportDto("Second", true)], service.List());
    }

    [Fact]
    public async Task Creates_the_named_report_for_the_period()
    {
        var first = new StubReport("First");
        var second = new StubReport("Second");
        var service = new ReportService([first, second]);

        var location = await service.CreateAsync("Second", Sunday);

        Assert.Equal("Second.pdf", location);
        Assert.Empty(first.Dates);
        Assert.Equal([Sunday], second.Dates);
    }

    [Fact]
    public async Task An_unknown_report_is_not_found()
    {
        var service = new ReportService([new StubReport("First")]);

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync("Missing", Sunday));
    }

    [Fact]
    public async Task The_payroll_report_needs_a_finalized_period()
    {
        var app = new TestApp();
        var service = new ReportService([new PayrollPeriodReport(app.PayrollService)]);

        Assert.True(Assert.Single(service.List()).RequiresFinalizedPeriod);
        await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync("Payroll report", Sunday));
        await app.PayrollService.FinalizeAsync(Sunday);
        await service.CreateAsync("Payroll report", Sunday);

        Assert.Equal(2, app.Reports.Written.Count);
    }

    [Fact]
    public async Task The_hours_summary_previews_an_open_period()
    {
        var app = new TestApp();
        var ada = await app.AddHourlyAsync(20m);
        await app.WorkWeekAsync(ada.Id, 9m);
        var writer = new RecordingHoursReportWriter();
        var service = new ReportService([new HoursSummaryReport(app.PayrollService, writer)]);

        Assert.False(Assert.Single(service.List()).RequiresFinalizedPeriod);
        await service.CreateAsync("Hours summary", Sunday);

        var run = Assert.Single(writer.Written).Run;
        Assert.False(run.IsLocked);
        Assert.Equal(45m, run.Statements.Single().Weeks.Sum(w => w.TotalHours));
    }

    private sealed class StubReport(string name, bool requiresFinalizedPeriod = false) : IPeriodReport
    {
        public List<DateOnly> Dates { get; } = [];
        public string Name => name;
        public bool RequiresFinalizedPeriod => requiresFinalizedPeriod;

        public Task<string> CreateAsync(DateOnly date)
        {
            Dates.Add(date);
            return Task.FromResult($"{name}.pdf");
        }
    }
}
