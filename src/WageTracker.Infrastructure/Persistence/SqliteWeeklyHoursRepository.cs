using Microsoft.Data.Sqlite;
using WageTracker.Application.Abstractions;
using WageTracker.Domain.Calendar;
using WageTracker.Domain.TimeTracking;

namespace WageTracker.Infrastructure.Persistence;

public sealed class SqliteWeeklyHoursRepository(SqliteConnectionFactory db) : IWeeklyHoursRepository
{
    public async Task<WeeklyHours?> GetAsync(Guid employeeId, DateOnly weekStart)
    {
        await using var connection = await db.OpenAsync();
        return (await connection.QueryAsync(
            "SELECT * FROM weekly_hours WHERE employee_id = $e AND week_start = $week",
            Map, ("$e", employeeId), ("$week", weekStart))).SingleOrDefault();
    }

    public async Task<IReadOnlyList<WeeklyHours>> ListForEmployeeAsync(Guid employeeId, DateOnly from, DateOnly to)
    {
        await using var connection = await db.OpenAsync();
        return await connection.QueryAsync(
            "SELECT * FROM weekly_hours WHERE employee_id = $e AND week_start BETWEEN $from AND $to ORDER BY week_start",
            Map, ("$e", employeeId), ("$from", from), ("$to", to));
    }

    public async Task SaveAsync(WeeklyHours hours)
    {
        await using var connection = await db.OpenAsync();
        connection.Execute(null, """
            INSERT INTO weekly_hours (employee_id, week_start, hours) VALUES ($e, $week, $hours)
            ON CONFLICT (employee_id, week_start) DO UPDATE SET hours = excluded.hours
            """, ("$e", hours.EmployeeId), ("$week", hours.Week.Start), ("$hours", hours.Hours));
    }

    public async Task RemoveAsync(Guid employeeId, DateOnly weekStart)
    {
        await using var connection = await db.OpenAsync();
        connection.Execute(null, "DELETE FROM weekly_hours WHERE employee_id = $e AND week_start = $week",
            ("$e", employeeId), ("$week", weekStart));
    }

    private static WeeklyHours Map(SqliteDataReader r) =>
        new(r.Guid("employee_id"), new WorkWeek(r.Date("week_start")), r.Decimal("hours"));
}
