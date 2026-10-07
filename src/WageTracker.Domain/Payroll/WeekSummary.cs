using WageTracker.Domain.Calendar;

namespace WageTracker.Domain.Payroll;

/// <summary>An employee's hours for one work week.</summary>
/// <param name="WorkedHours">Hours from time entries that fall inside the week.</param>
/// <param name="TimeOffHours">Hours credited for compensated time off during the week.</param>
/// <param name="OvertimeHours">Hours over the weekly threshold, paid at the overtime rate.</param>
/// <param name="SalaryHours">
/// For salary, the hours the week's salary covers: a fifth of the overtime threshold for each weekday employed,
/// whatever hours were recorded. Null for hourly.
/// </param>
public sealed record WeekSummary(WorkWeek Week, decimal WorkedHours, decimal TimeOffHours, decimal OvertimeHours, decimal? SalaryHours = null)
{
    public decimal TotalHours => WorkedHours + TimeOffHours;

    /// <summary>Hours paid at the regular rate.</summary>
    public decimal RegularHours => TotalHours - OvertimeHours;

    /// <summary>The hours the base pay covers: the salary hours for salary, otherwise the regular hours.</summary>
    public decimal CompensatedRegularHours => SalaryHours ?? RegularHours;
}
