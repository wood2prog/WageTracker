using Microsoft.Data.Sqlite;
using WageTracker.Domain.Calendar;
using WageTracker.Domain.Employees;
using WageTracker.Domain.Payroll;
using WageTracker.Domain.TimeOff;
using WageTracker.Domain.TimeTracking;
using WageTracker.Infrastructure.Persistence;

namespace WageTracker.Infrastructure.Tests;

public sealed class RepositoryTests : IDisposable
{
    private static readonly DateOnly Sunday = new(2026, 9, 6);

    private readonly TempDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private SqliteEmployeeRepository Employees => new(_db.Factory);
    private SqlitePayrollSettingsRepository Settings => new(_db.Factory);

    private async Task<PayrollSettings> SavedSettingsAsync()
    {
        var settings = PayrollSettings.CreateDefault(PayPeriodSchedule.Weekly(), 10);
        await Settings.SaveAsync(settings);
        return settings;
    }

    private async Task<Employee> SavedEmployeeAsync(Compensation? compensation = null)
    {
        var employee = new Employee(Guid.NewGuid(), "Ada", "Lovelace", new DateOnly(1990, 12, 10), new DateOnly(2020, 1, 6), null,
            EmploymentType.FullTime, compensation ?? Compensation.Hourly(20.25m), 50m, 10);
        await Employees.AddAsync(employee);
        return employee;
    }

    [Fact]
    public async Task The_database_is_created_and_migrated_on_first_open()
    {
        await using var connection = await _db.Factory.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";

        Assert.Equal((long)Schema.Migrations.Length, command.ExecuteScalar());
        Assert.True(File.Exists(_db.Options.DatabasePath));
    }

