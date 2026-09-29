using WageTracker.Domain.Common;

namespace WageTracker.Domain.Employees;

/// <summary>
/// How an employee is paid. For <see cref="CompensationType.Hourly"/> the amount is the hourly rate;
/// for <see cref="CompensationType.Salary"/> it is the annual salary.
/// </summary>
public sealed record Compensation
{
    public const int WeeksPerYear = 52;

    private Compensation(CompensationType type, decimal amount, bool overtimeEligible)
    {
        if (amount < 0)
            throw new DomainException("Compensation cannot be negative.");
        Type = type;
        Amount = amount;
        OvertimeEligible = overtimeEligible;
    }

    public CompensationType Type { get; }

    public decimal Amount { get; }

    /// <summary>Hourly employees always earn overtime. Salaried employees earn it only when this is set.</summary>
    public bool OvertimeEligible { get; }

    public static Compensation Hourly(decimal hourlyRate) => new(CompensationType.Hourly, hourlyRate, true);

    public static Compensation Salary(decimal annualSalary, bool overtimeEligible = false) =>
        new(CompensationType.Salary, annualSalary, overtimeEligible);

    public decimal WeeklySalary => Type == CompensationType.Salary
        ? Amount / WeeksPerYear
        : throw new DomainException("Only salaried compensation has a weekly salary.");

    /// <summary>
    /// The rate for one hour. For salary it is the weekly salary divided by the overtime threshold,
    /// because the salary covers a standard week.
    /// </summary>
    public decimal HourlyRate(decimal standardWeekHours) => Type switch
    {
        CompensationType.Hourly => Amount,
        CompensationType.Salary => WeeklySalary / standardWeekHours,
        _ => throw new DomainException($"Unknown compensation type {Type}."),
    };
}
