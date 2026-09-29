using WageTracker.Domain.Common;
using WageTracker.Domain.Employees;
using WageTracker.Domain.TimeOff;
using static WageTracker.Domain.Tests.TestData;

namespace WageTracker.Domain.Tests;

public class TimeOffTests
{
    private static readonly HolidayCalendar NoHolidays = new();

    [Fact]
    public void Full_time_employees_can_book_time_off()
    {
        var employee = Hourly();
        var settings = Settings();

        var dayOff = employee.BookTimeOff(Sunday.AddDays(1), settings.Type("Sick"), [], NoHolidays);

        Assert.Equal(employee.Id, dayOff.EmployeeId);
        Assert.Equal(settings.Type("Sick").Id, dayOff.TimeOffTypeId);
    }

    [Fact]
    public void Part_time_employees_cannot_book_time_off()
    {
        var employee = Hourly(type: EmploymentType.PartTime);

        Assert.Throws<DomainException>(() => employee.BookTimeOff(Sunday.AddDays(1), Settings().Type("Sick"), [], NoHolidays));
    }

    [Fact]
    public void Vacation_cannot_go_over_the_yearly_allowance()
    {
        var employee = Hourly(vacationDays: 2);
        var vacation = Settings().VacationType;
        var booked = new[]
        {
            employee.BookTimeOff(new DateOnly(2026, 3, 2), vacation, [], NoHolidays),
            DayOff(employee, new DateOnly(2026, 3, 3), vacation),
        };

        Assert.Throws<DomainException>(() => employee.BookTimeOff(new DateOnly(2026, 3, 4), vacation, booked, NoHolidays));
        employee.BookTimeOff(new DateOnly(2027, 1, 4), vacation, booked, NoHolidays);
    }

    [Fact]
    public void Vacation_balance_counts_days_used_in_that_year_only()
    {
        var employee = Hourly(vacationDays: 10);
        var vacation = Settings().VacationType;
        var timeOff = new[]
        {
            DayOff(employee, new DateOnly(2025, 12, 30), vacation),
            DayOff(employee, new DateOnly(2026, 2, 2), vacation),
            DayOff(employee, new DateOnly(2026, 2, 3), vacation),
        };

        var balance = employee.VacationBalance(2026, vacation, timeOff);

        Assert.Equal(2, balance.Used);
        Assert.Equal(8, balance.Remaining);
    }

    [Fact]
    public void Only_one_day_off_per_date()
    {
        var employee = Hourly();
        var settings = Settings();
        var sick = DayOff(employee, Sunday.AddDays(1), settings.Type("Sick"));

        Assert.Throws<DomainException>(() => employee.BookTimeOff(Sunday.AddDays(1), settings.VacationType, [sick], NoHolidays));
    }

    [Fact]
    public void Archived_types_cannot_be_booked()
    {
        var employee = Hourly();
        var settings = Settings();
        var sick = settings.Type("Sick");
        settings.ArchiveTimeOffType(sick.Id);

        Assert.Throws<DomainException>(() => employee.BookTimeOff(Sunday.AddDays(1), sick, [], NoHolidays));
    }

    [Fact]
    public void Holidays_cannot_be_booked_per_employee()
    {
        var settings = Settings();

        Assert.Throws<DomainException>(() => Hourly().BookTimeOff(Sunday.AddDays(1), settings.HolidayType, [], NoHolidays));
    }

    [Fact]
    public void Nothing_can_be_booked_on_a_company_holiday()
    {
        var settings = Settings();
        settings.Holidays.Add("Labor Day", HolidayRule.NthWeekday(9, DayOfWeek.Monday, 1));

        Assert.Throws<DomainException>(() =>
            Hourly().BookTimeOff(new DateOnly(2026, 9, 7), settings.VacationType, [], settings.Holidays));
    }
}
