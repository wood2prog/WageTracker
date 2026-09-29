using Microsoft.Data.Sqlite;
using WageTracker.Application.Abstractions;
using WageTracker.Domain.Calendar;
using WageTracker.Domain.Employees;
using WageTracker.Domain.Payroll;

namespace WageTracker.Infrastructure.Persistence;

/// <summary>Saves payroll runs with their statements, which are the snapshot kept when a period is locked.</summary>
public sealed class SqlitePayrollRunRepository(SqliteConnectionFactory db) : IPayrollRunRepository
{
    public async Task<PayrollRun?> GetByPeriodStartAsync(DateOnly start) =>
        (await LoadAsync("WHERE period_start = $d", ("$d", start))).SingleOrDefault();

    public async Task<PayrollRun?> GetEndingOnAsync(DateOnly end) =>
        (await LoadAsync("WHERE period_end = $d", ("$d", end))).SingleOrDefault();

    public async Task<IReadOnlyList<PayrollRun>> ListLockedOverlappingAsync(DateOnly from, DateOnly to) =>
        await LoadAsync("WHERE status = 'Locked' AND period_start <= $to AND period_end >= $from", ("$from", from), ("$to", to));

    public async Task<PayrollRun?> GetLatestLockedAsync() =>
        (await LoadAsync("WHERE status = 'Locked' ORDER BY period_start DESC LIMIT 1")).SingleOrDefault();

    public async Task AddAsync(PayrollRun run)
    {
        await using var connection = await db.OpenAsync();
        using var tx = connection.BeginTransaction();

        connection.Execute(tx, """
            INSERT INTO payroll_runs (id, period_start, period_end, week_count, payout_date, status, locked_at)
            VALUES ($id, $start, $end, $weeks, $payout, $status, $lockedAt)
            """,
            ("$id", run.Id), ("$start", run.Period.Start), ("$end", run.Period.End), ("$weeks", run.Period.WeekCount),
            ("$payout", run.PayoutDate), ("$status", run.Status), ("$lockedAt", run.LockedAt));

        foreach (var s in run.Statements)
        {
            connection.Execute(tx, """
                INSERT INTO pay_statements (run_id, employee_id, compensation_type, hourly_rate, overtime_multiplier,
                    base_pay, overtime_pay, deferred_overtime_pay, vacation_days_paid_out, vacation_payout)
                VALUES ($run, $e, $type, $rate, $multiplier, $base, $ot, $deferred, $vacationDays, $vacationPay)
                """,
                ("$run", run.Id), ("$e", s.EmployeeId), ("$type", s.CompensationType), ("$rate", s.HourlyRate),
                ("$multiplier", s.OvertimeMultiplier), ("$base", s.BasePay), ("$ot", s.OvertimePay),
                ("$deferred", s.DeferredOvertimePay), ("$vacationDays", s.VacationDaysPaidOut), ("$vacationPay", s.VacationPayout));

            foreach (var w in s.Weeks)
            {
                connection.Execute(tx, """
                    INSERT INTO pay_statement_weeks (run_id, employee_id, week_start, worked_hours, time_off_hours, overtime_hours)
                    VALUES ($run, $e, $week, $worked, $timeOff, $ot)
                    """,
                    ("$run", run.Id), ("$e", s.EmployeeId), ("$week", w.Week.Start), ("$worked", w.WorkedHours),
                    ("$timeOff", w.TimeOffHours), ("$ot", w.OvertimeHours));
            }
        }
        tx.Commit();
    }

    private async Task<List<PayrollRun>> LoadAsync(string whereAndOrder, params (string, object?)[] parameters)
    {
        await using var connection = await db.OpenAsync();
        var runs = await connection.QueryAsync($"SELECT * FROM payroll_runs {whereAndOrder}", r => new
        {
            Id = r.Guid("id"),
            Period = new PayPeriod(new WorkWeek(r.Date("period_start")), r.Int("week_count")),
            PayoutDate = r.Date("payout_date"),
            Status = r.Enum<PayrollRunStatus>("status"),
            LockedAt = r.DateTimeOrNull("locked_at"),
        }, parameters);

        var result = new List<PayrollRun>();
        foreach (var run in runs)
        {
            var statements = await LoadStatementsAsync(connection, run.Id, run.Period, run.PayoutDate);
            result.Add(new PayrollRun(run.Id, run.Period, run.PayoutDate, run.Status, run.LockedAt, statements));
        }
        return result;
    }

    private static async Task<List<PayStatement>> LoadStatementsAsync(
        SqliteConnection connection, Guid runId, PayPeriod period, DateOnly payoutDate)
    {
        var weeks = (await connection.QueryAsync("SELECT * FROM pay_statement_weeks WHERE run_id = $run ORDER BY week_start",
                r => (EmployeeId: r.Guid("employee_id"), Week: new WeekSummary(
                    new WorkWeek(r.Date("week_start")), r.Decimal("worked_hours"), r.Decimal("time_off_hours"), r.Decimal("overtime_hours"))),
                ("$run", runId)))
            .ToLookup(w => w.EmployeeId, w => w.Week);

        return await connection.QueryAsync("SELECT * FROM pay_statements WHERE run_id = $run", r =>
        {
            var employeeId = r.Guid("employee_id");
            return new PayStatement(
                employeeId,
                period,
                payoutDate,
                weeks[employeeId].ToList(),
                r.Enum<CompensationType>("compensation_type"),
                r.Decimal("hourly_rate"),
                r.Decimal("overtime_multiplier"),
                r.Decimal("base_pay"),
                r.Decimal("overtime_pay"),
                r.Decimal("deferred_overtime_pay"),
                r.Int("vacation_days_paid_out"),
                r.Decimal("vacation_payout"));
        }, ("$run", runId));
    }
}
