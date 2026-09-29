using Microsoft.Data.Sqlite;

namespace WageTracker.Infrastructure.Persistence;

/// <summary>Opens connections to the database, creating and migrating it on first use.</summary>
public sealed class SqliteConnectionFactory(StorageOptions options)
{
    private readonly Lock _migrationLock = new();
    private bool _migrated;

    private string ConnectionString { get; } = new SqliteConnectionStringBuilder
    {
        DataSource = options.DatabasePath,
        ForeignKeys = true,
    }.ToString();

    public async Task<SqliteConnection> OpenAsync()
    {
        if (Path.GetDirectoryName(options.DatabasePath) is { Length: > 0 } folder)
            Directory.CreateDirectory(folder);

        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        EnsureMigrated(connection);
        return connection;
    }

    private void EnsureMigrated(SqliteConnection connection)
    {
        lock (_migrationLock)
        {
            if (_migrated)
                return;

            var version = Convert.ToInt32(connection.Scalar("PRAGMA user_version"));
            if (version > Schema.Migrations.Length)
                throw new InvalidOperationException(
                    $"The database was created by a newer version of WageTracker (schema {version}).");

            for (var i = version; i < Schema.Migrations.Length; i++)
            {
                using var tx = connection.BeginTransaction();
                connection.Execute(tx, Schema.Migrations[i]);
                connection.Execute(tx, $"PRAGMA user_version = {i + 1}");
                tx.Commit();
            }
            _migrated = true;
        }
    }
}
