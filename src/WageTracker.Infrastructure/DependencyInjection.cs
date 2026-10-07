using Microsoft.Extensions.DependencyInjection;
using WageTracker.Application.Abstractions;
using WageTracker.Infrastructure.Persistence;
using WageTracker.Infrastructure.Reports;

namespace WageTracker.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the SQLite repositories, the database backup, and the PDF report writers. The database is created and migrated
    /// the first time it is opened.
    /// </summary>
    public static IServiceCollection AddWageTrackerInfrastructure(this IServiceCollection services, Action<StorageOptions>? configure = null)
    {
        var options = new StorageOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<IEmployeeRepository, SqliteEmployeeRepository>();
        services.AddSingleton<ITimeEntryRepository, SqliteTimeEntryRepository>();
        services.AddSingleton<IWeeklyHoursRepository, SqliteWeeklyHoursRepository>();
        services.AddSingleton<ITimeOffRepository, SqliteTimeOffRepository>();
        services.AddSingleton<IPayrollSettingsRepository, SqlitePayrollSettingsRepository>();
        services.AddSingleton<IPayrollRunRepository, SqlitePayrollRunRepository>();
        services.AddSingleton<IPayrollReportWriter, PdfPayrollReportWriter>();
        services.AddSingleton<IHoursReportWriter, PdfHoursReportWriter>();
        services.AddSingleton<IDatabaseBackup, SqliteDatabaseBackup>();
        return services;
    }
}
