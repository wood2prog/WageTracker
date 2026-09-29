using WageTracker.Domain.Common;

namespace WageTracker.Domain.TimeOff;

/// <summary>One company holiday on the date it is credited.</summary>
public sealed record ObservedHoliday(Guid HolidayId, string Name, DateOnly Date);

/// <summary>The company's holidays, edited on the settings page.</summary>
public sealed class HolidayCalendar
{
    private readonly List<CompanyHoliday> _holidays;

    public HolidayCalendar(IEnumerable<CompanyHoliday>? holidays = null) => _holidays = holidays?.ToList() ?? [];

    public IReadOnlyList<CompanyHoliday> Holidays => _holidays;

    public CompanyHoliday Get(Guid id) =>
        _holidays.SingleOrDefault(h => h.Id == id) ?? throw new DomainException($"Unknown holiday {id}.");

    public CompanyHoliday Add(string name, HolidayRule rule)
    {
        EnsureNameIsFree(name, exceptId: null);
        var holiday = new CompanyHoliday(Guid.NewGuid(), name, rule);
        _holidays.Add(holiday);
        return holiday;
    }

    /// <summary>Removes the holiday from future pay. Locked pay periods keep what they already paid.</summary>
    public void Remove(Guid id) => _holidays.Remove(Get(id));

    public void Rename(Guid id, string name)
    {
        EnsureNameIsFree(name, exceptId: id);
        Get(id).Rename(name);
    }

    public void ChangeRule(Guid id, HolidayRule rule) => Get(id).ChangeRule(rule);

    public void SetObservedDate(Guid id, int year, DateOnly date) => Get(id).SetObservedDate(year, date);

    public void ClearObservedDate(Guid id, int year) => Get(id).ClearObservedDate(year);

    /// <summary>Holidays credited on dates from <paramref name="from"/> through <paramref name="to"/>, in date order.</summary>
    public IReadOnlyList<ObservedHoliday> Between(DateOnly from, DateOnly to)
    {
        // An observed date can move a holiday into the neighboring year, so check one year either side.
        var years = Enumerable.Range(from.Year - 1, to.Year - from.Year + 3);
        return _holidays
            .SelectMany(h => years.Select(y => new ObservedHoliday(h.Id, h.Name, h.DateIn(y))))
            .Where(h => h.Date >= from && h.Date <= to)
            .OrderBy(h => h.Date)
            .ToList();
    }

    public bool IsHoliday(DateOnly date) => Between(date, date).Count > 0;

    private void EnsureNameIsFree(string name, Guid? exceptId)
    {
        var trimmed = TimeOffType.ValidateName(name);
        if (_holidays.Any(h => h.Id != exceptId && string.Equals(h.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"A holiday named {trimmed} already exists.");
    }
}
