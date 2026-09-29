using WageTracker.Domain.Common;

namespace WageTracker.Domain.TimeOff;

public enum TimeOffKind
{
    /// <summary>A type users add in settings, such as Sick or Bereavement. Booked per employee.</summary>
    Standard,

    /// <summary>Built in. Booked per employee and counted against the yearly vacation allowance.</summary>
    Vacation,

    /// <summary>Built in. Never booked; credited automatically for each date on the company holiday calendar.</summary>
    Holiday,
}

/// <summary>
/// A kind of compensated time off. <see cref="DefaultHoursPerDay"/> is the global value; an employee can
/// override it. Changes go through <see cref="Payroll.PayrollSettings"/> so the list stays consistent.
/// </summary>
public sealed class TimeOffType
{
    public const decimal MaxHoursPerDay = 24m;

    public TimeOffType(Guid id, string name, decimal defaultHoursPerDay, TimeOffKind kind, bool isArchived = false)
    {
        Id = id;
        Name = ValidateName(name);
        DefaultHoursPerDay = ValidateHours(defaultHoursPerDay);
        Kind = kind;
        IsArchived = isArchived;
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public decimal DefaultHoursPerDay { get; private set; }

    public TimeOffKind Kind { get; }

    public bool IsVacation => Kind == TimeOffKind.Vacation;

    public bool IsHoliday => Kind == TimeOffKind.Holiday;

    /// <summary>The built-in Vacation and Holiday types cannot be archived.</summary>
    public bool IsBuiltIn => Kind != TimeOffKind.Standard;

    /// <summary>Archived types stay on existing records but cannot be booked.</summary>
    public bool IsArchived { get; private set; }

    internal void Rename(string name) => Name = ValidateName(name);

    internal void SetDefaultHoursPerDay(decimal hours) => DefaultHoursPerDay = ValidateHours(hours);

    internal void Archive() => IsArchived = true;

    internal void Restore() => IsArchived = false;

    internal static string ValidateName(string name) =>
        string.IsNullOrWhiteSpace(name) ? throw new DomainException("A name is required.") : name.Trim();

    internal static decimal ValidateHours(decimal hours)
    {
        if (hours < 0 || hours > MaxHoursPerDay)
            throw new DomainException($"Hours per day must be between 0 and {MaxHoursPerDay}.");
        if (!Rounding.HasAtMostTwoPlaces(hours))
            throw new DomainException("Hours per day can have at most two decimal places.");
        return hours;
    }
}
