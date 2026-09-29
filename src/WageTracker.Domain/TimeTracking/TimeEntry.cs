using WageTracker.Domain.Common;

namespace WageTracker.Domain.TimeTracking;

/// <summary>
/// A block of time an employee worked, from a start to an end date and time. New entries are created
/// with <see cref="Record"/>, which enforces the rules; the constructor is for loading saved entries.
/// </summary>
public sealed class TimeEntry
{
    public static readonly TimeSpan MaxLength = TimeSpan.FromHours(24);

    public TimeEntry(Guid id, Guid employeeId, DateTime start, DateTime end)
    {
        Id = id;
        EmployeeId = employeeId;
        (Start, End) = ValidateSpan(start, end);
    }

    public Guid Id { get; }

    public Guid EmployeeId { get; }

    public DateTime Start { get; private set; }

    public DateTime End { get; private set; }

    /// <summary>Hours worked, rounded to two decimal places.</summary>
    public decimal Hours => Rounding.Hours((decimal)(End - Start).TotalHours);

    /// <summary>
    /// Creates a new entry. It must end after it starts, last no more than 24 hours, not end after
    /// <paramref name="now"/>, and not overlap the employee's other entries.
    /// </summary>
    /// <param name="existingEntries">The employee's entries around this time.</param>
    public static TimeEntry Record(
        Guid employeeId, DateTime start, DateTime end, IEnumerable<TimeEntry> existingEntries, DateTime now)
    {
        var entry = new TimeEntry(Guid.NewGuid(), employeeId, start, end);
        entry.EnsureFitsAmong(existingEntries, now);
        return entry;
    }

    /// <summary>Moves the entry, applying the same rules as <see cref="Record"/>.</summary>
    public void Reschedule(DateTime start, DateTime end, IEnumerable<TimeEntry> existingEntries, DateTime now)
    {
        var moved = new TimeEntry(Id, EmployeeId, start, end);
        moved.EnsureFitsAmong(existingEntries, now);
        (Start, End) = (start, end);
    }

    /// <summary>
    /// Hours of this entry that fall within [from, to), rounded to two decimal places,
    /// so an entry that crosses a week boundary is split.
    /// </summary>
    public decimal HoursWithin(DateTime from, DateTime to)
    {
        var overlapStart = Start > from ? Start : from;
        var overlapEnd = End < to ? End : to;
        return overlapEnd > overlapStart ? Rounding.Hours((decimal)(overlapEnd - overlapStart).TotalHours) : 0m;
    }

    public bool Overlaps(TimeEntry other) => Start < other.End && other.Start < End;

    private void EnsureFitsAmong(IEnumerable<TimeEntry> existingEntries, DateTime now)
    {
        if (End > now)
            throw new DomainException("A time entry cannot end in the future.");

        var clash = existingEntries.FirstOrDefault(e => e.EmployeeId == EmployeeId && e.Id != Id && Overlaps(e));
        if (clash is not null)
            throw new DomainException(
                $"This entry overlaps the entry from {clash.Start:yyyy-MM-dd HH:mm} to {clash.End:yyyy-MM-dd HH:mm}.");
    }

    private static (DateTime, DateTime) ValidateSpan(DateTime start, DateTime end)
    {
        if (end <= start)
            throw new DomainException("A time entry must end after it starts.");
        if (end - start > MaxLength)
            throw new DomainException("A time entry cannot be longer than 24 hours.");
        return (start, end);
    }
}
