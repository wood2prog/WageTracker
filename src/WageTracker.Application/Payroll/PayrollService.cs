using WageTracker.Application.Abstractions;
using WageTracker.Application.Common;
using WageTracker.Domain.Common;
using WageTracker.Domain.Employees;
using WageTracker.Domain.Payroll;

namespace WageTracker.Application.Payroll;

public sealed class PayrollService(
    IEmployeeRepository employees,
    ITimeEntryRepository timeEntries,
    IWeeklyHoursRepository weeklyTotals,
    ITimeOffRepository timeOff,
    IPayrollSettingsRepository settings,
    IPayrollRunRepository runs,
    IPayrollReportWriter reportWriter,
    IClock clock)
{
    /// <summary>
    /// The payroll for the period containing <paramref name="date"/>: the stored statements if the
    /// period is locked, otherwise a preview calculated from the current data.
    /// </summary>
    public async Task<PayrollRunDto> GetRunAsync(DateOnly date)
    {
        var current = await settings.RequireAsync();
        var staff = await employees.ListAsync();

        if (await LockedRunContainingAsync(date) is { } locked)
            return ToDto(locked, staff);

        var period = current.PeriodContaining(date);
        var statements = await CalculateAllAsync(staff, period, current);
        return new PayrollRunDto(period.Start, period.End, period.WeekCount, current.PayoutDateFor(period),
            false, null, ToDtos(statements, staff));
    }

    /// <summary>
    /// Calculates every employee's statement for the period containing <paramref name="date"/>, locks the
    /// period, and writes the report for the payroll accountant. The period must have ended, and periods
    /// are finalized in order.
    /// </summary>
    public async Task<FinalizeResult> FinalizeAsync(DateOnly date)
    {
        var current = await settings.RequireAsync();
        if (await LockedRunContainingAsync(date) is { } alreadyLocked)
            throw new DomainException($"Pay period {alreadyLocked.Period} is already locked.");

        var period = current.PeriodContaining(date);
        if (clock.Today <= period.End)
            throw new DomainException($"Pay period {period} has not ended yet.");
        if (await runs.GetLatestLockedAsync() is { } latest && latest.Period.End.AddDays(1) != period.Start)
            throw new DomainException(
                $"Pay periods are finalized in order. Finalize the period starting {latest.Period.End.AddDays(1):yyyy-MM-dd} first.");

        var staff = await employees.ListAsync();
        var run = new PayrollRun(period, current.PayoutDateFor(period));
        run.Lock(await CalculateAllAsync(staff, period, current), clock.Now);
        await runs.AddAsync(run);

        var dto = ToDto(run, staff);
        return new FinalizeResult(dto, await reportWriter.WriteAsync(Report(dto, current)));
    }

    /// <summary>Writes the report again for a locked period, for example if the first copy was lost.</summary>
    public async Task<string> ExportReportAsync(DateOnly date)
    {
        var current = await settings.RequireAsync();
        var run = await LockedRunContainingAsync(date)
            ?? throw new DomainException($"The pay period containing {date:yyyy-MM-dd} has not been finalized yet.");
        return await reportWriter.WriteAsync(Report(ToDto(run, await employees.ListAsync()), current));
    }

    private static PayrollReport Report(PayrollRunDto run, PayrollSettings current) =>
        new(run, current.CompanyName, current.CompanyLogo);

    private async Task<PayrollRun?> LockedRunContainingAsync(DateOnly date) =>
        (await runs.ListLockedOverlappingAsync(date, date)).SingleOrDefault();

    /// <summary>Statements for everyone employed on at least one day of the period.</summary>
    private async Task<List<PayStatement>> CalculateAllAsync(IEnumerable<Employee> staff, PayPeriod period, PayrollSettings current)
    {
        var statements = new List<PayStatement>();
        foreach (var employee in staff.Where(e => e.IsEmployedDuring(period.Start, period.End)))
            statements.Add(await CalculateAsync(employee, period, current, await PreviousStatementAsync(employee, period, current)));
        return statements;
    }

    private async Task<PayStatement> CalculateAsync(Employee employee, PayPeriod period, PayrollSettings current, PayStatement? previous)
    {
        var entries = await timeEntries.ListForEmployeeAsync(employee.Id, period.StartsAt, period.EndsAt);
        var totals = await weeklyTotals.ListForEmployeeAsync(employee.Id, period.Start, period.End);
        // From January 1 so the vacation payout can count the days used that year.
        var daysOff = await timeOff.ListForEmployeeAsync(employee.Id, new DateOnly(period.Start.Year, 1, 1), period.End);
        return PayStatement.Calculate(employee, period, current, entries, daysOff, previous, totals);
    }

    /// <summary>
    /// The statement for the period just before, which carries deferred salaried overtime. A locked
    /// period's stored statement is used when there is one; otherwise it is calculated. Its own previous
    /// statement is not needed, because deferred overtime depends only on that period's hours.
    /// </summary>
    private async Task<PayStatement?> PreviousStatementAsync(Employee employee, PayPeriod period, PayrollSettings current)
    {
        var dayBefore = period.Start.AddDays(-1);
        if (await runs.GetEndingOnAsync(dayBefore) is { IsLocked: true } locked)
            return locked.StatementFor(employee.Id);
        if (dayBefore < current.ScheduleEffectiveFrom || !employee.IsEmployedOn(dayBefore))
            return null;

        return await CalculateAsync(employee, current.PeriodContaining(dayBefore), current, previous: null);
    }

    private static PayrollRunDto ToDto(PayrollRun run, IEnumerable<Employee> staff) =>
        new(run.Period.Start, run.Period.End, run.Period.WeekCount, run.PayoutDate, run.IsLocked, run.LockedAt,
            ToDtos(run.Statements, staff));

    private static List<PayStatementDto> ToDtos(IEnumerable<PayStatement> statements, IEnumerable<Employee> staff)
    {
        var names = staff.ToDictionary(e => e.Id, e => e.FullName);
        return statements
            .Select(s => PayStatementDto.From(s, names.GetValueOrDefault(s.EmployeeId, "(unknown employee)")))
            .OrderBy(s => s.EmployeeName)
            .ToList();
    }
}
