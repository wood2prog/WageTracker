using WageTracker.Application.Common;
using WageTracker.Domain.Common;
using static WageTracker.Application.Tests.TestApp;

namespace WageTracker.Application.Tests;

public class PayrollServiceTests
{
    [Fact]
    public async Task Preview_calculates_every_employee_for_the_period()
    {
        var app = new TestApp();
        var ada = await app.AddHourlyAsync(20m);
        await app.AddSalariedAsync(52_000m);
        await app.WorkWeekAsync(ada.Id, 9m);

        var run = await app.PayrollService.GetRunAsync(Sunday.AddDays(3));

        Assert.False(run.IsLocked);
        Assert.Equal(Sunday, run.Start);
        Assert.Equal(new DateOnly(2026, 9, 12), run.End);
        Assert.Equal(new DateOnly(2026, 10, 10), run.PayoutDate);
        Assert.Equal(950m, run.Statements.Single(s => s.EmployeeId == ada.Id).GrossPay);
        Assert.Equal(1950m, run.TotalGrossPay);
    }

    [Fact]
    public async Task Finalizing_locks_the_period_and_writes_the_report()
    {
        var app = new TestApp();
        var ada = await app.AddHourlyAsync();
        await app.WorkWeekAsync(ada.Id, 8m);

        var result = await app.PayrollService.FinalizeAsync(Sunday);

        Assert.True(result.Run.IsLocked);
        Assert.Equal(app.Clock.Now, result.Run.LockedAt);
        Assert.Equal("report-2026-09-06.pdf", result.ReportLocation);
        Assert.Same(result.Run, app.Reports.Written.Single().Run);
        Assert.True(app.Runs.Items.Single().IsLocked);
    }

    [Fact]
    public async Task A_locked_period_keeps_its_snapshot_after_the_rate_changes()
    {
        var app = new TestApp();
        var ada = await app.AddHourlyAsync(20m);
        await app.WorkWeekAsync(ada.Id, 8m);
        await app.PayrollService.FinalizeAsync(Sunday);

        await app.EmployeeService.UpdateAsync(ada.Id, new Employees.EmployeeInput(
            "Ada", "Lovelace", ada.BirthDate, ada.HireDate, ada.EndDate, ada.EmploymentType, ada.CompensationType, 30m, false, 50m, 10));
        var run = await app.PayrollService.GetRunAsync(Sunday);

        Assert.Equal(800m, run.Statements.Single().GrossPay);
    }

    [Fact]
    public async Task Time_cannot_be_changed_in_a_finalized_period()
    {
        var app = new TestApp();
        var ada = await app.AddHourlyAsync();
        await app.WorkWeekAsync(ada.Id, 8m);
        await app.PayrollService.FinalizeAsync(Sunday);
        var monday = Sunday.AddDays(1).ToDateTime(new TimeOnly(18, 0));

        await Assert.ThrowsAsync<DomainException>(() => app.TimeEntryService.RecordAsync(ada.Id, monday, monday.AddHours(2)));
        await Assert.ThrowsAsync<DomainException>(() => app.TimeEntryService.DeleteAsync(app.TimeEntries.Items.Keys.First()));
        await Assert.ThrowsAsync<DomainException>(() =>
            app.TimeOffService.BookAsync(ada.Id, Sunday.AddDays(1), app.CurrentSettings.VacationType.Id));
    }

    [Fact]
    public async Task A_period_cannot_be_finalized_before_it_ends()
    {
        var app = new TestApp(now: new DateTime(2026, 9, 12, 23, 0, 0));

        await Assert.ThrowsAsync<DomainException>(() => app.PayrollService.FinalizeAsync(Sunday));
    }

    [Fact]
    public async Task A_period_can_be_finalized_only_once()
    {
        var app = new TestApp();
        await app.PayrollService.FinalizeAsync(Sunday);

        await Assert.ThrowsAsync<DomainException>(() => app.PayrollService.FinalizeAsync(Sunday));
    }