    [Fact]
    public async Task Employees_round_trip_with_their_time_off_hours()
    {
        var settings = await SavedSettingsAsync();
        var employee = await SavedEmployeeAsync(Compensation.Salary(61_500m, overtimeEligible: true));
        employee.ChangeEmploymentDates(employee.HireDate, new DateOnly(2026, 12, 18));
        employee.TimeOffHours.Set(settings.VacationType.Id, 7.5m);
        employee.ChangeTimeRecording(TimeRecording.Weekly);
        await Employees.UpdateAsync(employee);

        var loaded = await Employees.GetAsync(employee.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Ada Lovelace", loaded.FullName);
        Assert.Equal(new DateOnly(2020, 1, 6), loaded.HireDate);
        Assert.Equal(new DateOnly(2026, 12, 18), loaded.EndDate);
        Assert.Equal(Compensation.Salary(61_500m, overtimeEligible: true), loaded.Compensation);
        Assert.Equal(1.5m, loaded.OvertimeMultiplier);
        Assert.Equal(7.5m, loaded.TimeOffHours.HoursFor(settings.VacationType.Id));
        Assert.Equal(TimeRecording.Weekly, loaded.TimeRecording);
    }

    [Fact]
    public async Task Weekly_totals_are_saved_replaced_listed_and_removed()
    {
        var employee = await SavedEmployeeAsync();
        var repo = new SqliteWeeklyHoursRepository(_db.Factory);
        var week = new WorkWeek(Sunday);
        await repo.SaveAsync(new WeeklyHours(employee.Id, week, 40m));
        await repo.SaveAsync(new WeeklyHours(employee.Id, week, 42.25m));
        await repo.SaveAsync(new WeeklyHours(employee.Id, week.Next(), 38m));

        Assert.Equal(42.25m, (await repo.GetAsync(employee.Id, Sunday))!.Hours);
        Assert.Equal([Sunday, Sunday.AddDays(7)], (await repo.ListForEmployeeAsync(employee.Id, Sunday, Sunday.AddDays(7))).Select(w => w.Week.Start));
        Assert.Single(await repo.ListForEmployeeAsync(employee.Id, Sunday.AddDays(1), Sunday.AddDays(13)));

        await repo.RemoveAsync(employee.Id, Sunday);
        Assert.Null(await repo.GetAsync(employee.Id, Sunday));
    }

    [Fact]
    public async Task Clearing_a_time_off_override_removes_it()
    {
        var settings = await SavedSettingsAsync();
        var employee = await SavedEmployeeAsync();
        employee.TimeOffHours.Set(settings.VacationType.Id, 7.5m);
        await Employees.UpdateAsync(employee);

        employee.TimeOffHours.Clear(settings.VacationType.Id);
        await Employees.UpdateAsync(employee);

        Assert.True((await Employees.GetAsync(employee.Id))!.TimeOffHours.IsEmpty);
    }

    [Fact]
    public async Task Time_entries_are_found_by_overlap_and_keep_their_exact_times()
    {
        var employee = await SavedEmployeeAsync();
        var repo = new SqliteTimeEntryRepository(_db.Factory);
        var start = new DateTime(2026, 9, 7, 8, 0, 0).AddTicks(1234567);
        var entry = new TimeEntry(Guid.NewGuid(), employee.Id, start, start.AddHours(4));
        await repo.AddAsync(entry);

        var overlapping = await repo.ListForEmployeeAsync(employee.Id, start.AddHours(3), start.AddHours(10));
        var after = await repo.ListForEmployeeAsync(employee.Id, start.AddHours(4), start.AddHours(10));

        Assert.Equal(start, overlapping.Single().Start);
        Assert.Empty(after);
    }

    [Fact]
    public async Task Time_entries_can_be_moved_and_removed()
    {
        var employee = await SavedEmployeeAsync();
        var repo = new SqliteTimeEntryRepository(_db.Factory);
        var start = new DateTime(2026, 9, 7, 8, 0, 0);
        var entry = new TimeEntry(Guid.NewGuid(), employee.Id, start, start.AddHours(4));
        await repo.AddAsync(entry);

        var moved = new TimeEntry(entry.Id, employee.Id, start.AddHours(1), start.AddHours(6));
        await repo.UpdateAsync(moved);
        Assert.Equal(5m, (await repo.GetAsync(entry.Id))!.Hours);

        await repo.RemoveAsync(entry.Id);
        Assert.Null(await repo.GetAsync(entry.Id));
    }

    [Fact]
    public async Task Time_off_is_listed_by_date_range()
    {
        var settings = await SavedSettingsAsync();
        var employee = await SavedEmployeeAsync();
        var repo = new SqliteTimeOffRepository(_db.Factory);
        foreach (var day in new[] { 1, 15, 30 })
            await repo.AddAsync(new CompensatedTimeOff(Guid.NewGuid(), employee.Id, new DateOnly(2026, 9, day), settings.VacationType.Id));

        var listed = await repo.ListForEmployeeAsync(employee.Id, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 15));

        Assert.Equal([1, 15], listed.Select(t => t.Date.Day));
    }

