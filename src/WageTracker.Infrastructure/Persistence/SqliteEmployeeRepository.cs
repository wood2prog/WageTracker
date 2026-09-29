using Microsoft.Data.Sqlite;
using WageTracker.Application.Abstractions;
using WageTracker.Domain.Employees;

namespace WageTracker.Infrastructure.Persistence;

public sealed class SqliteEmployeeRepository(SqliteConnectionFactory db) : IEmployeeRepository
{
    public async Task<Employee?> GetAsync(Guid id) =>
        (await LoadAsync("WHERE e.id = $id", ("$id", id))).SingleOrDefault();

    public async Task<IReadOnlyList<Employee>> ListAsync() => await LoadAsync("");

    public async Task AddAsync(Employee employee)
    {
        await using var connection = await db.OpenAsync();
        using var tx = connection.BeginTransaction();
        connection.Execute(tx, """
            INSERT INTO employees (id, first_name, last_name, birth_date, hire_date, end_date, employment_type,
                compensation_type, compensation_amount, overtime_eligible, overtime_percentage, vacation_days_permitted)
            VALUES ($id, $first, $last, $birth, $hire, $end, $employment, $compType, $amount, $otEligible, $otPct, $vacation)
            """, Parameters(employee));
        SaveTimeOffHours(connection, tx, employee);
        tx.Commit();
    }

    public async Task UpdateAsync(Employee employee)
    {
        await using var connection = await db.OpenAsync();
        using var tx = connection.BeginTransaction();
        connection.Execute(tx, """
            UPDATE employees SET first_name = $first, last_name = $last, birth_date = $birth, hire_date = $hire,
                end_date = $end, employment_type = $employment, compensation_type = $compType,
                compensation_amount = $amount, overtime_eligible = $otEligible, overtime_percentage = $otPct,
                vacation_days_permitted = $vacation
            WHERE id = $id
            """, Parameters(employee));
        connection.Execute(tx, "DELETE FROM employee_time_off_hours WHERE employee_id = $id", ("$id", employee.Id));
        SaveTimeOffHours(connection, tx, employee);
        tx.Commit();
    }

    private async Task<List<Employee>> LoadAsync(string where, params (string, object?)[] parameters)
    {
        await using var connection = await db.OpenAsync();
        var employees = await connection.QueryAsync($"SELECT e.* FROM employees e {where}", Map, parameters);
        var overrides = (await connection.QueryAsync(
                $"SELECT h.* FROM employee_time_off_hours h JOIN employees e ON e.id = h.employee_id {where}",
                r => (EmployeeId: r.Guid("employee_id"), TypeId: r.Guid("time_off_type_id"), Hours: r.Decimal("hours_per_day")),
                parameters))
            .ToLookup(o => o.EmployeeId);

        foreach (var employee in employees)
        {
            foreach (var o in overrides[employee.Id])
                employee.TimeOffHours.Set(o.TypeId, o.Hours);
        }
        return employees;
    }

    private static Employee Map(SqliteDataReader r)
    {
        var amount = r.Decimal("compensation_amount");
        var compensation = r.Enum<CompensationType>("compensation_type") == CompensationType.Salary
            ? Compensation.Salary(amount, r.Bool("overtime_eligible"))
            : Compensation.Hourly(amount);

        return new Employee(
            r.Guid("id"),
            r.Text("first_name"),
            r.Text("last_name"),
            r.Date("birth_date"),
            r.Date("hire_date"),
            r.DateOrNull("end_date"),
            r.Enum<EmploymentType>("employment_type"),
            compensation,
            r.Decimal("overtime_percentage"),
            r.Int("vacation_days_permitted"));
    }

    private static (string, object?)[] Parameters(Employee e) =>
    [
        ("$id", e.Id), ("$first", e.FirstName), ("$last", e.LastName), ("$birth", e.BirthDate),
        ("$hire", e.HireDate), ("$end", e.EndDate), ("$employment", e.EmploymentType),
        ("$compType", e.Compensation.Type), ("$amount", e.Compensation.Amount),
        ("$otEligible", e.Compensation.OvertimeEligible), ("$otPct", e.OvertimePercentage),
        ("$vacation", e.VacationDaysPermitted),
    ];

    private static void SaveTimeOffHours(SqliteConnection connection, SqliteTransaction tx, Employee employee)
    {
        foreach (var (typeId, hours) in employee.TimeOffHours.Entries)
        {
            connection.Execute(tx,
                "INSERT INTO employee_time_off_hours (employee_id, time_off_type_id, hours_per_day) VALUES ($e, $t, $h)",
                ("$e", employee.Id), ("$t", typeId), ("$h", hours));
        }
    }
}
