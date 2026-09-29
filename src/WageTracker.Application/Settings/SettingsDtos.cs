using WageTracker.Domain.Common;
using WageTracker.Domain.Payroll;
using WageTracker.Domain.TimeOff;

namespace WageTracker.Application.Settings;

/// <param name="BiWeeklyAnchor">Required for <see cref="PayFrequency.BiWeekly"/>: a Sunday that starts a period.</param>
public sealed record ScheduleInput(PayFrequency Frequency, DateOnly? BiWeeklyAnchor = null)
{
    internal PayPeriodSchedule ToSchedule() => Frequency switch
    {
        PayFrequency.Weekly => PayPeriodSchedule.Weekly(),
        PayFrequency.Monthly => PayPeriodSchedule.Monthly(),
        PayFrequency.BiWeekly => PayPeriodSchedule.BiWeekly(
            BiWeeklyAnchor ?? throw new DomainException("A two-week schedule needs a starting Sunday.")),
        _ => throw new DomainException($"Unknown pay frequency {Frequency}."),
    };
}

public sealed record TimeOffTypeDto(Guid Id, string Name, decimal DefaultHoursPerDay, TimeOffKind Kind, bool IsArchived)
{
    internal static TimeOffTypeDto From(TimeOffType t) => new(t.Id, t.Name, t.DefaultHoursPerDay, t.Kind, t.IsArchived);
}

public enum HolidayRuleKind
{
    /// <summary>The same date every year: uses Month and Day.</summary>
    FixedDate,

    /// <summary>The nth weekday of a month: uses Month, DayOfWeek, and Occurrence (1–4).</summary>
    NthWeekday,

    /// <summary>The last weekday of a month: uses Month and DayOfWeek.</summary>
    LastWeekday,
}

public sealed record HolidayRuleDto(HolidayRuleKind Kind, int Month, int? Day = null, DayOfWeek? DayOfWeek = null, int? Occurrence = null)
{
    internal HolidayRule ToRule() => Kind switch
    {
        HolidayRuleKind.FixedDate => HolidayRule.Fixed(Month, Day ?? throw Missing("day")),
        HolidayRuleKind.NthWeekday => HolidayRule.NthWeekday(Month, DayOfWeek ?? throw Missing("weekday"), Occurrence ?? throw Missing("occurrence")),
        HolidayRuleKind.LastWeekday => HolidayRule.LastWeekday(Month, DayOfWeek ?? throw Missing("weekday")),
        _ => throw new DomainException($"Unknown holiday rule {Kind}."),
    };

    internal static HolidayRuleDto From(HolidayRule rule) => rule switch
    {
        FixedDateRule r => new(HolidayRuleKind.FixedDate, r.Month, Day: r.Day),
        NthWeekdayRule r => new(HolidayRuleKind.NthWeekday, r.Month, DayOfWeek: r.DayOfWeek, Occurrence: r.Occurrence),
        LastWeekdayRule r => new(HolidayRuleKind.LastWeekday, r.Month, DayOfWeek: r.DayOfWeek),
        _ => throw new DomainException($"Unknown holiday rule {rule.GetType().Name}."),
    };

    private static DomainException Missing(string part) => new($"The holiday rule needs a {part}.");
}

/// <param name="ObservedDates">Dates that replace the rule's date, keyed by year.</param>
public sealed record CompanyHolidayDto(Guid Id, string Name, HolidayRuleDto Rule, IReadOnlyDictionary<int, DateOnly> ObservedDates)
{
    internal static CompanyHolidayDto From(CompanyHoliday h) =>
        new(h.Id, h.Name, HolidayRuleDto.From(h.Rule), new Dictionary<int, DateOnly>(h.ObservedDates));
}

public sealed record ObservedHolidayDto(Guid HolidayId, string Name, DateOnly Date);

/// <param name="ScheduleEffectiveFrom">The Sunday the current schedule took effect, or null if it always applied.</param>
public sealed record PayrollSettingsDto(
    PayFrequency Frequency,
    DateOnly? BiWeeklyAnchor,
    DateOnly? ScheduleEffectiveFrom,
    int PayoutDayOfMonth,
    decimal OvertimeThresholdHours,
    bool TimeOffCountsTowardOvertime,
    string? CompanyName,
    byte[]? CompanyLogo,
    int BackupsToKeep,
    IReadOnlyList<TimeOffTypeDto> TimeOffTypes,
    IReadOnlyList<CompanyHolidayDto> Holidays)
{
    internal static PayrollSettingsDto From(PayrollSettings s) => new(
        s.Schedule.Frequency,
        s.Schedule.Anchor,
        s.ScheduleEffectiveFrom,
        s.PayoutDayOfMonth,
        s.OvertimeThresholdHours,
        s.TimeOffCountsTowardOvertime,
        s.CompanyName,
        s.CompanyLogo,
        s.BackupsToKeep,
        s.TimeOffTypes.Select(TimeOffTypeDto.From).ToList(),
        s.Holidays.Holidays.Select(CompanyHolidayDto.From).ToList());
}
