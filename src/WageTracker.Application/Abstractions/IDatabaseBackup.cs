namespace WageTracker.Application.Abstractions;

/// <summary>Copies the database to the backups folder. Implemented by Infrastructure.</summary>
public interface IDatabaseBackup
{
    /// <summary>
    /// Writes a backup named for <paramref name="timestamp"/>, then deletes the oldest backups so that at most
    /// <paramref name="backupsToKeep"/> remain.
    /// </summary>
    /// <returns>The backup's path, or null if there is no database yet to back up.</returns>
    Task<string?> BackupAsync(DateTime timestamp, int backupsToKeep);
}
