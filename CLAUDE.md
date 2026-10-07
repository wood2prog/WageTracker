# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

All four layers exist: domain, application, infrastructure, and the WinForms UI. Requirements come from [Specs.txt](Specs.txt). When you add projects or commands, update this file to match.

## Build and test

.NET 10, solution file `WageTracker.slnx`.

- Build: `dotnet build`
- Test: `dotnet test`
- One test: `dotnet test --filter "FullyQualifiedName~PayPeriodScheduleTests"`
- Run the app: `dotnet run --project src/WageTracker.WinForms`. It uses the real database under `%LOCALAPPDATA%` and writes a backup to Documents when it closes.
- Build the installer: `dotnet build installer -c Release` produces `installer\bin\Release\WageTracker.msi`.

Layout:

- `src/WageTracker.Domain` has no dependencies and targets `net10.0`. Folders: `Calendar` (WorkWeek), `Employees`, `TimeTracking` (TimeEntry, WeeklyHours), `TimeOff`, `Payroll` (schedules, `PayrollSettings`, `PayStatement` calculation, and `PayrollRun` locking).
- `src/WageTracker.Application` references only Domain. It contains:
  - `Abstractions`: the ports Infrastructure implements. These are the repositories (in `Repositories.cs`), `IPayrollReportWriter`, and `IClock`.
  - One use-case service per area: `EmployeeService`, `TimeEntryService`, `TimeOffService`, `SettingsService`, `HolidayService`, and `PayrollService`.
  - `Reports`: the pay period reports the Payroll tab lists. Each implements `IPeriodReport` (name, whether the period must be finalized, `CreateAsync(date)`) and is registered in `AddWageTrackerApplication()`, in list order. `ReportService` lists them and creates one by name. There are two: `PayrollPeriodReport` re-exports the accountant's report (finalized periods only), and `HoursSummaryReport` lists each employee's total compensated hours (open periods give a preview). Each has its own writer port (`IPayrollReportWriter`, `IHoursReportWriter`).
  - `AddWageTrackerApplication()`, which registers the services.
- Services take and return DTO records and never hand domain entities to the UI. Every change goes through a domain method. Before changing a date or time, a service checks it isn't in a locked pay period.
- Repositories save each write immediately. No use case changes more than one aggregate, so there is no unit of work.
- `src/WageTracker.Infrastructure` references Application. It contains:
  - SQLite repositories written by hand with `Microsoft.Data.Sqlite`. There is no ORM: repositories rebuild domain objects through their public load constructors.
  - The QuestPDF report writers (Community license). `PdfLayout` holds the page they share: header with company and logo, period, preview notice, and page numbers.
  - `SqliteDatabaseBackup`, which makes the backups.
  - `AddWageTrackerInfrastructure()`, which registers them.
