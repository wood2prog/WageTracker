using WageTracker.Domain.Common;
using WageTracker.Domain.Payroll;
using static WageTracker.Domain.Tests.TestData;

namespace WageTracker.Domain.Tests;

public class PayrollSettingsTests
{
    [Theory]
    [InlineData(15, 2026, 10, 15)] // after the period ends, same month
    [InlineData(3, 2026, 11, 3)]   // the period ends on Oct 3, so the next 3rd is in November
    [InlineData(1, 2026, 11, 1)]
    public void Payout_is_the_first_payout_day_after_the_period_ends(int day, int year, int month, int expectedDay)
    {
        var settings = PayrollSettings.CreateDefault(PayPeriodSchedule.Monthly(), day);
        var september = settings.Schedule.PeriodContaining(new DateOnly(2026, 9, 15));

        Assert.Equal(new DateOnly(year, month, expectedDay), settings.PayoutDateFor(september));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    public void Payout_day_must_be_1_to_28(int day) =>
        Assert.Throws<DomainException>(() => PayrollSettings.CreateDefault(PayPeriodSchedule.Weekly(), day));

    [Fact]
    public void Defaults_are_a_40_hour_threshold_with_time_off_counting_toward_overtime()
    {
        var settings = Settings();

        Assert.Equal(40m, settings.OvertimeThresholdHours);
        Assert.True(settings.TimeOffCountsTowardOvertime);
        Assert.Equal(["Holiday", "Vacation", "Sick"], settings.TimeOffTypes.Select(t => t.Name));
    }

    [Fact]
    public void Users_can_add_time_off_types()
    {
        var settings = Settings();

        var bereavement = settings.AddTimeOffType("Bereavement", 8m);

        Assert.Contains(bereavement, settings.TimeOffTypes);
        Assert.False(bereavement.IsVacation);
    }

    [Fact]
    public void Time_off_type_names_must_be_unique() =>
        Assert.Throws<DomainException>(() => Settings().AddTimeOffType(" holiday ", 8m));

    [Fact]
    public void The_vacation_type_cannot_be_archived()
    {
        var settings = Settings();

        Assert.Throws<DomainException>(() => settings.ArchiveTimeOffType(settings.VacationType.Id));
    }

    [Fact]
    public void Time_off_hours_allow_at_most_two_decimal_places() =>
        Assert.Throws<DomainException>(() => Settings().AddTimeOffType("Personal", 7.125m));
}
