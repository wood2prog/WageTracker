using WageTracker.Domain.Common;

namespace WageTracker.Domain.Payroll;

public enum PayrollRunStatus
{
    Open,
    Locked,
}

/// <summary>
/// Payroll for one pay period. It is locked when the report for the payroll accountant is created,
/// which stores every employee's statement as a snapshot. After that, nothing dated inside the
/// period can change.
/// </summary>
public sealed class PayrollRun
{
    private readonly List<PayStatement> _statements;

    public PayrollRun(PayPeriod period, DateOnly payoutDate)
        : this(Guid.NewGuid(), period, payoutDate, PayrollRunStatus.Open, null, [])
    {
    }

    public PayrollRun(
        Guid id,
        PayPeriod period,
        DateOnly payoutDate,
        PayrollRunStatus status,
        DateTime? lockedAt,
        IEnumerable<PayStatement> statements)
    {
        Id = id;
        Period = period;
        PayoutDate = payoutDate;
        Status = status;
        LockedAt = lockedAt;
        _statements = statements.ToList();
    }

    public Guid Id { get; }

    public PayPeriod Period { get; }

    public DateOnly PayoutDate { get; }

    public PayrollRunStatus Status { get; private set; }

    public bool IsLocked => Status == PayrollRunStatus.Locked;

    public DateTime? LockedAt { get; private set; }

    public IReadOnlyList<PayStatement> Statements => _statements;

    public PayStatement? StatementFor(Guid employeeId) => _statements.SingleOrDefault(s => s.EmployeeId == employeeId);

    /// <summary>Locks the run with the final statements. Called when the payroll report is created.</summary>
    public void Lock(IEnumerable<PayStatement> statements, DateTime lockedAt)
    {
        if (IsLocked)
            throw new DomainException($"Pay period {Period} is already locked.");

        var final = statements.ToList();
        if (final.Any(s => s.Period != Period))
            throw new DomainException($"Every statement must be for pay period {Period}.");
        if (final.GroupBy(s => s.EmployeeId).Any(g => g.Count() > 1))
            throw new DomainException("An employee can have only one statement per pay period.");

        _statements.Clear();
        _statements.AddRange(final);
        Status = PayrollRunStatus.Locked;
        LockedAt = lockedAt;
    }

    /// <summary>Throws if <paramref name="date"/> is inside this period and the period is locked.</summary>
    public void EnsureCanChange(DateOnly date)
    {
        if (IsLocked && Period.Contains(date))
            throw new DomainException($"Pay period {Period} is locked, so {date:yyyy-MM-dd} cannot be changed.");
    }

    /// <summary>Throws if any part of [start, end) is inside this period and the period is locked.</summary>
    public void EnsureCanChange(DateTime start, DateTime end)
    {
        if (IsLocked && start < Period.EndsAt && Period.StartsAt < end)
            throw new DomainException($"Pay period {Period} is locked, so time inside it cannot be changed.");
    }
}
