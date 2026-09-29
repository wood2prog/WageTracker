using WageTracker.Domain.Employees;
using WageTracker.Domain.Payroll;
using WageTracker.Domain.TimeOff;
using WageTracker.Domain.TimeTracking;

namespace WageTracker.Application.Abstractions;

// Implemented by Infrastructure. Each write is saved immediately; no use case changes more than one aggregate.

public interface IEmployeeRepository
{
    Task<Employee?> GetAsync(Guid id);

    Task<IReadOnlyList<Employee>> ListAsync();

    Task AddAsync(Employee employee);

    Task UpdateAsync(Employee employee);
}

public interface ITimeEntryRepository
{
    Task<TimeEntry?> GetAsync(Guid id);

    /// <summary>The employee's entries that overlap [from, to).</summary>
    Task<IReadOnlyList<TimeEntry>> ListForEmployeeAsync(Guid employeeId, DateTime from, DateTime to);

    Task AddAsync(TimeEntry entry);

    Task UpdateAsync(TimeEntry entry);

    Task RemoveAsync(Guid id);
}

public interface ITimeOffRepository
{
    Task<CompensatedTimeOff?> GetAsync(Guid id);

    /// <summary>The employee's days off from <paramref name="from"/> through <paramref name="to"/>, inclusive.</summary>
    Task<IReadOnlyList<CompensatedTimeOff>> ListForEmployeeAsync(Guid employeeId, DateOnly from, DateOnly to);

    Task AddAsync(CompensatedTimeOff timeOff);

    Task RemoveAsync(Guid id);
}

/// <summary>There is one settings record, including its time-off types and holiday calendar.</summary>
public interface IPayrollSettingsRepository
{
    /// <summary>Null until settings are first set up.</summary>
    Task<PayrollSettings?> GetAsync();

    Task SaveAsync(PayrollSettings settings);
}

public interface IPayrollRunRepository
{
    Task<PayrollRun?> GetByPeriodStartAsync(DateOnly start);

    /// <summary>The run whose period ends on <paramref name="end"/> (a Saturday), if any.</summary>
    Task<PayrollRun?> GetEndingOnAsync(DateOnly end);

    /// <summary>Locked runs whose period overlaps <paramref name="from"/> through <paramref name="to"/>, inclusive.</summary>
    Task<IReadOnlyList<PayrollRun>> ListLockedOverlappingAsync(DateOnly from, DateOnly to);

    /// <summary>The locked run with the latest period, or null if nothing is locked yet.</summary>
    Task<PayrollRun?> GetLatestLockedAsync();

    Task AddAsync(PayrollRun run);
}
