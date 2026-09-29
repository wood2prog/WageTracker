using Microsoft.Data.Sqlite;
using WageTracker.Application.Abstractions;
using WageTracker.Domain.TimeOff;

namespace WageTracker.Infrastructure.Persistence;

public sealed class SqliteTimeOffRepository(SqliteConnectionFactory db) : ITimeOffRepository
{
    public async Task<CompensatedTimeOff?> GetAsync(Guid id)
    {
        await using var connection = await db.OpenAsync();
        return (await connection.QueryAsync("SELECT * FROM time_off WHERE id = $id", Map, ("$id", id))).SingleOrDefault();
    }

    public async Task<IReadOnlyList<CompensatedTimeOff>> ListForEmployeeAsync(Guid employeeId, DateOnly from, DateOnly to)
    {
        await using var connection = await db.OpenAsync();
        return await connection.QueryAsync(
            "SELECT * FROM time_off WHERE employee_id = $e AND date BETWEEN $from AND $to ORDER BY date",
            Map, ("$e", employeeId), ("$from", from), ("$to", to));
    }

    public async Task AddAsync(CompensatedTimeOff timeOff)
    {
        await using var connection = await db.OpenAsync();
        connection.Execute(null, "INSERT INTO time_off (id, employee_id, date, time_off_type_id) VALUES ($id, $e, $date, $type)",
            ("$id", timeOff.Id), ("$e", timeOff.EmployeeId), ("$date", timeOff.Date), ("$type", timeOff.TimeOffTypeId));
    }

    public async Task RemoveAsync(Guid id)
    {
        await using var connection = await db.OpenAsync();
        connection.Execute(null, "DELETE FROM time_off WHERE id = $id", ("$id", id));
    }

    private static CompensatedTimeOff Map(SqliteDataReader r) =>
        new(r.Guid("id"), r.Guid("employee_id"), r.Date("date"), r.Guid("time_off_type_id"));
}