    [Fact]
    public async Task Salaried_overtime_is_carried_from_a_locked_previous_period()
    {
        var app = new TestApp();
        var grace = await app.AddSalariedAsync(52_000m, overtimeEligible: true);
        await app.WorkWeekAsync(grace.Id, 9m);
        var first = await app.PayrollService.FinalizeAsync(Sunday);

        var second = await app.PayrollService.GetRunAsync(Sunday.AddDays(7));

        Assert.Equal(187.50m, first.Run.Statements.Single().DeferredOvertimePay);
        Assert.Equal(187.50m, second.Statements.Single().OvertimePay);
        Assert.Equal(1187.50m, second.Statements.Single().GrossPay);
    }

    [Fact]
    public async Task Salaried_overtime_is_carried_from_an_open_previous_period()
    {
        var app = new TestApp();
        var grace = await app.AddSalariedAsync(52_000m, overtimeEligible: true);
        await app.WorkWeekAsync(grace.Id, 9m);

        var second = await app.PayrollService.GetRunAsync(Sunday.AddDays(7));

        Assert.Equal(187.50m, second.Statements.Single().OvertimePay);
    }

    [Fact]
    public async Task The_report_can_be_exported_again_only_for_a_locked_period()
    {
        var app = new TestApp();

        await Assert.ThrowsAsync<DomainException>(() => app.PayrollService.ExportReportAsync(Sunday));
        await app.PayrollService.FinalizeAsync(Sunday);
        await app.PayrollService.ExportReportAsync(Sunday);

        Assert.Equal(2, app.Reports.Written.Count);
    }

    [Fact]
    public async Task Periods_are_finalized_in_order()
    {
        var app = new TestApp();
        await app.PayrollService.FinalizeAsync(Sunday);

        await Assert.ThrowsAsync<DomainException>(() => app.PayrollService.FinalizeAsync(Sunday.AddDays(14)));
        await app.PayrollService.FinalizeAsync(Sunday.AddDays(7));
    }

    [Fact]
    public async Task A_schedule_change_starts_after_the_last_locked_period()
    {
        var app = new TestApp();
        await app.PayrollService.FinalizeAsync(Sunday);

        var settings = await app.SettingsService.ChangeScheduleAsync(new Settings.ScheduleInput(Domain.Payroll.PayFrequency.Monthly));
        var locked = await app.PayrollService.GetRunAsync(Sunday.AddDays(2));
        var transition = await app.PayrollService.GetRunAsync(Sunday.AddDays(14));

        Assert.Equal(Sunday.AddDays(7), settings.ScheduleEffectiveFrom);
        Assert.True(locked.IsLocked);
        Assert.Equal(1, locked.WeekCount);
        Assert.Equal(Sunday.AddDays(7), transition.Start);
        Assert.Equal(new DateOnly(2026, 10, 3), transition.End);
    }

    [Fact]
    public async Task Only_employees_employed_during_the_period_are_paid()
    {
        var app = new TestApp();
        var ada = await app.AddHourlyAsync();
        await app.EmployeeService.CreateAsync(new Employees.EmployeeInput(
            "New", "Hire", new DateOnly(2000, 1, 1), new DateOnly(2026, 10, 1), null,
            Domain.Employees.EmploymentType.FullTime, Domain.Employees.CompensationType.Salary, 52_000m, false, 50m, 10));

        var run = await app.PayrollService.GetRunAsync(Sunday);

        Assert.Equal([ada.Id], run.Statements.Select(s => s.EmployeeId));
    }

    [Fact]
    public async Task Payroll_needs_settings()
    {
        var app = new TestApp();
        app.Settings.Current = null;

        await Assert.ThrowsAsync<SettingsNotConfiguredException>(() => app.PayrollService.GetRunAsync(Sunday));
    }
}
