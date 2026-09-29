# WageTracker

A Windows desktop app for tracking employee time and wages. It records hours worked and paid time off, calculates each pay period's payroll (including overtime, holidays, and vacation payouts), and produces a PDF report for your payroll accountant.

## Requirements

- Windows 10 or 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Running it

From the repository folder:

```
dotnet run --project src/WageTracker.WinForms
```

Or build once with `dotnet build` and run `src\WageTracker.WinForms\bin\Debug\net10.0-windows\WageTracker.exe`.

## First-time setup

The first time WageTracker starts, it opens the settings page before anything else. Choose:

- **Pay schedule**: every week, every two weeks, or monthly.
- **Payout day**: the day of the month (1–28) payroll is paid.
- **Overtime**: hours per week before overtime starts (40 by default), and whether paid time off counts toward it.
- **Company name and logo** (optional): shown at the top of the payroll report.
- **Backups to keep**: 10 by default.

Click **Save and continue**. You can then add time-off types and company holidays, or close the window to start. If you close setup without saving, WageTracker exits and asks again next time.

Everything can be changed later from **File > Settings**.

## Using WageTracker

### Employees tab

Add and edit employees: name, birth date, hire date, last day (for someone who has left), full-time or part-time, hourly rate or annual salary, overtime premium, and vacation days per year. **Time-off hours...** sets an employee's own hours per day for each type of time off. Types left blank use the default from settings.

### Time tab

Pick an employee and a week. Weeks run Sunday through Saturday.

- **Time worked**: add, edit, or delete entries with a start and end date and time. An entry can be at most 24 hours long, can't end in the future, and can't overlap another entry.
- **Days off**: book whole days of vacation, sick time, or other types you've added. Company holidays are credited automatically, so you never book them.

The header shows the week's hours, any company holidays, and how much vacation the employee has left this year.

### Payroll tab

Choose a pay period to see each employee's pay statement: base pay, overtime, deferred overtime, vacation payout, and gross pay. Select an employee to see their hours week by week.

- An open period shows a **preview** that updates as time is entered.
- **Finalize and create report...** locks the period and writes the PDF report. After that, nothing dated inside the period can be changed. Periods must be finalized in order, and only after they have ended.
- **Create report again** rewrites the PDF for a finalized period, for example if the first copy was lost.

### Typing hours

Anywhere you enter hours, you can type decimal hours (`7.5`) or hours and minutes (`7:30`).

## How pay is calculated

- **Pay periods** are made of whole weeks. A monthly period holds the weeks whose Sunday falls in that month, so it can end a few days into the next month.
- **Payday** is the first payout day after the period ends.
- **Overtime** is worked out week by week, for hours over the weekly threshold. The overtime premium is on top of the regular rate: 50% means time and a half. Salaried employees get overtime only if they are marked eligible, and it is paid in the following pay period.
- **Paid time off** (holidays, vacation, and other types you add) is only for full-time employees. Part-time employees are paid for hours worked plus overtime.
- **Unused vacation** is paid out in the last pay period of the year, and in an employee's final pay period when they leave.

## Where your data is kept

| What | Where |
| --- | --- |
| Database | `%LOCALAPPDATA%\WageTracker\wagetracker.db` |
| Payroll reports (PDF) | `Documents\WageTracker Reports` |
| Backups | `Documents\WageTracker Backups` |

The database is kept outside OneDrive on purpose, because file syncing can corrupt a database while it is open.

**Backups** are made automatically every time WageTracker closes. Each one is named `WageTracker yyyy-MM-dd HH-mm-ss.db`, and the oldest are deleted once there are more than the number set in settings. To restore one, close WageTracker, then copy the backup over `wagetracker.db` and rename it to `wagetracker.db`.

The reports and backups folders can be opened from the **File** menu.

## For developers

- Build: `dotnet build`
- Test: `dotnet test`

The solution (`WageTracker.slnx`) is layered, and each layer depends only on the ones beneath it:

| Project | Role |
| --- | --- |
| `src/WageTracker.Domain` | Entities and business rules. It is the source of truth for all pay rules. |
| `src/WageTracker.Application` | Use-case services that the UI calls, taking and returning DTOs. |
| `src/WageTracker.Infrastructure` | SQLite storage, the QuestPDF report writer, and backups. |
| `src/WageTracker.WinForms` | The desktop UI and the app's startup code. |

Each project has a matching test project under `tests/`. [CLAUDE.md](CLAUDE.md) has the detailed conventions and the full list of business rules, and [Specs.txt](Specs.txt) has the original requirements.
