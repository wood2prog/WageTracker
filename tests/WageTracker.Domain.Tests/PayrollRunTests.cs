using WageTracker.Domain.Common;
using WageTracker.Domain.Payroll;
using static WageTracker.Domain.Tests.TestData;

namespace WageTracker.Domain.Tests;

public class PayrollRunTests
{
    private static (PayrollRun Run, PayStatement Statement) LockedRun()
    {
        var settings = Settings();
        var employee = Hourly();
        var period = settings.Schedule.PeriodContaining(Sunday);
        var statement = PayStatement.Calculate(employee, period, settings, WorkWeek(employee, 8m), [], null);
        var run = new PayrollRun(period, settings.PayoutDateFor(period));
        run.Lock([statement], new DateTime(2026, 9, 14, 9, 0, 0));
        return (run, statement);
    }

    [Fact]
    public void Locking_stores_the_statements()
    {
        var (run, statement) = LockedRun();

        Assert.True(run.IsLocked);
        Assert.Same(statement, run.StatementFor(statement.EmployeeId));
    }

    [Fact]
    public void Dates_inside_a_locked_period_cannot_change()
    {
        var (run, _) = LockedRun();

        Assert.Throws<DomainException>(() => run.EnsureCanChange(Sunday.AddDays(3)));
        run.EnsureCanChange(Sunday.AddDays(7));
    }

    [Fact]
    public void Time_that_reaches_into_a_locked_period_cannot_change()
    {
        var (run, _) = LockedRun();
        var saturdayNight = new DateTime(2026, 9, 12, 22, 0, 0);

        Assert.Throws<DomainException>(() => run.EnsureCanChange(saturdayNight, saturdayNight.AddHours(5)));
        run.EnsureCanChange(saturdayNight.AddHours(2), saturdayNight.AddHours(5));
    }

    [Fact]
    public void An_open_period_can_change()
    {
        var run = new PayrollRun(Settings().Schedule.PeriodContaining(Sunday), new DateOnly(2026, 9, 10));

        run.EnsureCanChange(Sunday.AddDays(3));
    }

    [Fact]
    public void A_run_can_only_be_locked_once()
    {
        var (run, statement) = LockedRun();

        Assert.Throws<DomainException>(() => run.Lock([statement], DateTime.Now));
    }

    [Fact]
    public void Statements_must_be_for_the_runs_period()
    {
        var (_, statement) = LockedRun();
        var nextWeek = new PayrollRun(Settings().Schedule.PeriodContaining(Sunday.AddDays(7)), new DateOnly(2026, 10, 10));

        Assert.Throws<DomainException>(() => nextWeek.Lock([statement], DateTime.Now));
    }
}