    [Fact]
    public async Task Settings_round_trip_with_types_holidays_and_schedule()
    {
        var settings = PayrollSettings.CreateDefault(PayPeriodSchedule.BiWeekly(Sunday), 15);
        settings.ChangeSchedule(PayPeriodSchedule.BiWeekly(Sunday), Sunday.AddDays(14));
        settings.SetOvertimeThresholdHours(37.5m);
        settings.TimeOffCountsTowardOvertime = false;
        var bereavement = settings.AddTimeOffType("Bereavement", 7.25m);
        settings.ArchiveTimeOffType(settings.TimeOffTypes.Single(t => t.Name == "Sick").Id);
        var christmas = settings.Holidays.Add("Christmas", HolidayRule.Fixed(12, 25));
        settings.Holidays.SetObservedDate(christmas.Id, 2027, new DateOnly(2027, 12, 24));
        settings.Holidays.Add("Thanksgiving", HolidayRule.NthWeekday(11, DayOfWeek.Thursday, 4));
        settings.Holidays.Add("Memorial Day", HolidayRule.LastWeekday(5, DayOfWeek.Monday));
        await Settings.SaveAsync(settings);

        var loaded = await Settings.GetAsync();

        Assert.NotNull(loaded);
        Assert.Equal(PayFrequency.BiWeekly, loaded.Schedule.Frequency);
        Assert.Equal(Sunday, loaded.Schedule.Anchor);
        Assert.Equal(Sunday.AddDays(14), loaded.ScheduleEffectiveFrom);
        Assert.Equal(15, loaded.PayoutDayOfMonth);
        Assert.Equal(37.5m, loaded.OvertimeThresholdHours);
        Assert.False(loaded.TimeOffCountsTowardOvertime);
        Assert.Equal(["Holiday", "Vacation", "Sick", "Bereavement"], loaded.TimeOffTypes.Select(t => t.Name));
        Assert.Equal(7.25m, loaded.GetTimeOffType(bereavement.Id).DefaultHoursPerDay);
        Assert.True(loaded.TimeOffTypes.Single(t => t.Name == "Sick").IsArchived);
        Assert.Equal(
            [settings.Holidays.Holidays[0].Rule, settings.Holidays.Holidays[1].Rule, settings.Holidays.Holidays[2].Rule],
            loaded.Holidays.Holidays.Select(h => h.Rule));
        Assert.Equal(new DateOnly(2027, 12, 24), loaded.Holidays.Get(christmas.Id).DateIn(2027));
    }

    [Fact]
    public async Task Removed_holidays_are_removed_from_the_database()
    {
        var settings = await SavedSettingsAsync();
        var christmas = settings.Holidays.Add("Christmas", HolidayRule.Fixed(12, 25));
        settings.Holidays.SetObservedDate(christmas.Id, 2027, new DateOnly(2027, 12, 24));
        await Settings.SaveAsync(settings);

        settings.Holidays.Remove(christmas.Id);
        await Settings.SaveAsync(settings);

        Assert.Empty((await Settings.GetAsync())!.Holidays.Holidays);
    }

    [Fact]
    public async Task No_settings_until_saved() => Assert.Null(await Settings.GetAsync());

    [Fact]
    public async Task Locked_statements_keep_salaried_overtime_ineligibility()
    {
        var settings = await SavedSettingsAsync();
        var employee = await SavedEmployeeAsync(Compensation.Salary(52_000m, overtimeEligible: false));
        var period = settings.PeriodContaining(Sunday);
        var run = new PayrollRun(period, settings.PayoutDateFor(period));
        run.Lock([PayStatement.Calculate(employee, period, settings, [], [], null)], new DateTime(2026, 9, 14, 9, 30, 0));
        var repo = new SqlitePayrollRunRepository(_db.Factory);
        await repo.AddAsync(run);

        Assert.False((await repo.GetByPeriodStartAsync(Sunday))!.StatementFor(employee.Id)!.OvertimeEligible);
    }

