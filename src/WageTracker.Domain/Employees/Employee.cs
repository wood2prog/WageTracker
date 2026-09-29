using WageTracker.Domain.Common;
using WageTracker.Domain.TimeOff;

namespace WageTracker.Domain.Employees;

public sealed class Employee
{
    public Employee(
        string firstName,
        string lastName,
        DateOnly birthDate,
        DateOnly hireDate,
        EmploymentType employmentType,
        Compensation compensation,
        decimal overtimePercentage,
        int vacationDaysPermitted)
        : this(Guid.NewGuid(), firstName, lastName, birthDate, hireDate, null, employmentType, compensation,
            overtimePercentage, vacationDaysPermitted)
    {
    }

    public Employee(
        Guid id,
        string firstName,
        string lastName,
        DateOnly birthDate,
        DateOnly hireDate,
        DateOnly? endDate,
        EmploymentType employmentType,
        Compensation compensation,
        decimal overtimePercentage,
        int vacationDaysPermitted)
    {
        Id = id;
        Rename(firstName, lastName);
        BirthDate = birthDate;
        ChangeEmploymentDates(hireDate, endDate);
        EmploymentType = employmentType;
        Compensation = compensation;
        SetOvertimePercentage(overtimePercentage);
        SetVacationDaysPermitted(vacationDaysPermitted);
    }

    public Guid Id { get; }

    public string FirstName { get; private set; } = "";

    public string LastName { get; private set; } = "";

    public string FullName => $"{FirstName} {LastName}";

    public DateOnly BirthDate { get; private set; }

    /// <summary>The first day of employment.</summary>
    public DateOnly HireDate { get; private set; }

    /// <summary>The last day of employment (inclusive), or null while still employed.</summary>
    public DateOnly? EndDate { get; private set; }

    public EmploymentType EmploymentType { get; private set; }

    public Compensation Compensation { get; private set; }

    /// <summary>
    /// The premium on top of the regular rate for overtime hours, as a percentage.
    /// 50 means overtime is paid at 1.5 times the regular rate.
    /// </summary>
    public decimal OvertimePercentage { get; private set; }

    public decimal OvertimeMultiplier => 1m + OvertimePercentage / 100m;

    /// <summary>Vacation days allowed per calendar year.</summary>
    public int VacationDaysPermitted { get; private set; }

    /// <summary>
    /// Optional per-employee hours for each type of time off. Any type left unset falls back to the type's default.
    /// </summary>
    public TimeOffHoursTable TimeOffHours { get; } = new();

    /// <summary>Only full-time employees get compensated time off (holidays, vacation, and the like).</summary>
    public bool GetsCompensatedTimeOff => EmploymentType == EmploymentType.FullTime;

    public void Rename(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("An employee needs a first and last name.");
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
    }

    public void ChangeBirthDate(DateOnly birthDate) => BirthDate = birthDate;

    /// <param name="endDate">The last day worked, or null while still employed.</param>
    public void ChangeEmploymentDates(DateOnly hireDate, DateOnly? endDate)
    {
        if (endDate < hireDate)
            throw new DomainException("The end date cannot be before the hire date.");
        HireDate = hireDate;
        EndDate = endDate;
    }

    public bool IsEmployedOn(DateOnly date) => date >= HireDate && !(date > EndDate);

    /// <summary>Whether the employee was employed on any day from <paramref name="from"/> through <paramref name="to"/>.</summary>
    public bool IsEmployedDuring(DateOnly from, DateOnly to) => HireDate <= to && !(EndDate < from);

    /// <summary>Monday–Friday days employed from <paramref name="from"/> through <paramref name="to"/>.</summary>
    public int WeekdaysEmployed(DateOnly from, DateOnly to)
    {
        var count = 0;
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && IsEmployedOn(date))
                count++;
        }
        return count;
    }

    public void EnsureEmployedOn(DateOnly date)
    {
        if (!IsEmployedOn(date))
            throw new DomainException($"{FullName} is not employed on {date:yyyy-MM-dd}.");
    }

    public void ChangeEmploymentType(EmploymentType employmentType) => EmploymentType = employmentType;

    public void ChangeCompensation(Compensation compensation) => Compensation = compensation;

    public void SetOvertimePercentage(decimal percentage)
    {
        if (percentage < 0)
            throw new DomainException("Overtime percentage cannot be negative.");
        OvertimePercentage = percentage;
    }

    public void SetVacationDaysPermitted(int days)
    {
        if (days < 0)
            throw new DomainException("Vacation days permitted cannot be negative.");
        VacationDaysPermitted = days;
    }

    /// <summary>
    /// Hours one day of <paramref name="type"/> is worth for this employee: their own value if set,
    /// otherwise the type's global default.
    /// </summary>
    public decimal ResolveTimeOffHours(TimeOffType type) =>
        TimeOffHours.HoursFor(type.Id) ?? type.DefaultHoursPerDay;

    /// <param name="timeOff">This employee's time off; records outside <paramref name="year"/> are ignored.</param>
    public VacationBalance VacationBalance(int year, TimeOffType vacationType, IEnumerable<CompensatedTimeOff> timeOff)
    {
        if (!vacationType.IsVacation)
            throw new DomainException($"{vacationType.Name} is not the vacation type.");
        var used = timeOff.Count(t =>
            t.EmployeeId == Id && t.TimeOffTypeId == vacationType.Id && t.Date.Year == year);
        return new VacationBalance(year, VacationDaysPermitted, used);
    }

    /// <summary>
    /// Books one day of compensated time off. Only full-time employees can book. Holidays come from the
    /// company calendar and cannot be booked, and nothing can be booked on a company holiday. Archived
    /// types cannot be booked, an employee can have only one day off per date, and vacation days cannot
    /// go over the yearly allowance.
    /// </summary>
    /// <param name="existingTimeOff">This employee's time off, including at least the calendar year of <paramref name="date"/>.</param>
    public CompensatedTimeOff BookTimeOff(
        DateOnly date, TimeOffType type, IEnumerable<CompensatedTimeOff> existingTimeOff, HolidayCalendar holidays)
    {
        EnsureEmployedOn(date);
        if (!GetsCompensatedTimeOff)
            throw new DomainException($"{FullName} is part-time and does not get compensated time off.");
        if (type.IsHoliday)
            throw new DomainException("Holidays come from the company holiday calendar and cannot be booked.");
        if (type.IsArchived)
            throw new DomainException($"{type.Name} is archived and can no longer be booked.");
        if (holidays.IsHoliday(date))
            throw new DomainException($"{date:yyyy-MM-dd} is a company holiday.");

        var existing = existingTimeOff.Where(t => t.EmployeeId == Id).ToList();
        if (existing.Any(t => t.Date == date))
            throw new DomainException($"{FullName} already has time off on {date:yyyy-MM-dd}.");
        if (type.IsVacation && VacationBalance(date.Year, type, existing).Remaining == 0)
            throw new DomainException($"{FullName} has no vacation days left in {date.Year}.");

        return new CompensatedTimeOff(Guid.NewGuid(), Id, date, type.Id);
    }
}
