namespace WageTracker.Infrastructure;

/// <summary>Where WageTracker keeps its data and reports.</summary>
public sealed class StorageOptions
{
    /// <summary>
    /// The SQLite database file. Defaults to the local (not roaming or OneDrive-synced) app data folder,
    /// because file sync can corrupt a database that is open.
    /// </summary>
    public string DatabasePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WageTracker", "wagetracker.db");

    /// <summary>The folder payroll report PDFs are written to.</summary>
    public string ReportsFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WageTracker Reports");

    /// <summary>The folder database backups are written to, in Documents so they are easy to find.</summary>
    public string BackupsFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WageTracker Backups");
}
