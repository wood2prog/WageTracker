using WageTracker.Domain.Common;

namespace WageTracker.Domain.TimeOff;

/// <summary>
/// A holiday every full-time employee gets. Its rule gives the date each year. When that date is
/// inconvenient, such as on a weekend, the user sets the observed date for that year.
/// </summary>
public sealed class CompanyHoliday
{
    /// <summary>How far an observed date can be from the rule's date.</summary>
    public const int MaxObservedShiftDays = 7;

    private readonly Dictionary<int, DateOnly> _observedDates;

    public CompanyHoliday(Guid id, string name, HolidayRule rule, IReadOnlyDictionary<int, DateOnly>? observedDates = null)
    {
        Id = id;
        Name = TimeOffType.ValidateName(name);
        Rule = rule;
        _observedDates = [];
        foreach (var (year, date) in observedDates ?? new Dictionary<int, DateOnly>())
            SetObservedDate(year, date);
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public HolidayRule Rule { get; private set; }

    /// <summary>Observed dates that replace the rule's date, keyed by the rule's year.</summary>
    public IReadOnlyDictionary<int, DateOnly> ObservedDates => _observedDates;

    /// <summary>The date the holiday is credited in <paramref name="year"/>: the observed date if set, otherwise the rule's date.</summary>
    public DateOnly DateIn(int year) =>
        _observedDates.TryGetValue(year, out var observed) ? observed : Rule.DateIn(year);

    internal void Rename(string name) => Name = TimeOffType.ValidateName(name);

    /// <summary>Changing the rule clears the observed dates, since they were set against the old rule.</summary>
    internal void ChangeRule(HolidayRule rule)
    {
        Rule = rule;
        _observedDates.Clear();
    }

    internal void SetObservedDate(int year, DateOnly date)
    {
        var ruleDate = Rule.DateIn(year);
        if (Math.Abs(date.DayNumber - ruleDate.DayNumber) > MaxObservedShiftDays)
            throw new DomainException(
                $"The observed date for {Name} must be within {MaxObservedShiftDays} days of {ruleDate:yyyy-MM-dd}.");
        _observedDates[year] = date;
    }

    internal void ClearObservedDate(int year) => _observedDates.Remove(year);
}
