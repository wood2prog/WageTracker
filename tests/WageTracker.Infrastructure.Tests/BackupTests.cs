using Microsoft.Data.Sqlite;
using WageTracker.Domain.Payroll;
using WageTracker.Infrastructure.Persistence;

namespace WageTracker.Infrastructure.Tests;

public sealed class BackupTests : IDisposable
{
    private static readonly DateTime Timestamp = new(2026, 9, 29, 17, 30, 5);

    private readonly TempDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private SqliteDatabaseBackup Backup => new(_db.Options, _db.Factory);

    private async Task SaveSettingsAsync(string companyName)
    {
        var settings = PayrollSettings.CreateDefault(PayPeriodSchedule.Weekly(), 10);
        settings.SetCompanyName(companyName);
        await new SqlitePayrollSettingsRepository(_db.Factory).SaveAsync(settings);
    }

    [Fact]
    public async Task Nothing_to_back_up_before_the_database_exists() =>
        Assert.Null(await Backup.BackupAsync(Timestamp, 10));

    [Fact]
    public async Task A_backup_is_a_dated_working_copy_of_the_database()
    {
        await SaveSettingsAsync("Acme Tools");

        var path = await Backup.BackupAsync(Timestamp, 10);

        Assert.Equal(Path.Combine(_db.Options.BackupsFolder, "WageTracker 2026-09-29 17-30-05.db"), path);
        var restored = new SqliteConnectionFactory(new StorageOptions { DatabasePath = path! });
        var settings = await new SqlitePayrollSettingsRepository(restored).GetAsync();
        Assert.Equal("Acme Tools", settings!.CompanyName);
    }

    [Fact]
    public async Task Only_the_newest_backups_are_kept()
    {
        await SaveSettingsAsync("Acme Tools");

        for (var i = 0; i < 5; i++)
            await Backup.BackupAsync(Timestamp.AddDays(i), 3);

        var names = Directory.GetFiles(_db.Options.BackupsFolder).Select(Path.GetFileName).Order();
        Assert.Equal(
            ["WageTracker 2026-10-01 17-30-05.db", "WageTracker 2026-10-02 17-30-05.db", "WageTracker 2026-10-03 17-30-05.db"],
            names);
    }

    [Fact]
    public async Task Other_files_in_the_backups_folder_are_never_deleted()
    {
        await SaveSettingsAsync("Acme Tools");
        Directory.CreateDirectory(_db.Options.BackupsFolder);
        var keepMe = Path.Combine(_db.Options.BackupsFolder, "WageTracker before upgrade.db");
        File.WriteAllText(keepMe, "mine");

        await Backup.BackupAsync(Timestamp, 1);
        await Backup.BackupAsync(Timestamp.AddDays(1), 1);

        Assert.True(File.Exists(keepMe));
        Assert.Equal(2, Directory.GetFiles(_db.Options.BackupsFolder).Length);
    }

    [Fact]
    public async Task Settings_round_trip_company_details_and_backup_count()
    {
        var repo = new SqlitePayrollSettingsRepository(_db.Factory);
        var settings = PayrollSettings.CreateDefault(PayPeriodSchedule.Weekly(), 10);
        settings.SetCompanyName("Acme Tools");
        settings.SetCompanyLogo(TempDatabase.Png);
        settings.SetBackupsToKeep(30);
        await repo.SaveAsync(settings);

        var loaded = (await repo.GetAsync())!;

        Assert.Equal("Acme Tools", loaded.CompanyName);
        Assert.Equal(TempDatabase.Png, loaded.CompanyLogo);
        Assert.Equal(30, loaded.BackupsToKeep);
    }

    [Fact]
    public async Task A_version_1_database_is_upgraded_with_the_default_backup_count()
    {
        Directory.CreateDirectory(_db.Folder);
        await using (var v1 = new SqliteConnection($"Data Source={_db.Options.DatabasePath};Pooling=False"))
        {
            await v1.OpenAsync();
            using var command = v1.CreateCommand();
            command.CommandText = Schema.Migrations[0] + """
                ;
                PRAGMA user_version = 1;
                INSERT INTO settings VALUES (1, 'Weekly', NULL, NULL, 10, '40', 1);
                INSERT INTO time_off_types VALUES ('00000000-0000-0000-0000-000000000001', 'Holiday', '8', 'Holiday', 0, 0);
                INSERT INTO time_off_types VALUES ('00000000-0000-0000-0000-000000000002', 'Vacation', '10', 'Vacation', 0, 1);
                """;
            command.ExecuteNonQuery();
        }

        var loaded = (await new SqlitePayrollSettingsRepository(_db.Factory).GetAsync())!;

        Assert.Equal(PayrollSettings.DefaultBackupsToKeep, loaded.BackupsToKeep);
        Assert.Null(loaded.CompanyName);
    }
}
