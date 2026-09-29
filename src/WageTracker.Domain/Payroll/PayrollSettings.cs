using System.Diagnostics.CodeAnalysis;
using WageTracker.Domain.Calendar;
using WageTracker.Domain.Common;
using WageTracker.Domain.TimeOff;

namespace WageTracker.Domain.Payroll;

/// <summary>Company-wide payroll settings, edited on the settings page.</summary>
public sealed class PayrollSettings
{
    public const decimal DefaultOvertimeThresholdHours = 40m;
    public const int MaxPayoutDayOfMonth = 28;

    private readonly List<TimeOffType> _timeOffTypes;

    /// <param name="timeOffTypes">Must include exactly one Vacation type and one Holiday type.</param>
    public PayrollSettings(
        PayPeriodSchedule schedule,
        int payoutDayOfMonth,
        IEnumerable<TimeOffType> timeOffTypes,
        HolidayCalendar holidays,
        decimal overtimeThresholdHours = DefaultOvertimeThresholdHours,
        bool timeOffCountsTowardOvertime = true,
        DateOnly? scheduleEffectiveFrom = null)
    {
        ChangeSchedule(schedule, scheduleEffectiveFrom);
        SetPayoutDayOfMonth(payoutDayOfMonth);
        SetOvertimeThresholdHours(overtimeThresholdHours);
        TimeOffCountsTowardOvertime = timeOffCountsTowardOvertime;
        Holidays = holidays;

        _timeOffTypes = timeOffTypes.ToList();
        if (_timeOffTypes.Count(t => t.IsVacation) != 1 || _timeOffTypes.Count(t => t.IsHoliday) != 1)
            throw new DomainException("Settings must have exactly one Vacation type and one Holiday type.");
    }

    /// <summary>
    /// New settings with the built-in Holiday (8 h) and Vacation (10 h) types, a Sick (8 h) type,
    /// and an empty holiday calendar.
    /// </summary>
    public static PayrollSettings CreateDefault(PayPeriodSchedule schedule, int payoutDayOfMonth) =>
        new(schedule, payoutDayOfMonth,
        [
            new TimeOffType(Guid.NewGuid(), "Holiday", 8m, TimeOffKind.Holiday),
            new TimeOffType(Guid.NewGuid(), "Vacation", 10m, TimeOffKind.Vacation),
            new TimeOffType(Guid.NewGuid(), "Sick", 8m, TimeOffKind.Standard),
        ],
        new HolidayCalendar());

    public PayPeriodSchedule Schedule { get; private set; }

    /// <summary>
    /// The Sunday the current schedule took effect, or null if it has always applied. Days before it are
    /// in pay periods that were locked under an earlier schedule.
    /// </summary>
    public DateOnly? ScheduleEffectiveFrom { get; private set; }

    /// <summary>The day of the month (1–28) payouts happen on.</summary>
    public int PayoutDayOfMonth { get; private set; }

    /// <summary>Hours in a work week above which hours are overtime.</summary>
    public decimal OvertimeThresholdHours { get; private set; }

    /// <summary>
    /// Whether compensated time off counts toward the overtime threshold. When true (the default),
    /// every hour over the threshold is overtime, however it was earned.
    /// </summary>
    public bool TimeOffCountsTowardOvertime { get; set; }

    public IReadOnlyList<TimeOffType> TimeOffTypes => _timeOffTypes;

    public TimeOffType VacationType => _timeOffTypes.Single(t => t.IsVacation);

    /// <summary>Supplies the hours each company holiday is worth.</summary>
    public TimeOffType HolidayType => _timeOffTypes.Single(t => t.IsHoliday);

    public HolidayCalendar Holidays { get; }

    public TimeOffType GetTimeOffType(Guid id) =>
        _timeOffTypes.SingleOrDefault(t => t.Id == id)
        ?? throw new DomainException($"Unknown time-off type {id}.");

    /// <summary>
    /// The first payout day strictly after the period's last day, so a period is never paid before it ends.
    /// </summary>
    public DateOnly PayoutDateFor(PayPeriod period)
    {
        var candidate = new DateOnly(period.End.Year, period.End.Month, PayoutDayOfMonth);
        return candidate > period.End ? candidate : candidate.AddMonths(1);
    }

    /// <summary>
    /// The pay period containing <paramref name="date"/> under the current schedule. The first period after
    /// a schedule change is trimmed so it starts on <see cref="ScheduleEffectiveFrom"/>.
    /// </summary>
    public PayPeriod PeriodContaining(DateOnly date)
    {
        if (date < ScheduleEffectiveFrom)
            throw new DomainException($"{date:yyyy-MM-dd} is in a pay period locked under an earlier schedule.");

        var period = Schedule.PeriodContaining(date);
        if (ScheduleEffectiveFrom is { } start && period.Start < start)
            return new PayPeriod(new WorkWeek(start), (period.End.DayNumber - start.DayNumber + 1) / 7);
        return period;
    }

    /// <param name="effectiveFrom">
    /// The Sunday the new schedule starts, which is the day after the last locked pay period; null if no period is locked.
    /// </param>
    [MemberNotNull(nameof(Schedule))]
    public void ChangeSchedule(PayPeriodSchedule schedule, DateOnly? effectiveFrom)
    {
        if (effectiveFrom is { } sunday)
            _ = new WorkWeek(sunday); // throws unless it is a Sunday
        Schedule = schedule;
        ScheduleEffectiveFrom = effectiveFrom;
    }

    public void SetPayoutDayOfMonth(int day)
    {
        if (day < 1 || day > MaxPayoutDayOfMonth)
            throw new DomainException($"The payout day must be between 1 and {MaxPayoutDayOfMonth}.");
        PayoutDayOfMonth = day;
    }

    public void SetOvertimeThresholdHours(decimal hours)
    {
        if (hours <= 0)
            throw new DomainException("The overtime threshold must be greater than zero.");
        if (!Rounding.HasAtMostTwoPlaces(hours))
            throw new DomainException("The overtime threshold can have at most two decimal places.");
        OvertimeThresholdHours = hours;
    }

    public TimeOffType AddTimeOffType(string name, decimal defaultHoursPerDay)
    {
        EnsureNameIsFree(name, exceptId: null);
        var type = new TimeOffType(Guid.NewGuid(), name, defaultHoursPerDay, TimeOffKind.Standard);
        _timeOffTypes.Add(type);
        return type;
    }

    public void RenameTimeOffType(Guid id, string name)
    {
        EnsureNameIsFree(name, exceptId: id);
        GetTimeOffType(id).Rename(name);
    }

    public void SetTimeOffTypeHours(Guid id, decimal defaultHoursPerDay) =>
        GetTimeOffType(id).SetDefaultHoursPerDay(defaultHoursPerDay);

    /// <summary>Stops a type from being booked. Days already booked with it keep it.</summary>
    public void ArchiveTimeOffType(Guid id)
    {
        var type = GetTimeOffType(id);
        if (type.IsBuiltIn)
            throw new DomainException($"The built-in {type.Name} type cannot be archived.");
        type.Archive();
    }

    public void RestoreTimeOffType(Guid id)
    {
        var type = GetTimeOffType(id);
        EnsureNameIsFree(type.Name, exceptId: id);
        type.Restore();
    }

    private void EnsureNameIsFree(string name, Guid? exceptId)
    {
        var trimmed = TimeOffType.ValidateName(name);
        if (_timeOffTypes.Any(t => t.Id != exceptId && !t.IsArchived
                && string.Equals(t.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"A time-off type named {trimmed} already exists.");
    }
}
