using Microsoft.Data.Sqlite;
using WageTracker.Application.Abstractions;
using WageTracker.Domain.Payroll;
using WageTracker.Domain.TimeOff;

namespace WageTracker.Infrastructure.Persistence;

/// <summary>Saves the single settings record together with its time-off types and holiday calendar.</summary>
public sealed class SqlitePayrollSettingsRepository(SqliteConnectionFactory db) : IPayrollSettingsRepository
{
    public async Task<PayrollSettings?> GetAsync()
    {
        await using var connection = await db.OpenAsync();
        var row = (await connection.QueryAsync("SELECT * FROM settings WHERE id = 1", r => new
        {
            Frequency = r.Enum<PayFrequency>("frequency"),
            Anchor = r.DateOrNull("biweekly_anchor"),
            EffectiveFrom = r.DateOrNull("schedule_effective_from"),
            PayoutDay = r.Int("payout_day_of_month"),
            Threshold = r.Decimal("overtime_threshold_hours"),
            TimeOffCounts = r.Bool("time_off_counts_toward_overtime"),
            CompanyName = r.TextOrNull("company_name"),
            CompanyLogo = r.BlobOrNull("company_logo"),
            BackupsToKeep = r.Int("backups_to_keep"),
        })).SingleOrDefault();
        if (row is null)
            return null;

        var types = await connection.QueryAsync("SELECT * FROM time_off_types ORDER BY sort_order", r => new TimeOffType(
            r.Guid("id"), r.Text("name"), r.Decimal("default_hours_per_day"), r.Enum<TimeOffKind>("kind"), r.Bool("is_archived")));

        var observed = (await connection.QueryAsync("SELECT * FROM holiday_observed_dates",
                r => (HolidayId: r.Guid("holiday_id"), Year: r.Int("year"), Date: r.Date("date"))))
            .ToLookup(o => o.HolidayId);
        var holidays = await connection.QueryAsync("SELECT * FROM company_holidays ORDER BY sort_order", r =>
        {
            var id = r.Guid("id");
            return new CompanyHoliday(id, r.Text("name"), MapRule(r), observed[id].ToDictionary(o => o.Year, o => o.Date));
        });

        var schedule = row.Frequency switch
        {
            PayFrequency.Weekly => PayPeriodSchedule.Weekly(),
            PayFrequency.Monthly => PayPeriodSchedule.Monthly(),
            PayFrequency.BiWeekly => PayPeriodSchedule.BiWeekly(row.Anchor!.Value),
            _ => throw new InvalidOperationException($"Unknown pay frequency {row.Frequency} in the database."),
        };

        return new PayrollSettings(schedule, row.PayoutDay, types, new HolidayCalendar(holidays),
            row.Threshold, row.TimeOffCounts, row.EffectiveFrom, row.CompanyName, row.CompanyLogo, row.BackupsToKeep);
    }

    public async Task SaveAsync(PayrollSettings settings)
    {
        await using var connection = await db.OpenAsync();
        using var tx = connection.BeginTransaction();

        connection.Execute(tx, """
            INSERT INTO settings (id, frequency, biweekly_anchor, schedule_effective_from, payout_day_of_month,
                overtime_threshold_hours, time_off_counts_toward_overtime, company_name, company_logo, backups_to_keep)
            VALUES (1, $frequency, $anchor, $effective, $payoutDay, $threshold, $counts, $companyName, $companyLogo, $backups)
            ON CONFLICT (id) DO UPDATE SET frequency = excluded.frequency, biweekly_anchor = excluded.biweekly_anchor,
                schedule_effective_from = excluded.schedule_effective_from, payout_day_of_month = excluded.payout_day_of_month,
                overtime_threshold_hours = excluded.overtime_threshold_hours,
                time_off_counts_toward_overtime = excluded.time_off_counts_toward_overtime,
                company_name = excluded.company_name, company_logo = excluded.company_logo,
                backups_to_keep = excluded.backups_to_keep
            """,
            ("$frequency", settings.Schedule.Frequency), ("$anchor", settings.Schedule.Anchor),
            ("$effective", settings.ScheduleEffectiveFrom), ("$payoutDay", settings.PayoutDayOfMonth),
            ("$threshold", settings.OvertimeThresholdHours), ("$counts", settings.TimeOffCountsTowardOvertime),
            ("$companyName", settings.CompanyName), ("$companyLogo", settings.CompanyLogo),
            ("$backups", settings.BackupsToKeep));

        // Types are never deleted (days off refer to them), so they are upserted.
        for (var i = 0; i < settings.TimeOffTypes.Count; i++)
        {
            var type = settings.TimeOffTypes[i];
            connection.Execute(tx, """
                INSERT INTO time_off_types (id, name, default_hours_per_day, kind, is_archived, sort_order)
                VALUES ($id, $name, $hours, $kind, $archived, $order)
                ON CONFLICT (id) DO UPDATE SET name = excluded.name, default_hours_per_day = excluded.default_hours_per_day,
                    kind = excluded.kind, is_archived = excluded.is_archived, sort_order = excluded.sort_order
                """,
                ("$id", type.Id), ("$name", type.Name), ("$hours", type.DefaultHoursPerDay), ("$kind", type.Kind),
                ("$archived", type.IsArchived), ("$order", i));
        }

        // Nothing refers to holidays, so the calendar is replaced.
        connection.Execute(tx, "DELETE FROM company_holidays");
        for (var i = 0; i < settings.Holidays.Holidays.Count; i++)
            InsertHoliday(connection, tx, settings.Holidays.Holidays[i], i);

        tx.Commit();
    }

    private static void InsertHoliday(SqliteConnection connection, SqliteTransaction tx, CompanyHoliday holiday, int order)
    {
        var (kind, day, dayOfWeek, occurrence) = holiday.Rule switch
        {
            FixedDateRule r => ("FixedDate", (int?)r.Day, (int?)null, (int?)null),
            NthWeekdayRule r => ("NthWeekday", null, (int)r.DayOfWeek, r.Occurrence),
            LastWeekdayRule r => ("LastWeekday", null, (int)r.DayOfWeek, null),
            _ => throw new InvalidOperationException($"Unknown holiday rule {holiday.Rule.GetType().Name}."),
        };
        connection.Execute(tx, """
            INSERT INTO company_holidays (id, name, rule_kind, month, day, day_of_week, occurrence, sort_order)
            VALUES ($id, $name, $kind, $month, $day, $dow, $occurrence, $order)
            """,
            ("$id", holiday.Id), ("$name", holiday.Name), ("$kind", kind), ("$month", holiday.Rule.Month),
            ("$day", day), ("$dow", dayOfWeek), ("$occurrence", occurrence), ("$order", order));

        foreach (var (year, date) in holiday.ObservedDates)
        {
            connection.Execute(tx, "INSERT INTO holiday_observed_dates (holiday_id, year, date) VALUES ($id, $year, $date)",
                ("$id", holiday.Id), ("$year", year), ("$date", date));
        }
    }

    private static HolidayRule MapRule(SqliteDataReader r)
    {
        var month = r.Int("month");
        return r.Text("rule_kind") switch
        {
            "FixedDate" => HolidayRule.Fixed(month, r.Int("day")),
            "NthWeekday" => HolidayRule.NthWeekday(month, (DayOfWeek)r.Int("day_of_week"), r.Int("occurrence")),
            "LastWeekday" => HolidayRule.LastWeekday(month, (DayOfWeek)r.Int("day_of_week")),
            var kind => throw new InvalidOperationException($"Unknown holiday rule {kind} in the database."),
        };
    }
}
