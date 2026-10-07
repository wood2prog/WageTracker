namespace WageTracker.Application.Reports;

/// <summary>
/// A report the user can create for a pay period, chosen from the list on the Payroll tab. To add one,
/// implement this and register it in <see cref="DependencyInjection.AddWageTrackerApplication"/>; the list
/// shows the reports in the order they are registered.
/// </summary>
public interface IPeriodReport
{
    /// <summary>The name shown in the list. It identifies the report, so it must be unique.</summary>
    string Name { get; }

    /// <summary>True if the report can only be created once the period is finalized.</summary>
    bool RequiresFinalizedPeriod { get; }

    /// <summary>Creates the report for the pay period containing <paramref name="date"/>.</summary>
    /// <returns>Where the report was written, such as a file path, to show the user.</returns>
    Task<string> CreateAsync(DateOnly date);
}
