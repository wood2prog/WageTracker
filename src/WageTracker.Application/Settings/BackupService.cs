using WageTracker.Application.Abstractions;
using WageTracker.Domain.Payroll;

namespace WageTracker.Application.Settings;

/// <summary>Backs up the database. The UI calls <see cref="BackupAsync"/> when the app exits.</summary>
public sealed class BackupService(IPayrollSettingsRepository settings, IDatabaseBackup backup, IClock clock)
{
    /// <summary>
    /// Backs up the database and keeps only as many backups as the settings allow
    /// (<see cref="PayrollSettings.DefaultBackupsToKeep"/> before settings are set up).
    /// </summary>
    /// <returns>The backup's path, or null if there was nothing to back up.</returns>
    public async Task<string?> BackupAsync()
    {
        var keep = (await settings.GetAsync())?.BackupsToKeep ?? PayrollSettings.DefaultBackupsToKeep;
        return await backup.BackupAsync(clock.Now, keep);
    }
}
