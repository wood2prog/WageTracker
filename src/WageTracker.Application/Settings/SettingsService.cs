using WageTracker.Application.Abstractions;
using WageTracker.Application.Common;
using WageTracker.Domain.Common;
using WageTracker.Domain.Payroll;

namespace WageTracker.Application.Settings;

/// <summary>The settings page: pay schedule, payout day, overtime, and time-off types. Every change returns the updated settings.</summary>
public sealed class SettingsService(IPayrollSettingsRepository settings, IPayrollRunRepository runs)
{
    public async Task<bool> IsConfiguredAsync() => await settings.GetAsync() is not null;

    public async Task<PayrollSettingsDto> GetAsync() => PayrollSettingsDto.From(await settings.RequireAsync());

    /// <summary>First-time setup, with the default Holiday, Vacation, and Sick types.</summary>
    public async Task<PayrollSettingsDto> InitializeAsync(ScheduleInput schedule, int payoutDayOfMonth)
    {
        if (await settings.GetAsync() is not null)
            throw new DomainException("Payroll settings are already set up.");
        var created = PayrollSettings.CreateDefault(schedule.ToSchedule(), payoutDayOfMonth);
        await settings.SaveAsync(created);
        return PayrollSettingsDto.From(created);
    }

    /// <summary>The new schedule takes effect the day after the last locked pay period, so locked periods keep their dates.</summary>
    public async Task<PayrollSettingsDto> ChangeScheduleAsync(ScheduleInput schedule)
    {
        var effectiveFrom = (await runs.GetLatestLockedAsync())?.Period.End.AddDays(1);
        return await ChangeAsync(s => s.ChangeSchedule(schedule.ToSchedule(), effectiveFrom));
    }

    public Task<PayrollSettingsDto> SetPayoutDayOfMonthAsync(int day) =>
        ChangeAsync(s => s.SetPayoutDayOfMonth(day));

    public Task<PayrollSettingsDto> SetOvertimeThresholdHoursAsync(decimal hours) =>
        ChangeAsync(s => s.SetOvertimeThresholdHours(hours));

    public Task<PayrollSettingsDto> SetTimeOffCountsTowardOvertimeAsync(bool counts) =>
        ChangeAsync(s => s.TimeOffCountsTowardOvertime = counts);

    public Task<PayrollSettingsDto> AddTimeOffTypeAsync(string name, decimal defaultHoursPerDay) =>
        ChangeAsync(s => s.AddTimeOffType(name, defaultHoursPerDay));

    public Task<PayrollSettingsDto> RenameTimeOffTypeAsync(Guid id, string name) =>
        ChangeAsync(s => s.RenameTimeOffType(id, name));

    public Task<PayrollSettingsDto> SetTimeOffTypeHoursAsync(Guid id, decimal defaultHoursPerDay) =>
        ChangeAsync(s => s.SetTimeOffTypeHours(id, defaultHoursPerDay));

    public Task<PayrollSettingsDto> ArchiveTimeOffTypeAsync(Guid id) =>
        ChangeAsync(s => s.ArchiveTimeOffType(id));

    public Task<PayrollSettingsDto> RestoreTimeOffTypeAsync(Guid id) =>
        ChangeAsync(s => s.RestoreTimeOffType(id));

    private async Task<PayrollSettingsDto> ChangeAsync(Action<PayrollSettings> change) =>
        PayrollSettingsDto.From(await settings.ChangeAsync(change));
}
