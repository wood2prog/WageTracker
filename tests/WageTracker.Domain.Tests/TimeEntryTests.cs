using WageTracker.Domain.Common;
using WageTracker.Domain.TimeTracking;

namespace WageTracker.Domain.Tests;

public class TimeEntryTests
{
    private static readonly Guid EmployeeId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 9, 29, 17, 0, 0);
    private static readonly DateTime Morning = new(2026, 9, 28, 8, 0, 0);

    [Fact]
    public void Records_a_valid_entry()
    {
        var entry = TimeEntry.Record(EmployeeId, Morning, Morning.AddHours(8), [], Now);

        Assert.Equal(8m, entry.Hours);
    }

    [Fact]
    public void Must_end_after_it_starts() =>
        Assert.Throws<DomainException>(() => TimeEntry.Record(EmployeeId, Morning, Morning, [], Now));

    [Fact]
    public void Cannot_be_longer_than_24_hours() =>
        Assert.Throws<DomainException>(() => TimeEntry.Record(EmployeeId, Morning, Morning.AddHours(24.5), [], Now));

    [Fact]
    public void Cannot_end_in_the_future() =>
        Assert.Throws<DomainException>(() => TimeEntry.Record(EmployeeId, Now.AddHours(-1), Now.AddMinutes(1), [], Now));

    [Fact]
    public void Cannot_overlap_another_entry_for_the_same_employee()
    {
        var existing = TimeEntry.Record(EmployeeId, Morning, Morning.AddHours(4), [], Now);

        Assert.Throws<DomainException>(() =>
            TimeEntry.Record(EmployeeId, Morning.AddHours(3), Morning.AddHours(7), [existing], Now));
    }

    [Fact]
    public void Back_to_back_entries_and_other_employees_entries_do_not_overlap()
    {
        var existing = TimeEntry.Record(EmployeeId, Morning, Morning.AddHours(4), [], Now);
        var someoneElse = TimeEntry.Record(Guid.NewGuid(), Morning, Morning.AddHours(8), [], Now);

        TimeEntry.Record(EmployeeId, Morning.AddHours(4), Morning.AddHours(8), [existing, someoneElse], Now);
    }

    [Fact]
    public void Rescheduling_ignores_the_entry_itself_but_checks_the_others()
    {
        var first = TimeEntry.Record(EmployeeId, Morning, Morning.AddHours(4), [], Now);
        var second = TimeEntry.Record(EmployeeId, Morning.AddHours(5), Morning.AddHours(8), [first], Now);

        first.Reschedule(Morning.AddHours(1), Morning.AddHours(5), [first, second], Now);
        Assert.Throws<DomainException>(() => first.Reschedule(Morning, Morning.AddHours(6), [first, second], Now));
        Assert.Equal(Morning.AddHours(1), first.Start);
    }
}
