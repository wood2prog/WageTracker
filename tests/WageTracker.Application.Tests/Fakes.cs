using WageTracker.Application.Abstractions;
using WageTracker.Application.Employees;
using WageTracker.Application.Payroll;
using WageTracker.Application.Settings;
using WageTracker.Application.TimeOff;
using WageTracker.Application.TimeTracking;
using WageTracker.Domain.Employees;
using WageTracker.Domain.Payroll;
using WageTracker.Domain.TimeOff;
using WageTracker.Domain.TimeTracking;

namespace WageTracker.Application.Tests;

internal sealed class FakeClock(DateTime now) : IClock
{
    public DateTime Now { get; set; } = now;
}

internal sealed class InMemoryEmployees : IEmployeeRepository
{
    public Dictionary<Guid, Employee> Items { get; } = [];

    public Task<Employee?> GetAsync(Guid id) => Task.FromResult(Items.GetValueOrDefault(id));

    public Task<IReadOnlyList<Employee>> ListAsync() => Task.FromResult<IReadOnlyList<Employee>>(Items.Values.ToList());

    public Task AddAsync(Employee employee) { Items.Add(employee.Id, employee); return Task.CompletedTask; }

    public Task UpdateAsync(Employee employee) { Items[employee.Id] = employee; return Task.CompletedTask; }
}

internal sealed class InMemoryTimeEntries : ITimeEntryRepository
{
    public Dictionary<Guid, TimeEntry> Items { get; } = [];

    public Task<TimeEntry?> GetAsync(Guid id) => Task.FromResult(Items.GetValueOrDefault(id));

    public Task<IReadOnlyList<TimeEntry>> ListForEmployeeAsync(Guid employeeId, DateTime from, DateTime to) =>
        Task.FromResult<IReadOnlyList<TimeEntry>>(
            Items.Values.Where(e => e.EmployeeId == employeeId && e.Start < to && from < e.End).ToList());

    public Task AddAsync(TimeEntry entry) { Items.Add(entry.Id, entry); return Task.CompletedTask; }

    public Task UpdateAsync(TimeEntry entry) { Items[entry.Id] = entry; return Task.CompletedTask; }

    public Task RemoveAsync(Guid id) { Items.Remove(id); return Task.CompletedTask; }
}

internal sealed class InMemoryTimeOff : ITimeOffRepository
{
    public Dictionary<Guid, CompensatedTimeOff> Items { get; } = [];

    public Task<CompensatedTimeOff?> GetAsync(Guid id) => Task.FromResult(Items.GetValueOrDefault(id));

    public Task<IReadOnlyList<CompensatedTimeOff>> ListForEmployeeAsync(Guid employeeId, DateOnly from, DateOnly to) =>
        Task.FromResult<IReadOnlyList<CompensatedTimeOff>>(
            Items.Values.Where(t => t.EmployeeId == employeeId && t.Date >= from && t.Date <= to).ToList());

    public Task AddAsync(CompensatedTimeOff timeOff) { Items.Add(timeOff.Id, timeOff); return Task.CompletedTask; }

    public Task RemoveAsync(Guid id) { Items.Remove(id); return Task.CompletedTask; }
}

internal sealed class InMemorySettings : IPayrollSettingsRepository
{
    public PayrollSettings? Current { get; set; }

    public Task<PayrollSettings?> GetAsync() => Task.FromResult(Current);

    public Task SaveAsync(PayrollSettings settings) { Current = settings; return Task.CompletedTask; }
}

internal sealed class InMemoryRuns : IPayrollRunRepository
{
    public List<PayrollRun> Items { get; } = [];

    public Task<PayrollRun?> GetByPeriodStartAsync(DateOnly start) =>
        Task.FromResult(Items.SingleOrDefault(r => r.Period.Start == start));

    public Task<PayrollRun?> GetEndingOnAsync(DateOnly end) =>
        Task.FromResult(Items.SingleOrDefault(r => r.Period.End == end));

