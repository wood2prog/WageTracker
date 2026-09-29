using Microsoft.Extensions.DependencyInjection;
using WageTracker.Application;
using WageTracker.Application.Abstractions;
using WageTracker.Application.Employees;
using WageTracker.Application.Payroll;
using WageTracker.Application.Settings;
using WageTracker.Application.TimeOff;
using WageTracker.Application.TimeTracking;
using WageTracker.Domain.Common;
using WageTracker.Domain.Employees;
using WageTracker.Domain.Payroll;

namespace WageTracker.Infrastructure.Tests;

/// <summary>The real services running against SQLite and the PDF writer, wired as the app will wire them.</summary>
public sealed class EndToEndTests : IDisposable
{
    private static readonly DateOnly Sunday = new(2026, 9, 6);

    private readonly TempDatabase _db = new();
    private readonly ServiceProvider _services;

    public EndToEndTests()
    {
        _services = new ServiceCollection()
            .AddWageTrackerApplication()
            .AddWageTrackerInfrastructure(o =>
            {
                o.DatabasePath = _db.Options.DatabasePath;
                o.ReportsFolder = _db.Options.ReportsFolder;
            })
            .AddSingleton<IClock>(new FixedClock(new DateTime(2026, 9, 29, 17, 0, 0)))
            .BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();
        _db.Dispose();
    }

    private T Get<T>() where T : notnull => _services.GetRequiredService<T>();

    [Fact]
    public async Task A_pay_period_can_be_recorded_finalized_and_reported()
    {
        await Get<SettingsService>().InitializeAsync(new ScheduleInput(PayFrequency.Weekly), 10);
        await Get<HolidayService>().AddAsync("Labor Day", new HolidayRuleDto(HolidayRuleKind.NthWeekday, 9, DayOfWeek: DayOfWeek.Monday, Occurrence: 1));
        var ada = await Get<EmployeeService>().CreateAsync(new EmployeeInput(
            "Ada", "Lovelace", new DateOnly(1990, 12, 10), new DateOnly(2020, 1, 6), null,
            EmploymentType.FullTime, CompensationType.Hourly, 20m, false, 50m, 10));
        var vacation = (await Get<SettingsService>().GetAsync()).TimeOffTypes.Single(t => t.Kind == Domain.TimeOff.TimeOffKind.Vacation);

        for (var day = 2; day <= 5; day++) // Tuesday–Friday; Monday is Labor Day
        {
            var start = Sunday.AddDays(day).ToDateTime(new TimeOnly(8, 0));
            await Get<TimeEntryService>().RecordAsync(ada.Id, start, start.AddHours(9));
        }
        await Get<TimeOffService>().BookAsync(ada.Id, new DateOnly(2026, 9, 14), vacation.Id);

        var result = await Get<PayrollService>().FinalizeAsync(Sunday);
        var reloaded = await Get<PayrollService>().GetRunAsync(Sunday);
        var statement = reloaded.Statements.Single();

        // 36 h worked + 8 h Labor Day = 44 h: 40 regular, 4 overtime.
        Assert.True(reloaded.IsLocked);
        Assert.Equal(44m, statement.WorkedHours + statement.TimeOffHours);
        Assert.Equal(4m, statement.OvertimeHours);
        Assert.Equal(40m * 20m + 4m * 30m, statement.GrossPay);
        Assert.True(File.Exists(result.ReportLocation));
        Assert.Equal("%PDF"u8.ToArray(), File.ReadAllBytes(result.ReportLocation)[..4]);

        var monday = Sunday.AddDays(1).ToDateTime(new TimeOnly(8, 0));
        await Assert.ThrowsAsync<DomainException>(() => Get<TimeEntryService>().RecordAsync(ada.Id, monday, monday.AddHours(1)));
        Assert.Equal(1, (await Get<EmployeeService>().GetVacationBalanceAsync(ada.Id, 2026)).Used);
    }

    [Fact]
    public async Task Data_persists_across_app_restarts()
    {
        await Get<SettingsService>().InitializeAsync(new ScheduleInput(PayFrequency.Monthly), 15);
        var ada = await Get<EmployeeService>().CreateAsync(new EmployeeInput(
            "Ada", "Lovelace", new DateOnly(1990, 12, 10), new DateOnly(2020, 1, 6), null,
            EmploymentType.FullTime, CompensationType.Hourly, 20m, false, 50m, 10));

        using var restarted = new ServiceCollection()
            .AddWageTrackerApplication()
            .AddWageTrackerInfrastructure(o => o.DatabasePath = _db.Options.DatabasePath)
            .BuildServiceProvider();

        Assert.True(await restarted.GetRequiredService<SettingsService>().IsConfiguredAsync());
        Assert.Equal(ada, await restarted.GetRequiredService<EmployeeService>().GetAsync(ada.Id), new EmployeeDtoComparer());
    }

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime Now => now;
    }

    /// <summary>EmployeeDto holds a dictionary, so records compare it by reference; compare its contents instead.</summary>
    private sealed class EmployeeDtoComparer : IEqualityComparer<EmployeeDto>
    {
        public bool Equals(EmployeeDto? x, EmployeeDto? y) =>
            x is not null && y is not null
            && x with { TimeOffHoursOverrides = y.TimeOffHoursOverrides } == y
            && x.TimeOffHoursOverrides.OrderBy(p => p.Key).SequenceEqual(y.TimeOffHoursOverrides.OrderBy(p => p.Key));

        public int GetHashCode(EmployeeDto obj) => obj.Id.GetHashCode();
    }
}