    [Fact]
    public async Task Upgrading_takes_locked_salaried_overtime_eligibility_from_the_employee()
    {
        Directory.CreateDirectory(_db.Folder);
        await using (var v3 = new SqliteConnection($"Data Source={_db.Options.DatabasePath};Pooling=False"))
        {
            await v3.OpenAsync();
            using var command = v3.CreateCommand();
            command.CommandText = string.Join(";", Schema.Migrations[..3]) + """
                ;
                PRAGMA user_version = 3;
                INSERT INTO employees (id, first_name, last_name, birth_date, hire_date, employment_type, compensation_type,
                    compensation_amount, overtime_eligible, overtime_percentage, vacation_days_permitted)
                VALUES ('00000000-0000-0000-0000-00000000000a', 'Ada', 'L', '1990-01-01', '2020-01-01', 'FullTime', 'Hourly', '20', 1, '50', 10),
                       ('00000000-0000-0000-0000-00000000000b', 'Grace', 'H', '1990-01-01', '2020-01-01', 'FullTime', 'Salary', '52000', 0, '50', 10),
                       ('00000000-0000-0000-0000-00000000000c', 'Alan', 'T', '1990-01-01', '2020-01-01', 'FullTime', 'Salary', '52000', 1, '50', 10);
                INSERT INTO payroll_runs VALUES ('00000000-0000-0000-0000-000000000001', '2026-09-06', '2026-09-12', 1, '2026-10-10', 'Locked', '2026-09-14T09:00:00.0000000');
                INSERT INTO pay_statements (run_id, employee_id, compensation_type, hourly_rate, overtime_multiplier, base_pay,
                    overtime_pay, deferred_overtime_pay, vacation_days_paid_out, vacation_payout)
                VALUES ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-00000000000a', 'Hourly', '20', '1.5', '800', '0', '0', 0, '0'),
                       ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-00000000000b', 'Salary', '25', '1.5', '1000', '0', '0', 0, '0'),
                       ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-00000000000c', 'Salary', '25', '1.5', '1000', '0', '0', 0, '0');
                """;
            command.ExecuteNonQuery();
        }

        var run = (await new SqlitePayrollRunRepository(_db.Factory).GetByPeriodStartAsync(Sunday))!;

        Assert.Equal([true, false, true], run.Statements.OrderBy(s => s.EmployeeId).Select(s => s.OvertimeEligible));
        Assert.Equal(40m, run.Statements.Single(s => s.CompensationType == CompensationType.Salary && !s.OvertimeEligible).CompensatedRegularHours);
    }

    [Fact]
    public async Task Locked_runs_round_trip_with_exact_statement_values()
    {
        var settings = await SavedSettingsAsync();
        var employee = await SavedEmployeeAsync(Compensation.Salary(50_000m, overtimeEligible: true));
        var period = settings.PeriodContaining(Sunday);
        var start = Sunday.AddDays(1).ToDateTime(new TimeOnly(8, 0));
        var entries = Enumerable.Range(0, 5).Select(i => new TimeEntry(Guid.NewGuid(), employee.Id, start.AddDays(i), start.AddDays(i).AddHours(9))).ToList();
        var statement = PayStatement.Calculate(employee, period, settings, entries, [], null);
        var run = new PayrollRun(period, settings.PayoutDateFor(period));
        run.Lock([statement], new DateTime(2026, 9, 14, 9, 30, 0));
        var repo = new SqlitePayrollRunRepository(_db.Factory);
        await repo.AddAsync(run);

        var loaded = (await repo.GetByPeriodStartAsync(Sunday))!;
        var loadedStatement = loaded.StatementFor(employee.Id)!;

        Assert.True(loaded.IsLocked);
        Assert.Equal(run.LockedAt, loaded.LockedAt);
        Assert.Equal(run.PayoutDate, loaded.PayoutDate);
        Assert.Equal(statement.HourlyRate, loadedStatement.HourlyRate); // 24.0384615... kept exactly
        Assert.Equal(statement.DeferredOvertimePay, loadedStatement.DeferredOvertimePay);
        Assert.Equal(statement.GrossPay, loadedStatement.GrossPay);
        Assert.Equal(statement.Weeks, loadedStatement.Weeks);
        Assert.True(loadedStatement.OvertimeEligible);
        Assert.Equal(40m, loadedStatement.CompensatedRegularHours);
        Assert.Equal(loaded.Id, (await repo.GetEndingOnAsync(Sunday.AddDays(6)))!.Id);
        Assert.Equal(loaded.Id, (await repo.GetLatestLockedAsync())!.Id);
        Assert.Single(await repo.ListLockedOverlappingAsync(Sunday.AddDays(6), Sunday.AddDays(7)));
        Assert.Empty(await repo.ListLockedOverlappingAsync(Sunday.AddDays(7), Sunday.AddDays(8)));
    }
}
