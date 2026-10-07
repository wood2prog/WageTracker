using WageTracker.Domain.Calendar;
using WageTracker.Domain.Common;
using WageTracker.Domain.Employees;
using WageTracker.Domain.TimeOff;
using WageTracker.Domain.TimeTracking;

namespace WageTracker.Domain.Payroll;

/// <summary>
/// An employee's hours and gross pay for one pay period. It is a snapshot: the rates and hours used
/// are stored on it, so a locked statement does not change when settings or the employee change later.
/// </summary>
public sealed class PayStatement(
    Guid employeeId,
    PayPeriod period,
    DateOnly payoutDate,
    IReadOnlyList<WeekSummary> weeks,
    CompensationType compensationType,
    decimal hourlyRate,
    decimal overtimeMultiplier,
    bool overtimeEligible,
    decimal basePay,
    decimal overtimePay,
    decimal deferredOvertimePay,
    int vacationDaysPaidOut,
    decimal vacationPayout)
{
    public Guid EmployeeId { get; } = employeeId;

    public PayPeriod Period { get; } = period;

    public DateOnly PayoutDate { get; } = payoutDate;

    public IReadOnlyList<WeekSummary> Weeks { get; } = weeks;

    public CompensationType CompensationType { get; } = compensationType;

    /// <summary>The hourly rate, or for salary the weekly salary divided by the overtime threshold.</summary>
    public decimal HourlyRate { get; } = hourlyRate;

    public decimal OvertimeMultiplier { get; } = overtimeMultiplier;

    /// <summary>Whether overtime hours earn overtime pay. Always true for hourly; set on the compensation for salary.</summary>
    public bool OvertimeEligible { get; } = overtimeEligible;

    /// <summary>
    /// Hourly: regular hours (worked and time off) times the rate. Salary: weekly salary ÷ 5 for each weekday employed.
    /// </summary>
    public decimal BasePay { get; } = basePay;

    /// <summary>
    /// Overtime paid in this period: hourly overtime earned in this period, plus any salaried overtime
    /// carried over from the previous period.
    /// </summary>
    public decimal OvertimePay { get; } = overtimePay;

    /// <summary>Salaried overtime earned in this period. It is paid in the next period, unless this is the employee's final period.</summary>
    public decimal DeferredOvertimePay { get; } = deferredOvertimePay;

    /// <summary>Unused vacation days paid out. Only nonzero in the last pay period of the year or an employee's final period.</summary>
    public int VacationDaysPaidOut { get; } = vacationDaysPaidOut;

    public decimal VacationPayout { get; } = vacationPayout;

    public decimal GrossPay => BasePay + OvertimePay + VacationPayout;

    public decimal WorkedHours => Weeks.Sum(w => w.WorkedHours);

    public decimal TimeOffHours => Weeks.Sum(w => w.TimeOffHours);

    public decimal OvertimeHours => Weeks.Sum(w => w.OvertimeHours);

    /// <summary>
    /// The hours the base pay covers. Hourly: the regular hours. Salary: the overtime threshold for a full week
    /// and a fifth of it per weekday employed for a partial week, whatever hours were recorded.
    /// </summary>
    public decimal CompensatedRegularHours => Weeks.Sum(w => w.CompensatedRegularHours);

    /// <summary>Overtime hours in <paramref name="week"/> that earn overtime pay.</summary>
    public decimal CompensatedOvertimeHoursIn(WeekSummary week) => OvertimeEligible ? week.OvertimeHours : 0m;

    /// <summary>
    /// Overtime hours that earn overtime pay: none for salaried employees who are not eligible. Salaried overtime
    /// counts in the period it is earned, though it is paid in the next.
    /// </summary>
    public decimal CompensatedOvertimeHours => OvertimeEligible ? OvertimeHours : 0m;

    public decimal CompensatedHours => CompensatedRegularHours + CompensatedOvertimeHours;

    /// <summary>
    /// Calculates the statement. Overtime is figured week by week, and a time entry that crosses a
    /// week boundary is split between the two weeks. A week's worked hours are its time entries plus its weekly total.
    /// </summary>
    /// <param name="timeOff">
    /// The employee's time off from January 1 of the period's first year through the end of the period,
    /// so the year-end vacation payout can count the days used.
    /// </param>
    /// <param name="previous">
    /// The employee's statement for the period just before this one, if there is one. Salaried overtime
    /// it deferred is paid in this statement.
    /// </param>
    /// <param name="weeklyTotals">The employee's weekly totals, for weeks whose hours were entered as one total.</param>
    public static PayStatement Calculate(
        Employee employee,
        PayPeriod period,
        PayrollSettings settings,
        IEnumerable<TimeEntry> timeEntries,
        IEnumerable<CompensatedTimeOff> timeOff,
        PayStatement? previous,
        IEnumerable<WeeklyHours>? weeklyTotals = null)
    {
        var entries = timeEntries.ToList();
        var daysOff = timeOff.ToList();
        var totals = weeklyTotals?.ToList() ?? [];
        if (entries.Any(e => e.EmployeeId != employee.Id) || daysOff.Any(t => t.EmployeeId != employee.Id)
            || totals.Any(w => w.EmployeeId != employee.Id))
            throw new DomainException("All time entries, weekly totals, and time off must belong to the employee being paid.");
        if (previous is not null && (previous.EmployeeId != employee.Id || previous.Period.End.AddDays(1) != period.Start))
            throw new DomainException("The previous statement must be this employee's statement for the period just before.");

        var weeks = period.Weeks
            .Select(week => SummarizeWeek(week, employee, entries, totals, daysOff, settings))
            .ToList();

        var compensation = employee.Compensation;
        var hourlyRate = compensation.HourlyRate(settings.OvertimeThresholdHours);
        var overtimeRate = hourlyRate * employee.OvertimeMultiplier;
        var overtimeEarned = compensation.OvertimeEligible ? weeks.Sum(w => w.OvertimeHours) * overtimeRate : 0m;
        var carriedOvertime = previous?.DeferredOvertimePay ?? 0m;
        var isFinalPeriod = employee.EndDate is { } lastDay && period.Contains(lastDay);

        decimal basePay, overtimePay, deferredOvertimePay;
        if (compensation.Type == CompensationType.Salary)
        {
            // A week is paid in fifths, one for each weekday employed, so partial weeks at hire and end are prorated.
            basePay = weeks.Sum(w => compensation.WeeklySalary * employee.WeekdaysEmployed(w.Week.Start, w.Week.End) / 5m);
            // Salaried overtime is normally paid next period, but a leaver's final period settles everything.
            overtimePay = carriedOvertime + (isFinalPeriod ? overtimeEarned : 0m);
            deferredOvertimePay = isFinalPeriod ? 0m : overtimeEarned;
        }
        else
        {
            basePay = weeks.Sum(w => w.RegularHours) * hourlyRate;
            overtimePay = overtimeEarned + carriedOvertime;
            deferredOvertimePay = 0m;
        }

        var vacationDays = UnusedVacationDaysToPayOut(employee, period, settings, daysOff);
        var vacationPayout = vacationDays * employee.ResolveTimeOffHours(settings.VacationType) * hourlyRate;

        return new PayStatement(
            employee.Id,
            period,
            settings.PayoutDateFor(period),
            weeks,
            compensation.Type,
            hourlyRate,
            employee.OvertimeMultiplier,
            compensation.OvertimeEligible,
            Rounding.Money(basePay),
            Rounding.Money(overtimePay),
            Rounding.Money(deferredOvertimePay),
            vacationDays,
            Rounding.Money(vacationPayout));
    }

    private static WeekSummary SummarizeWeek(
        WorkWeek week,
        Employee employee,
        List<TimeEntry> entries,
        List<WeeklyHours> weeklyTotals,
        List<CompensatedTimeOff> daysOff,
        PayrollSettings settings)
    {
        var worked = entries.Sum(e => e.HoursWithin(week.StartsAt, week.EndsAt))
            + weeklyTotals.Where(w => w.Week == week).Sum(w => w.Hours);
        var bookedHours = daysOff
            .Where(t => week.Contains(t.Date))
            .Sum(t => employee.ResolveTimeOffHours(settings.GetTimeOffType(t.TimeOffTypeId)));
        var holidayHours = employee.GetsCompensatedTimeOff
            ? settings.Holidays.Between(week.Start, week.End)
                .Select(h => h.Date).Distinct().Count(employee.IsEmployedOn)
                * employee.ResolveTimeOffHours(settings.HolidayType)
            : 0m;
        var timeOffHours = bookedHours + holidayHours;

        var countedTowardOvertime = worked + (settings.TimeOffCountsTowardOvertime ? timeOffHours : 0m);
        var overtime = Math.Max(0m, countedTowardOvertime - settings.OvertimeThresholdHours);

        decimal? salaryHours = employee.Compensation.Type == CompensationType.Salary
            ? Rounding.Hours(settings.OvertimeThresholdHours * employee.WeekdaysEmployed(week.Start, week.End) / 5m)
            : null;

        return new WeekSummary(week, worked, timeOffHours, overtime, salaryHours);
    }

    /// <summary>
    /// Unused vacation is paid out for the year this period closes, and for the year of the employee's
    /// end date if they leave during this period.
    /// </summary>
    private static int UnusedVacationDaysToPayOut(
        Employee employee, PayPeriod period, PayrollSettings settings, List<CompensatedTimeOff> daysOff)
    {
        if (!employee.GetsCompensatedTimeOff)
            return 0;

        var years = new HashSet<int>();
        if (period.ClosesYear is { } closedYear && employee.IsEmployedOn(new DateOnly(closedYear, 12, 31)))
            years.Add(closedYear);
        if (employee.EndDate is { } lastDay && period.Contains(lastDay))
            years.Add(lastDay.Year);

        return years.Sum(year => employee.VacationBalance(year, settings.VacationType, daysOff).Remaining);
    }
}