    public Task<IReadOnlyList<PayrollRun>> ListLockedOverlappingAsync(DateOnly from, DateOnly to) =>
        Task.FromResult<IReadOnlyList<PayrollRun>>(
            Items.Where(r => r.IsLocked && r.Period.Start <= to && from <= r.Period.End).ToList());

    public Task<PayrollRun?> GetLatestLockedAsync() =>
        Task.FromResult(Items.Where(r => r.IsLocked).MaxBy(r => r.Period.Start));

    public Task AddAsync(PayrollRun run) { Items.Add(run); return Task.CompletedTask; }
}

internal sealed class RecordingReportWriter : IPayrollReportWriter
{
    public List<PayrollRunDto> Written { get; } = [];

    public Task<string> WriteAsync(PayrollRunDto run)
    {
        Written.Add(run);
        return Task.FromResult($"report-{run.Start:yyyy-MM-dd}.pdf");
    }
}

/// <summary>All the services wired to in-memory repositories. Weekly schedule, payout on the 10th.</summary>
internal sealed class TestApp
{
    /// <summary>Sunday, September 6, 2026.</summary>
    public static readonly DateOnly Sunday = new(2026, 9, 6);

    public TestApp(DateTime? now = null)
    {
        Clock = new FakeClock(now ?? new DateTime(2026, 9, 29, 17, 0, 0));
        Settings.Current = PayrollSettings.CreateDefault(PayPeriodSchedule.Weekly(), payoutDayOfMonth: 10);
    }

    public FakeClock Clock { get; }
    public InMemoryEmployees Employees { get; } = new();
    public InMemoryTimeEntries TimeEntries { get; } = new();
    public InMemoryTimeOff TimeOff { get; } = new();
    public InMemorySettings Settings { get; } = new();
    public InMemoryRuns Runs { get; } = new();
    public RecordingReportWriter Reports { get; } = new();

    public EmployeeService EmployeeService => new(Employees, TimeOff, Settings);
    public TimeEntryService TimeEntryService => new(TimeEntries, Employees, Runs, Clock);
    public TimeOffService TimeOffService => new(TimeOff, Employees, Settings, Runs);
    public SettingsService SettingsService => new(Settings, Runs);
    public HolidayService HolidayService => new(Settings);
    public PayrollService PayrollService => new(Employees, TimeEntries, TimeOff, Settings, Runs, Reports, Clock);

    public PayrollSettings CurrentSettings => Settings.Current!;

    public Task<EmployeeDto> AddHourlyAsync(decimal rate = 20m, EmploymentType type = EmploymentType.FullTime) =>
        EmployeeService.CreateAsync(new EmployeeInput(
            "Ada", "Lovelace", new DateOnly(1990, 12, 10), new DateOnly(2020, 1, 1), null, type, CompensationType.Hourly, rate, false, 50m, 10));

    public Task<EmployeeDto> AddSalariedAsync(decimal annual = 52_000m, bool overtimeEligible = true) =>
        EmployeeService.CreateAsync(new EmployeeInput(
            "Grace", "Hopper", new DateOnly(1985, 12, 9), new DateOnly(2020, 1, 1), null, EmploymentType.FullTime, CompensationType.Salary, annual, overtimeEligible, 50m, 15));

    /// <summary>Monday through Friday, 8:00 for <paramref name="hoursPerDay"/> hours, in the week starting <paramref name="sunday"/>.</summary>
    public async Task WorkWeekAsync(Guid employeeId, decimal hoursPerDay, DateOnly? sunday = null)
    {
        for (var i = 1; i <= 5; i++)
        {
            var start = (sunday ?? Sunday).AddDays(i).ToDateTime(new TimeOnly(8, 0));
            await TimeEntryService.RecordAsync(employeeId, start, start.AddHours((double)hoursPerDay));
        }
    }
}
