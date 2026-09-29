using WageTracker.Domain.Calendar;

namespace WageTracker.Domain.Payroll;

/// <summary>An employee's hours for one work week.</summary>
/// <param name="WorkedHours">Hours from time entries that fall inside the week.</param>
/// <param name="TimeOffHours">Hours credited for compensated time off during the week.</param>
/// <param name="OvertimeHours">Hours over the weekly threshold, paid at the overtime rate.</param>
public sealed record WeekSummary(WorkWeek Week, decimal WorkedHours, decimal TimeOffHours, decimal OvertimeHours)
{
    public decimal TotalHours => WorkedHours + TimeOffHours;

    /// <summary>Hours paid at the regular rate.</summary>
    public decimal RegularHours => TotalHours - OvertimeHours;
}
