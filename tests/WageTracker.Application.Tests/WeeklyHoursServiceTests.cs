using WageTracker.Application.Employees;
using WageTracker.Domain.Common;
using WageTracker.Domain.Employees;
using static WageTracker.Application.Tests.TestApp;

namespace WageTracker.Application.Tests;

public class WeeklyHoursServiceTests
{
    [Fact]
    public async Task A_weekly_total_is_set_replaced_and_cleared()
    {
        var app = new TestApp();
        var ada = await app.AddHourlyAsync(recording: TimeRecording.Weekly);

        await app.TimeEntryService.SetWeeklyHoursAsync(ada.Id, Sunday, 40m);
        await app.TimeEntryService.SetWeeklyHoursAsync(ada.Id, Sunday, 42.5m);

        Assert.Equal(42.5m, (await app.TimeEntryService.GetWeeklyHoursAsync(ada.Id, Sunday))?.Hours);
        Assert.Equal(42.5m, await app.TimeEntryService.HoursWorkedAsync(ada.Id, Sunday, Sunday.AddDays(6)));

        await app.TimeEntryService.ClearWeeklyHoursAsync(ada.Id, Sunday);

        Assert.Null(await app.TimeEntryService.GetWeeklyHoursAsync(ada.Id, Sunday));
    }

    [Fact]
    public async Task The_week_must_start_on_a_Sunday()
    {
        var app = new TestApp();
        var ada = await app.AddHourlyAsync(recording: TimeRecording.Weekly);

        await Assert.ThrowsAsync<DomainException>(() => app.TimeEntryService.SetWeeklyHoursAsync(ada.Id, Sunday.AddDays(1), 40m));
    }

    [Fact]
    public async Task Switching_an_employee_to_weekly_entry_is_saved()
    {
        var app = new TestApp();
        var ada = await app.AddHourlyAsync();

        var updated = await app.EmployeeService.UpdateAsync(ada.Id, new EmployeeInput(
            ada.FirstName, ada.LastName, ada.BirthDate, ada.HireDate, ada.EndDate, ada.EmploymentType, ada.CompensationType,
            ada.CompensationAmount, ada.OvertimeEligible, ada.OvertimePercentage, ada.VacationDaysPermitted, TimeRecording.Weekly));

        Assert.Equal(TimeRecording.Daily, ada.TimeRecording);
        Assert.Equal(TimeRecording.Weekly, updated.TimeRecording);
        await app.TimeEntryService.SetWeeklyHoursAsync(ada.Id, Sunday, 40m);
    }

    [Fact]
    public async Task Payroll_pays_weekly_totals_with_overtime()
    {
        var app = new TestApp();
        var ada = await app.AddHourlyAsync(20m, recording: TimeRecording.Weekly);
        await app.TimeEntryService.SetWeeklyHoursAsync(ada.Id, Sunday, 45m);

        var run = await app.PayrollService.GetRunAsync(Sunday);
        var statement = run.Statements.Single();

        Assert.Equal(45m, statement.WorkedHours);
        Assert.Equal(5m, statement.OvertimeHours);
        Assert.Equal(950m, statement.GrossPay);
    }

    [Fact]
    public async Task Weekly_totals_cannot_change_in_a_finalized_period()
    {
        var app = new TestApp();
        var ada = await app.AddHourlyAsync(recording: TimeRecording.Weekly);
        await app.TimeEntryService.SetWeeklyHoursAsync(ada.Id, Sunday, 40m);
        await app.PayrollService.FinalizeAsync(Sunday);

        await Assert.ThrowsAsync<DomainException>(() => app.TimeEntryService.SetWeeklyHoursAsync(ada.Id, Sunday, 41m));
        await Assert.ThrowsAsync<DomainException>(() => app.TimeEntryService.ClearWeeklyHoursAsync(ada.Id, Sunday));
    }
}
