namespace WageTracker.Domain.TimeOff;

/// <summary>
/// One paid day off for an employee. Its hours are not stored; they come from the employee's
/// table, falling back to the type's default. New days off are created with
/// <see cref="Employees.Employee.BookTimeOff"/>, which enforces the booking rules; this constructor
/// is for loading saved records. To change a day off, remove it and book a new one.
/// </summary>
public sealed class CompensatedTimeOff(Guid id, Guid employeeId, DateOnly date, Guid timeOffTypeId)
{
    public Guid Id { get; } = id;

    public Guid EmployeeId { get; } = employeeId;

    public DateOnly Date { get; } = date;

    public Guid TimeOffTypeId { get; } = timeOffTypeId;
}
