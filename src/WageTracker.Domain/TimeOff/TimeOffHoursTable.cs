namespace WageTracker.Domain.TimeOff;

/// <summary>
/// An employee's optional per-type hours for compensated time off. A type with no entry here
/// falls back to that type's <see cref="TimeOffType.DefaultHoursPerDay"/>.
/// </summary>
public sealed class TimeOffHoursTable
{
    private readonly Dictionary<Guid, decimal> _hours = [];

    /// <summary>Hours per day, keyed by <see cref="TimeOffType.Id"/>.</summary>
    public IReadOnlyDictionary<Guid, decimal> Entries => _hours;

    public bool IsEmpty => _hours.Count == 0;

    public decimal? HoursFor(Guid timeOffTypeId) => _hours.TryGetValue(timeOffTypeId, out var hours) ? hours : null;

    public void Set(Guid timeOffTypeId, decimal hoursPerDay) =>
        _hours[timeOffTypeId] = TimeOffType.ValidateHours(hoursPerDay);

    public void Clear(Guid timeOffTypeId) => _hours.Remove(timeOffTypeId);
}