- `src/WageTracker.WinForms` (`net10.0-windows`, assembly `WageTracker.exe`) is the composition root and references Application and Infrastructure. It contains:
  - `Program`: wires DI, opens `SettingsForm` in first-time-setup mode when `SettingsService.IsConfiguredAsync()` is false (and exits if setup is closed unsaved), runs `MainForm`, then calls `BackupService.BackupAsync()` on exit.
  - `MainForm`: a File menu (Settings, open reports/backups folders) and tabs for `EmployeesView`, `TimeView` (one employee's work week: time entries, or the weekly total for weekly employees), and `PayrollView` (preview, finalize, and a report drop-down with a Create button). Each tab implements `IView` and reloads when selected.
  - `Settings/SettingsForm`: the General tab is edited and saved as a whole through `SaveGeneralAsync`/`InitializeAsync(GeneralSettingsInput)`. Time-off types and holidays save on each change.
  - Forms are laid out in code, not the designer, using the helpers in `Common/Build.cs`. Dialogs derive from `DialogForm`, whose OK button runs `SaveAsync` and stays open if it throws. Wrap service calls in `Ui.RunAsync`, which shows the message for the expected exceptions (plus the UI's own `InputException`). All message boxes go through `Ui.MessageBoxShow` so tests can record them.
  - Hours can be typed as `7.5` or `7:30`. `Formats.TryParseHours` converts them to decimal hours before they reach a service.
  - `Resources/WageTracker.png` is the source logo. `WageTracker.ico` is made from it (16–256 px) and serves as both the exe icon and, embedded, the icon `AppForm` gives every form.
  - Watch the `System.Windows.Forms.Application` vs. `WageTracker.Application` namespace clash; alias it as `WinFormsApp`.
- `installer/` is a WiX 6 SDK project (`WageTracker.Installer.wixproj` and `Package.wxs`) and isn't in the solution.
  - Before building, it publishes the WinForms app self-contained for win-x64 into `%TEMP%\WageTracker.Installer\publish`. The staging folder is kept outside OneDrive because sync placeholders block its cleanup.
  - The MSI installs per machine to Program Files, with Start menu and desktop shortcuts and a WixUI_InstallDir UI that has no license page.
  - The package version comes from `WageTracker.exe`, so bump `<Version>` in the WinForms csproj for each release. Never change the `UpgradeCode`.
- `StorageOptions` sets the storage locations. The database defaults to `%LOCALAPPDATA%\WageTracker\wagetracker.db`, outside OneDrive. Reports default to `Documents\WageTracker Reports`, and backups to `Documents\WageTracker Backups`.
- **Schema**: migrations live in `Persistence/Schema.cs` and run automatically on first connection, tracked with `PRAGMA user_version`. Never edit a shipped migration; append a new one. Decimals are stored as invariant text so rates and money stay exact. Dates are `yyyy-MM-dd`, and date-times are local `yyyy-MM-ddTHH:mm:ss.fffffff`.
- Tests use xUnit. Application tests use the in-memory fakes in `tests/WageTracker.Application.Tests/Fakes.cs`. Infrastructure tests use a temp-folder database (`TempDatabase`) and include end-to-end tests that wire the real DI container. `tests/WageTracker.WinForms.Tests` has smoke tests that open the real forms on an STA thread against a temp database and pump messages until the async loads finish.
- Business rule violations throw `DomainException`. A missing record throws `NotFoundException`, and missing settings throw `SettingsNotConfiguredException`. The UI shows the message for all three.

## Purpose

A desktop app for tracking employee time and wages.

## Intended architecture (layered)

Dependencies point inward, toward Domain:

1. **UI**: WinForms.
2. **Application / Services**: use cases and orchestration.
3. **Domain / Core**: entities and business rules. **The domain model is the source of truth.** Changes anywhere in the app should flow through the model, and the UI and persistence should reflect it rather than hold their own copies of the rules.
4. **Infrastructure**: SQLite, files, and external APIs.

Domain entities named in the spec: `Employee`, `TimeEntry`, `CompensatedTimeOff` (covers vacation, holiday, and sick days), and `PayPeriod`. The spec's "etc." means more entities are expected.

## Domain rules from the spec

- **TimeEntry**: a start date/time and an end date/time. An entry can't last longer than 24 hours, can't end in the future, and can't overlap another entry for the same employee. Back-to-back entries are allowed. New entries go through `TimeEntry.Record`.
- **Daily or weekly hours**: each employee's `TimeRecording` says how hours worked are entered.
  - `Daily`: time entries with a start and end.
  - `Weekly`: one `WeeklyHours` total per work week, going through `WeeklyHours.Record`. The week must have started, and the total must be more than 0 and at most 24 hours for each day employed that week.
  - New time entries require `Daily`, and new weekly totals require `Weekly`. Switching keeps what was already entered, and it is still paid.
  - A week can't have both time entries and a weekly total. A week's worked hours are its entries plus its total.
  - `TimeEntryService` handles both.
- **Hours and money**: the model stores both as decimals with two decimal places (0.00). The UI may accept hour:minute input, but it converts that to decimal hours before it reaches the model.
- **Work week**: the spec says "Saturday midnight to Saturday midnight," which means **Sunday 00:00 up to, but not including, the next Sunday 00:00**. Sunday is the first day of the week and Saturday is the last.
- **Pay period**: the user picks one week, two weeks, or one month. Pay periods are built from whole weeks. For the monthly option, **a week belongs to the month its Sunday falls in**. The monthly period runs from the first Sunday of the month through the Saturday after the last Sunday of the month, so it can end in the next month. Any days before the month's first Sunday belong to the previous month's period. Example: September 2026 runs Sun Sept 6 – Sat Oct 3, and Sept 1–5 fall in August's period.
  - **Two-week option**: the user sets an anchor Sunday, and periods repeat every 14 days from it without regard to month boundaries.
- **Payout date**: a fixed day of the month from 1 to 28, set in settings. Capping it at 28 means the day exists in every month. A period is paid on the first payout day that falls strictly after its last Saturday. This applies to every schedule, so several weekly periods can share one payout date. It can also push the payout into the month after next. Example: September's period ends Oct 3, so with a payout day of the 1st it's paid Nov 1.
- **Settings page**: all of these live in `PayrollSettings` and are set there:
  - the payout day and pay schedule;
  - the overtime threshold and the "time off counts toward overtime" option;
  - time-off types and the holiday calendar;
  - the company name (optional, up to 100 characters) and logo (optional PNG or JPEG, up to 2 MB, stored in the database), both shown in the report header;
  - the number of backups to keep (1–365, default 10).
- **Backups**:
  - The UI calls `BackupService.BackupAsync()` when the app exits.
  - Each backup is a consistent SQLite online-backup copy named `WageTracker yyyy-MM-dd HH-mm-ss.db`, saved in `Documents\WageTracker Backups`.
  - After each backup, the oldest backups beyond the configured count are deleted. Only files whose names match that exact pattern are ever deleted.
- **Employee**: first and last name, birthdate, hire date, optional end date, full-time or part-time, overtime percentage, vacation days permitted, and more fields to come.
- **Employment dates**:
  - Both the hire date and the end date are inclusive.
  - Time entries and time off outside employment are refused. An entry that ends exactly at midnight after the last day is allowed.
  - Holidays are credited only on days the employee was employed.
  - Payroll includes only employees who were employed on at least one day of the period.
  - Salaried pay for a partial week is weekly salary ÷ 5 for each Monday–Friday employed.
  - The vacation allowance is **not** prorated in the hire or end year.
  - In a leaver's final period (the one containing their end date), unused vacation is paid out, and salaried overtime earned that period is paid then instead of being deferred.
- **Full-time vs. part-time**: only full-time employees get compensated time off (holidays, vacation, and other types) and the year-end vacation payout. Part-time employees are paid only for hours worked, plus overtime.
- **Compensation types**: hourly and salary. Salary pays annual ÷ 52 for each week in the period.
- **Overtime**:
  - Overtime is hours over the weekly threshold. The default threshold is 40, and it can be changed in settings.
  - Overtime is figured for each week, never across a whole pay period.
  - By default, compensated time off counts toward the threshold ("regardless how it was come by"). A setting turns this off.
  - The overtime percentage is a premium on top of the rate: 50 means 1.5×.
  - Salaried employees get overtime only if their compensation is marked overtime-eligible. The hourly rate for a salaried employee is weekly salary ÷ threshold. Salaried overtime is recorded on the week it's earned but paid in the **next** pay period.
- **Compensated time-off types**: users can add types in settings. Each type carries its default hours per day, and together those defaults are the global table. The built-in Vacation and Holiday types can't be archived. Types in use are archived, never deleted.
  - Each employee can have their own table of hours per type. It is optional.
  - Lookup rule: use the employee's value if one is set, and otherwise fall back to the type's default. Keep this resolution in the domain layer.
  - Each booking is one whole day; partial days aren't supported. An employee can have at most one day off per date.
- **Company holidays**: holidays are never booked for individual employees. The holiday calendar in settings credits every full-time employee automatically, at the Holiday type's hours (after any employee override).
  - Each holiday has a recurring rule: a fixed date (Dec 25), the nth weekday of a month (4th Thursday of November), or the last weekday of a month (last Monday of May).
  - When the rule's date is inconvenient, such as on a weekend, the user sets an observed date for that year. It must be within 7 days of the rule's date and can fall in the neighboring year.
  - No other time off can be booked on a company holiday.
- **Vacation**: the permitted days are per calendar year and are checked off as they're used. Booking beyond the allowance is refused. Unused days are paid out in the last pay period of the year, meaning the period that contains Dec 31. The payout is days × vacation hours × hourly rate. For salaried employees, that rate is weekly salary ÷ threshold.
- **Locking**: when the report for the payroll accountant is created, the period's `PayrollRun` locks. It stores each `PayStatement` as a snapshot, and nothing dated inside a locked period can change.
  - Periods are finalized **in order**: only the period right after the latest locked one can be finalized, and only after it has ended.
  - The report is a **PDF**. For each employee it shows the pay lines (base, overtime including carried-over overtime, deferred overtime, vacation payout, gross) and a week-by-week table of worked, time-off, regular, and overtime hours. Grand totals come at the end. Infrastructure writes it through `IPayrollReportWriter`.
- **Changing the pay schedule**: a new schedule takes effect the day after the last locked period (`PayrollSettings.ScheduleEffectiveFrom`). The first new period is trimmed to start on that Sunday. Dates before it belong to locked runs, so look those runs up rather than asking the schedule.
