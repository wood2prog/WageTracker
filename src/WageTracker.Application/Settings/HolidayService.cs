using WageTracker.Application.Abstractions;
using WageTracker.Application.Common;

namespace WageTracker.Application.Settings;

/// <summary>The company holiday calendar on the settings page. Every change returns the updated list.</summary>
public sealed class HolidayService(IPayrollSettingsRepository settings)
{
    public async Task<IReadOnlyList<CompanyHolidayDto>> ListAsync() =>
        (await settings.RequireAsync()).Holidays.Holidays.Select(CompanyHolidayDto.From).ToList();

    /// <summary>Holidays credited from <paramref name="from"/> through <paramref name="to"/>, in date order.</summary>
    public async Task<IReadOnlyList<ObservedHolidayDto>> ListObservedAsync(DateOnly from, DateOnly to) =>
        (await settings.RequireAsync()).Holidays.Between(from, to)
            .Select(h => new ObservedHolidayDto(h.HolidayId, h.Name, h.Date))
            .ToList();

    /// <summary>Each holiday with the date it is credited for <paramref name="year"/>: the observed date if set, otherwise the rule's date.</summary>
    public async Task<IReadOnlyList<ObservedHolidayDto>> ListForYearAsync(int year) =>
        (await settings.RequireAsync()).Holidays.Holidays
            .Select(h => new ObservedHolidayDto(h.Id, h.Name, h.DateIn(year)))
            .ToList();

    public Task<IReadOnlyList<CompanyHolidayDto>> AddAsync(string name, HolidayRuleDto rule) =>
        ChangeAsync(c => c.Add(name, rule.ToRule()));

    public Task<IReadOnlyList<CompanyHolidayDto>> RenameAsync(Guid id, string name) =>
        ChangeAsync(c => c.Rename(id, name));

    /// <summary>Changing the rule clears the holiday's observed dates.</summary>
    public Task<IReadOnlyList<CompanyHolidayDto>> ChangeRuleAsync(Guid id, HolidayRuleDto rule) =>
        ChangeAsync(c => c.ChangeRule(id, rule.ToRule()));

    public Task<IReadOnlyList<CompanyHolidayDto>> SetObservedDateAsync(Guid id, int year, DateOnly date) =>
        ChangeAsync(c => c.SetObservedDate(id, year, date));

    public Task<IReadOnlyList<CompanyHolidayDto>> ClearObservedDateAsync(Guid id, int year) =>
        ChangeAsync(c => c.ClearObservedDate(id, year));

    public Task<IReadOnlyList<CompanyHolidayDto>> RemoveAsync(Guid id) =>
        ChangeAsync(c => c.Remove(id));

    private async Task<IReadOnlyList<CompanyHolidayDto>> ChangeAsync(Action<Domain.TimeOff.HolidayCalendar> change) =>
        (await settings.ChangeAsync(s => change(s.Holidays))).Holidays.Holidays.Select(CompanyHolidayDto.From).ToList();
}
