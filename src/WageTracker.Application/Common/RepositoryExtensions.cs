using WageTracker.Application.Abstractions;
using WageTracker.Domain.Employees;
using WageTracker.Domain.Payroll;

namespace WageTracker.Application.Common;

internal static class RepositoryExtensions
{
    public static async Task<Employee> RequireAsync(this IEmployeeRepository employees, Guid id) =>
        await employees.GetAsync(id) ?? throw new NotFoundException("That employee no longer exists.");

    public static async Task<PayrollSettings> RequireAsync(this IPayrollSettingsRepository settings) =>
        await settings.GetAsync() ?? throw new SettingsNotConfiguredException();

    /// <summary>Loads the settings, applies <paramref name="change"/>, and saves them.</summary>
    public static async Task<PayrollSettings> ChangeAsync(this IPayrollSettingsRepository repository, Action<PayrollSettings> change)
    {
        var settings = await repository.RequireAsync();
        change(settings);
        await repository.SaveAsync(settings);
        return settings;
    }

    /// <summary>Throws if <paramref name="date"/> is in a locked pay period.</summary>
    public static async Task EnsureCanChangeAsync(this IPayrollRunRepository runs, DateOnly date)
    {
        foreach (var run in await runs.ListLockedOverlappingAsync(date, date))
            run.EnsureCanChange(date);
    }

    /// <summary>Throws if any part of [start, end) is in a locked pay period.</summary>
    public static async Task EnsureCanChangeAsync(this IPayrollRunRepository runs, DateTime start, DateTime end)
    {
        foreach (var run in await runs.ListLockedOverlappingAsync(DateOnly.FromDateTime(start), DateOnly.FromDateTime(end)))
            run.EnsureCanChange(start, end);
    }
}
