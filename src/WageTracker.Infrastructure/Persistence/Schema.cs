namespace WageTracker.Infrastructure.Persistence;

/// <summary>
/// Database migrations, applied in order. The database's <c>PRAGMA user_version</c> records how many have run.
/// Never edit a migration that has shipped; add a new one.
/// </summary>
/// <remarks>
/// Storage conventions: IDs are Guid text; dates are "yyyy-MM-dd"; date-times are local, "yyyy-MM-ddTHH:mm:ss.fffffff",
/// so text comparison orders them; decimals are invariant-culture text so no precision is lost; enums are their names.
/// </remarks>
internal static class Schema
{
    public static readonly string[] Migrations =
    [
        """
        CREATE TABLE settings (
            id                              INTEGER PRIMARY KEY CHECK (id = 1),
            frequency                       TEXT NOT NULL,
            biweekly_anchor                 TEXT NULL,
            schedule_effective_from         TEXT NULL,
            payout_day_of_month             INTEGER NOT NULL,
            overtime_threshold_hours        TEXT NOT NULL,
            time_off_counts_toward_overtime INTEGER NOT NULL
        );

        CREATE TABLE time_off_types (
            id                    TEXT PRIMARY KEY,
            name                  TEXT NOT NULL,
            default_hours_per_day TEXT NOT NULL,
            kind                  TEXT NOT NULL,
            is_archived           INTEGER NOT NULL,
            sort_order            INTEGER NOT NULL
        );

        CREATE TABLE company_holidays (
            id          TEXT PRIMARY KEY,
            name        TEXT NOT NULL,
            rule_kind   TEXT NOT NULL,
            month       INTEGER NOT NULL,
            day         INTEGER NULL,
            day_of_week INTEGER NULL,
            occurrence  INTEGER NULL,
            sort_order  INTEGER NOT NULL
        );

        CREATE TABLE holiday_observed_dates (
            holiday_id TEXT NOT NULL REFERENCES company_holidays (id) ON DELETE CASCADE,
            year       INTEGER NOT NULL,
            date       TEXT NOT NULL,
            PRIMARY KEY (holiday_id, year)
        );

        CREATE TABLE employees (
            id                      TEXT PRIMARY KEY,
            first_name              TEXT NOT NULL,
            last_name               TEXT NOT NULL,
            birth_date              TEXT NOT NULL,
            hire_date               TEXT NOT NULL,
            end_date                TEXT NULL,
            employment_type         TEXT NOT NULL,
            compensation_type       TEXT NOT NULL,
            compensation_amount     TEXT NOT NULL,
            overtime_eligible       INTEGER NOT NULL,
            overtime_percentage     TEXT NOT NULL,
            vacation_days_permitted INTEGER NOT NULL
        );

        CREATE TABLE employee_time_off_hours (
            employee_id      TEXT NOT NULL REFERENCES employees (id) ON DELETE CASCADE,
            time_off_type_id TEXT NOT NULL REFERENCES time_off_types (id),
            hours_per_day    TEXT NOT NULL,
            PRIMARY KEY (employee_id, time_off_type_id)
        );

        CREATE TABLE time_entries (
            id          TEXT PRIMARY KEY,
            employee_id TEXT NOT NULL REFERENCES employees (id),
            start       TEXT NOT NULL,
            "end"       TEXT NOT NULL
        );
        CREATE INDEX ix_time_entries_employee_start ON time_entries (employee_id, start);

        CREATE TABLE time_off (
            id               TEXT PRIMARY KEY,
            employee_id      TEXT NOT NULL REFERENCES employees (id),
            date             TEXT NOT NULL,
            time_off_type_id TEXT NOT NULL REFERENCES time_off_types (id)
        );
        CREATE INDEX ix_time_off_employee_date ON time_off (employee_id, date);

        CREATE TABLE payroll_runs (
            id           TEXT PRIMARY KEY,
            period_start TEXT NOT NULL UNIQUE,
            period_end   TEXT NOT NULL,
            week_count   INTEGER NOT NULL,
            payout_date  TEXT NOT NULL,
            status       TEXT NOT NULL,
            locked_at    TEXT NULL
        );

        CREATE TABLE pay_statements (
            run_id                 TEXT NOT NULL REFERENCES payroll_runs (id) ON DELETE CASCADE,
            employee_id            TEXT NOT NULL REFERENCES employees (id),
            compensation_type      TEXT NOT NULL,
            hourly_rate            TEXT NOT NULL,
            overtime_multiplier    TEXT NOT NULL,
            base_pay               TEXT NOT NULL,
            overtime_pay           TEXT NOT NULL,
            deferred_overtime_pay  TEXT NOT NULL,
            vacation_days_paid_out INTEGER NOT NULL,
            vacation_payout        TEXT NOT NULL,
            PRIMARY KEY (run_id, employee_id)
        );

        CREATE TABLE pay_statement_weeks (
            run_id         TEXT NOT NULL,
            employee_id    TEXT NOT NULL,
            week_start     TEXT NOT NULL,
            worked_hours   TEXT NOT NULL,
            time_off_hours TEXT NOT NULL,
            overtime_hours TEXT NOT NULL,
            PRIMARY KEY (run_id, employee_id, week_start),
            FOREIGN KEY (run_id, employee_id) REFERENCES pay_statements (run_id, employee_id) ON DELETE CASCADE
        );
        """,

        """
        ALTER TABLE settings ADD COLUMN company_name TEXT NULL;
        ALTER TABLE settings ADD COLUMN company_logo BLOB NULL;
        ALTER TABLE settings ADD COLUMN backups_to_keep INTEGER NOT NULL DEFAULT 10;
        """,

        """
        ALTER TABLE employees ADD COLUMN time_recording TEXT NOT NULL DEFAULT 'Daily';

        CREATE TABLE weekly_hours (
            employee_id TEXT NOT NULL REFERENCES employees (id),
            week_start  TEXT NOT NULL,
            hours       TEXT NOT NULL,
            PRIMARY KEY (employee_id, week_start)
        );
        """,

        // Statements locked before this only knew overtime eligibility through the employee, so take it from there.
        """
        ALTER TABLE pay_statements ADD COLUMN overtime_eligible INTEGER NOT NULL DEFAULT 1;
        UPDATE pay_statements
        SET overtime_eligible = COALESCE((SELECT e.overtime_eligible FROM employees e WHERE e.id = pay_statements.employee_id), 0)
        WHERE compensation_type = 'Salary';
        """,
    ];
}
