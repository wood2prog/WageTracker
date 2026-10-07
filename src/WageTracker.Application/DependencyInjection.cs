using Microsoft.Extensions.DependencyInjection;
using WageTracker.Application.Abstractions;
using WageTracker.Application.Employees;
using WageTracker.Application.Payroll;
using WageTracker.Application.Reports;
using WageTracker.Application.Settings;
using WageTracker.Application.TimeOff;
using WageTracker.Application.TimeTracking;

namespace WageTracker.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the use-case services and the system clock. Infrastructure registers the repositories,
    /// the report writer, and the database backup.
    /// </summary>
    public static IServiceCollection AddWageTrackerApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddTransient<EmployeeService>();
        services.AddTransient<TimeEntryService>();
        services.AddTransient<TimeOffService>();
        services.AddTransient<SettingsService>();
        services.AddTransient<HolidayService>();
        services.AddTransient<BackupService>();
        services.AddTransient<PayrollService>();
        services.AddTransient<ReportService>();

        // The pay period reports, in the order the Payroll tab lists them.
        services.AddTransient<IPeriodReport, PayrollPeriodReport>();
        return services;
    }
}
