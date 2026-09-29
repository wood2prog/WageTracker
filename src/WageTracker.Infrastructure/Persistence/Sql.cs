using System.Globalization;
using Microsoft.Data.Sqlite;

namespace WageTracker.Infrastructure.Persistence;

/// <summary>Small helpers for running SQL and converting values to and from the storage conventions in <see cref="Schema"/>.</summary>
internal static class Sql
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffff";

    public static object? Scalar(this SqliteConnection connection, string sql)
    {
        using var command = Command(connection, null, sql, []);
        return command.ExecuteScalar();
    }

    public static int Execute(this SqliteConnection connection, SqliteTransaction? tx, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = Command(connection, tx, sql, parameters);
        return command.ExecuteNonQuery();
    }

    public static async Task<List<T>> QueryAsync<T>(
        this SqliteConnection connection, string sql, Func<SqliteDataReader, T> map, params (string Name, object? Value)[] parameters)
    {
        using var command = Command(connection, null, sql, parameters);
        using var reader = await command.ExecuteReaderAsync();
        var rows = new List<T>();
        while (await reader.ReadAsync())
            rows.Add(map(reader));
        return rows;
    }

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? tx, string sql, (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = tx;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, ToDb(value));
        return command;
    }

    private static object ToDb(object? value) => value switch
    {
        null => DBNull.Value,
        Guid g => g.ToString("D"),
        DateOnly d => d.ToString(DateFormat, CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString(DateTimeFormat, CultureInfo.InvariantCulture),
        decimal m => m.ToString(CultureInfo.InvariantCulture),
        bool b => b ? 1 : 0,
        Enum e => e.ToString(),
        _ => value,
    };

    public static string Text(this SqliteDataReader r, string column) => r.GetString(r.GetOrdinal(column));

    public static Guid Guid(this SqliteDataReader r, string column) => System.Guid.Parse(r.Text(column));

    public static int Int(this SqliteDataReader r, string column) => r.GetInt32(r.GetOrdinal(column));

    public static int? IntOrNull(this SqliteDataReader r, string column) =>
        r.IsDBNull(r.GetOrdinal(column)) ? null : r.Int(column);

    public static bool Bool(this SqliteDataReader r, string column) => r.Int(column) != 0;

    public static decimal Decimal(this SqliteDataReader r, string column) =>
        decimal.Parse(r.Text(column), NumberStyles.Number, CultureInfo.InvariantCulture);

    public static DateOnly Date(this SqliteDataReader r, string column) =>
        DateOnly.ParseExact(r.Text(column), DateFormat, CultureInfo.InvariantCulture);

    public static DateOnly? DateOrNull(this SqliteDataReader r, string column) =>
        r.IsDBNull(r.GetOrdinal(column)) ? null : r.Date(column);

    public static DateTime DateTime(this SqliteDataReader r, string column) =>
        System.DateTime.ParseExact(r.Text(column), DateTimeFormat, CultureInfo.InvariantCulture);

    public static DateTime? DateTimeOrNull(this SqliteDataReader r, string column) =>
        r.IsDBNull(r.GetOrdinal(column)) ? null : r.DateTime(column);

    public static string? TextOrNull(this SqliteDataReader r, string column) =>
        r.IsDBNull(r.GetOrdinal(column)) ? null : r.Text(column);

    public static byte[]? BlobOrNull(this SqliteDataReader r, string column) =>
        r.IsDBNull(r.GetOrdinal(column)) ? null : (byte[])r.GetValue(r.GetOrdinal(column));

    public static T Enum<T>(this SqliteDataReader r, string column) where T : struct, Enum =>
        System.Enum.Parse<T>(r.Text(column));
}
