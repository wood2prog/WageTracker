using WageTracker.Domain.Common;
using WageTracker.Domain.Employees;

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
    /// Creates a new entry. The employee's hours must be entered daily. The entry must end after it starts,
    /// last no more than 24 hours, not end after <paramref name="now"/>, fall within the employee's employment,
    /// not overlap their other entries, and not fall in a week that has a weekly total.
    /// </summary>
    /// <param name="existingEntries">The employee's entries around this time.</param>
    /// <param name="weeklyTotals">The employee's weekly totals for the weeks this entry touches.</param>
    public static TimeEntry Record(
        Employee employee, DateTime start, DateTime end, IEnumerable<TimeEntry> existingEntries,
        IEnumerable<WeeklyHours> weeklyTotals, DateTime now)
    {
        if (employee.TimeRecording != TimeRecording.Daily)
            throw new DomainException($"{employee.FullName}'s hours are entered as a weekly total, not by the day.");
        var entry = new TimeEntry(Guid.NewGuid(), employee.Id, start, end);
        entry.EnsureFitsAmong(employee, existingEntries, weeklyTotals, now);
        return entry;
    }

    /// <summary>Moves the entry, applying the same rules as <see cref="Record"/> except how hours are entered.</summary>
    public void Reschedule(
        Employee employee, DateTime start, DateTime end, IEnumerable<TimeEntry> existingEntries,
        IEnumerable<WeeklyHours> weeklyTotals, DateTime now)
    {
        if (employee.Id != EmployeeId)
            throw new DomainException("A time entry can only be moved for its own employee.");
        var moved = new TimeEntry(Id, EmployeeId, start, end);
        moved.EnsureFitsAmong(employee, existingEntries, weeklyTotals, now);
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

    private void EnsureFitsAmong(
        Employee employee, IEnumerable<TimeEntry> existingEntries, IEnumerable<WeeklyHours> weeklyTotals, DateTime now)
    {
        if (End > now)
            throw new DomainException("A time entry cannot end in the future.");
        employee.EnsureEmployedOn(DateOnly.FromDateTime(Start));
        employee.EnsureEmployedOn(DateOnly.FromDateTime(End.AddTicks(-1)));

        var clash = existingEntries.FirstOrDefault(e => e.EmployeeId == EmployeeId && e.Id != Id && Overlaps(e));
        if (clash is not null)
            throw new DomainException(
                $"This entry overlaps the entry from {clash.Start:yyyy-MM-dd HH:mm} to {clash.End:yyyy-MM-dd HH:mm}.");

        var total = weeklyTotals.FirstOrDefault(w => w.EmployeeId == EmployeeId && Start < w.Week.EndsAt && w.Week.StartsAt < End);
        if (total is not null)
            throw new DomainException(
                $"The week of {total.Week} already has a weekly total of {total.Hours:0.00} hours. Clear it before adding time entries.");
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
