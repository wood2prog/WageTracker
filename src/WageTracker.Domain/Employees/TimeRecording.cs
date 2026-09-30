namespace WageTracker.Domain.Employees;

/// <summary>How an employee's hours worked are entered.</summary>
public enum TimeRecording
{
    /// <summary>Each stretch of work is a <see cref="TimeTracking.TimeEntry"/> with a start and end.</summary>
    Daily,

    /// <summary>One total per work week, a <see cref="TimeTracking.WeeklyHours"/>.</summary>
    Weekly,
}
