using WageTracker.Application.Settings;
using WageTracker.Domain.Common;
using WageTracker.Domain.Payroll;

namespace WageTracker.Application.Tests;

public class SettingsServiceTests
{
    [Fact]
    public async Task First_time_setup_creates_the_default_types()
    {
        var app = new TestApp();
        app.Settings.Current = null;

        Assert.False(await app.SettingsService.IsConfiguredAsync());
        var settings = await app.SettingsService.InitializeAsync(new ScheduleInput(PayFrequency.Monthly), 15);

        Assert.Equal(PayFrequency.Monthly, settings.Frequency);
        Assert.Equal(15, settings.PayoutDayOfMonth);
        Assert.Equal(40m, settings.OvertimeThresholdHours);
        Assert.Equal(["Holiday", "Vacation", "Sick"], settings.TimeOffTypes.Select(t => t.Name));
    }

    [Fact]
    public async Task Setup_can_only_happen_once()
    {
        var app = new TestApp();

        await Assert.ThrowsAsync<DomainException>(() => app.SettingsService.InitializeAsync(new ScheduleInput(PayFrequency.Weekly), 1));
    }

    [Fact]
    public async Task A_two_week_schedule_keeps_its_anchor()
    {
        var app = new TestApp();

        var settings = await app.SettingsService.ChangeScheduleAsync(new ScheduleInput(PayFrequency.BiWeekly, TestApp.Sunday));

        Assert.Equal(PayFrequency.BiWeekly, settings.Frequency);
        Assert.Equal(TestApp.Sunday, settings.BiWeeklyAnchor);
    }

    [Fact]
    public async Task A_two_week_schedule_needs_an_anchor()
    {
        var app = new TestApp();

        await Assert.ThrowsAsync<DomainException>(() => app.SettingsService.ChangeScheduleAsync(new ScheduleInput(PayFrequency.BiWeekly)));
    }

    [Fact]
    public async Task Changes_are_saved_and_returned()
    {
        var app = new TestApp();

        await app.SettingsService.SetOvertimeThresholdHoursAsync(37.5m);
        await app.SettingsService.SetTimeOffCountsTowardOvertimeAsync(false);
        var settings = await app.SettingsService.AddTimeOffTypeAsync("Bereavement", 8m);

        Assert.Equal(37.5m, app.CurrentSettings.OvertimeThresholdHours);
        Assert.False(settings.TimeOffCountsTowardOvertime);
        Assert.Contains(settings.TimeOffTypes, t => t.Name == "Bereavement");
    }

    [Fact]
    public async Task Backups_keep_the_number_set_in_settings()
    {
        var app = new TestApp();
        await app.SettingsService.SetBackupsToKeepAsync(5);

        var path = await app.BackupService.BackupAsync();

        Assert.Equal("backup-2026-09-29.db", path);
        Assert.Equal((app.Clock.Now, 5), app.Backup.Calls.Single());
    }

    [Fact]
    public async Task Backups_use_the_default_count_before_setup()
    {
        var app = new TestApp();
        app.Settings.Current = null;

        await app.BackupService.BackupAsync();

        Assert.Equal(PayrollSettings.DefaultBackupsToKeep, app.Backup.Calls.Single().BackupsToKeep);
    }

    [Fact]
    public async Task The_report_gets_the_company_name_and_logo()
    {
        var app = new TestApp();
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
        await app.SettingsService.SetCompanyNameAsync("Acme Tools");
        var settings = await app.SettingsService.SetCompanyLogoAsync(png);

        await app.PayrollService.FinalizeAsync(TestApp.Sunday);

        Assert.Equal("Acme Tools", settings.CompanyName);
        var report = app.Reports.Written.Single();
        Assert.Equal("Acme Tools", report.CompanyName);
        Assert.Equal(png, report.CompanyLogo);
    }

    [Fact]
    public async Task Holidays_are_added_and_listed_on_their_observed_dates()
    {
        var app = new TestApp();
        var holidays = await app.HolidayService.AddAsync("Christmas", new HolidayRuleDto(HolidayRuleKind.FixedDate, 12, Day: 25));
        await app.HolidayService.SetObservedDateAsync(holidays.Single().Id, 2027, new DateOnly(2027, 12, 24));

        var observed = await app.HolidayService.ListObservedAsync(new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31));

        Assert.Equal([new DateOnly(2026, 12, 25), new DateOnly(2027, 12, 24)], observed.Select(h => h.Date));
        Assert.Equal(HolidayRuleKind.FixedDate, (await app.HolidayService.ListAsync()).Single().Rule.Kind);
    }
}
