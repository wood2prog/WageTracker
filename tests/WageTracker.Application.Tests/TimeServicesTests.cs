using WageTracker.Application.Common;
using WageTracker.Application.Settings;
using WageTracker.Domain.Common;
using WageTracker.Domain.Employees;
using static WageTracker.Application.Tests.TestApp;

namespace WageTracker.Application.Tests;

public class TimeServicesTests
{
    private static readonly DateTime Monday8 = Sunday.AddDays(1).ToDateTime(new TimeOnly(8, 0));

    [Fact]
    public async Task Records_and_lists_time_entries()
    {
        var app = new TestApp();
        var ada = await app.AddHourlyAsync();

        await app.TimeEntryService.RecordAsync(ada.Id, Monday8, Monday8.AddHours(7.5));
        var listed = await app.TimeEntryService.ListAsync(ada.Id, Sunday, Sunday.AddDays(6));

        Assert.Equal(7.5m, listed.Single().Hours);
    }

    [Fact]
    public async Task Hours_worked_count_only_the_part_of_an_entry_inside_the_dates()
    {
        var app = new TestApp();
        var ada = await app.AddHourlyAsync();
        var saturday10pm = Sunday.AddDays(6).ToDateTime(new TimeOnly(22, 0));
        await app.TimeEntryService.RecordAsync(ada.Id, Monday8, Monday8.AddHours(8));
        await app.TimeEntryService.RecordAsync(ada.Id, saturday10pm, saturday10pm.AddHours(4));

        Assert.Equal(10m, await app.TimeEntryService.HoursWorkedAsync(ada.Id, Sunday, Sunday.AddDays(6)));
        Assert.Equal(2m, await app.TimeEntryService.HoursWorkedAsync(ada.Id, Sunday.AddDays(7), Sunday.AddDays(13)));
    }

    [Fact]
    public async Task Overlaps_are_checked_against_saved_entries()
    {
        var app = new TestApp();
        var ada = await app.AddHourlyAsync();
        await app.TimeEntryService.RecordAsync(ada.Id, Monday8, Monday8.AddHours(4));

        await Assert.ThrowsAsync<DomainException>(() =>
            app.TimeEntryService.RecordAsync(ada.Id, Monday8.AddHours(3), Monday8.AddHours(6)));
    }

    [Fact]
    public async Task Rescheduling_an_entry_keeps_its_id()
    {
        var app = new TestApp();
        var ada = await app.AddHourlyAsync();
        var entry = await app.TimeEntryService.RecordAsync(ada.Id, Monday8, Monday8.AddHours(4));

        var moved = await app.TimeEntryService.RescheduleAsync(entry.Id, Monday8.AddHours(1), Monday8.AddHours(6));

        Assert.Equal(entry.Id, moved.Id);
        Assert.Equal(5m, moved.Hours);
    }

    [Fact]
    public async Task Unknown_employees_are_reported()
    {
        var app = new TestApp();

        await Assert.ThrowsAsync<NotFoundException>(() => app.TimeEntryService.RecordAsync(Guid.NewGuid(), Monday8, Monday8.AddHours(1)));
    }

    [Fact]
    public async Task Vacation_balance_reflects_booked_days()
    {
        var app = new TestApp();
        var ada = await app.AddHourlyAsync();
        var vacation = app.CurrentSettings.VacationType.Id;

        var booked = await app.TimeOffService.BookAsync(ada.Id, Sunday.AddDays(1), vacation);
        await app.TimeOffService.BookAsync(ada.Id, Sunday.AddDays(2), vacation);
        var balance = await app.EmployeeService.GetVacationBalanceAsync(ada.Id, 2026);

        Assert.Equal("Vacation", booked.TypeName);
        Assert.Equal(10m, booked.Hours);
        Assert.Equal(2, balance.Used);
        Assert.Equal(8, balance.Remaining);
    }

    [Fact]
    public async Task Part_time_employees_cannot_book_time_off()
    {
        var app = new TestApp();
        var partTimer = await app.AddHourlyAsync(type: EmploymentType.PartTime);

        await Assert.ThrowsAsync<DomainException>(() =>
            app.TimeOffService.BookAsync(partTimer.Id, Sunday.AddDays(1), app.CurrentSettings.VacationType.Id));
    }

    [Fact]
    public async Task Time_off_cannot_be_booked_on_a_company_holiday()
    {
        var app = new TestApp();
        var ada = await app.AddHourlyAsync();
        await app.HolidayService.AddAsync("Labor Day", new HolidayRuleDto(HolidayRuleKind.NthWeekday, 9, DayOfWeek: DayOfWeek.Monday, Occurrence: 1));

        await Assert.ThrowsAsync<DomainException>(() =>
            app.TimeOffService.BookAsync(ada.Id, new DateOnly(2026, 9, 7), app.CurrentSettings.VacationType.Id));
    }

    [Fact]
    public async Task Employee_time_off_hours_override_shows_on_booked_days()
    {
        var app = new TestApp();
        var ada = await app.AddHourlyAsync();
        var vacation = app.CurrentSettings.VacationType.Id;

        await app.EmployeeService.SetTimeOffHoursAsync(ada.Id, vacation, 7.5m);
        var booked = await app.TimeOffService.BookAsync(ada.Id, Sunday.AddDays(1), vacation);

        Assert.Equal(7.5m, booked.Hours);
    }
}
