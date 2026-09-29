using Microsoft.Data.Sqlite;
using WageTracker.Application.Abstractions;
using WageTracker.Domain.TimeTracking;

namespace WageTracker.Infrastructure.Persistence;

public sealed class SqliteTimeEntryRepository(SqliteConnectionFactory db) : ITimeEntryRepository
{
    public async Task<TimeEntry?> GetAsync(Guid id)
    {
        await using var connection = await db.OpenAsync();
        return (await connection.QueryAsync("SELECT * FROM time_entries WHERE id = $id", Map, ("$id", id))).SingleOrDefault();
    }

    public async Task<IReadOnlyList<TimeEntry>> ListForEmployeeAsync(Guid employeeId, DateTime from, DateTime to)
    {
        await using var connection = await db.OpenAsync();
        return await connection.QueryAsync(
            """SELECT * FROM time_entries WHERE employee_id = $e AND start < $to AND "end" > $from ORDER BY start""",
            Map, ("$e", employeeId), ("$from", from), ("$to", to));
    }

    public async Task AddAsync(TimeEntry entry)
    {
        await using var connection = await db.OpenAsync();
        connection.Execute(null, """INSERT INTO time_entries (id, employee_id, start, "end") VALUES ($id, $e, $start, $end)""",
            ("$id", entry.Id), ("$e", entry.EmployeeId), ("$start", entry.Start), ("$end", entry.End));
    }

    public async Task UpdateAsync(TimeEntry entry)
    {
        await using var connection = await db.OpenAsync();
        connection.Execute(null, """UPDATE time_entries SET start = $start, "end" = $end WHERE id = $id""",
            ("$id", entry.Id), ("$start", entry.Start), ("$end", entry.End));
    }

    public async Task RemoveAsync(Guid id)
    {
        await using var connection = await db.OpenAsync();
        connection.Execute(null, "DELETE FROM time_entries WHERE id = $id", ("$id", id));
    }

    private static TimeEntry Map(SqliteDataReader r) =>
        new(r.Guid("id"), r.Guid("employee_id"), r.DateTime("start"), r.DateTime("end"));
}
