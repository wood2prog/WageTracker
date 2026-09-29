using Microsoft.Data.Sqlite;
using WageTracker.Infrastructure.Persistence;

namespace WageTracker.Infrastructure.Tests;

/// <summary>A fresh database and reports folder in a temp directory, deleted afterward.</summary>
public sealed class TempDatabase : IDisposable
{
    public TempDatabase()
    {
        Folder = Path.Combine(Path.GetTempPath(), "WageTrackerTests", Guid.NewGuid().ToString("N"));
        Options = new StorageOptions
        {
            DatabasePath = Path.Combine(Folder, "test.db"),
            ReportsFolder = Path.Combine(Folder, "Reports"),
            BackupsFolder = Path.Combine(Folder, "Backups"),
        };
        Factory = new SqliteConnectionFactory(Options);
    }

    /// <summary>A valid 1×1 PNG.</summary>
    public static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    public string Folder { get; }

    public StorageOptions Options { get; }

    public SqliteConnectionFactory Factory { get; }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(Folder))
            Directory.Delete(Folder, recursive: true);
    }
}
