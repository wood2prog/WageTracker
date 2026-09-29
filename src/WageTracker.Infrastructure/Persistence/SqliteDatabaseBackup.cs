using System.Globalization;
using Microsoft.Data.Sqlite;
using WageTracker.Application.Abstractions;

namespace WageTracker.Infrastructure.Persistence;

/// <summary>
/// Backs up the database with SQLite's online backup, which gives a consistent copy even while the database
/// is open. Backups are named "WageTracker yyyy-MM-dd HH-mm-ss.db"; only files matching that name are ever pruned.
/// </summary>
public sealed class SqliteDatabaseBackup(StorageOptions options, SqliteConnectionFactory db) : IDatabaseBackup
{
    private const string Prefix = "WageTracker ";
    private const string TimestampFormat = "yyyy-MM-dd HH-mm-ss";
    private const string Extension = ".db";

    public async Task<string?> BackupAsync(DateTime timestamp, int backupsToKeep)
    {
        if (!File.Exists(options.DatabasePath))
            return null;

        Directory.CreateDirectory(options.BackupsFolder);
        var path = Path.Combine(options.BackupsFolder,
            Prefix + timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture) + Extension);

        await using (var source = await db.OpenAsync())
        await using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false, // release the file as soon as the backup is done
        }.ToString()))
        {
            await destination.OpenAsync();
            source.BackupDatabase(destination);
        }

        Prune(backupsToKeep);
        return path;
    }

    private void Prune(int backupsToKeep)
    {
        var backups = Directory.GetFiles(options.BackupsFolder, Prefix + "*" + Extension)
            .Where(IsBackupName)
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal); // the timestamp format sorts by name

        foreach (var old in backups.Skip(backupsToKeep))
            File.Delete(old);
    }

    private static bool IsBackupName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.StartsWith(Prefix, StringComparison.Ordinal)
            && DateTime.TryParseExact(name[Prefix.Length..], TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }
}
