# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

The domain model exists; the UI, application, and infrastructure layers do not yet. Requirements come from [Specs.txt](Specs.txt). When you add projects or commands, update this file to match.

## Build and test

.NET 10, solution file `WageTracker.slnx`.

- Build: `dotnet build`
- Test: `dotnet test`
- One test: `dotnet test --filter "FullyQualifiedName~PayPeriodScheduleTests"`

Layout:

- `src/WageTracker.Domain` has no dependencies and targets `net10.0`. Folders: `Calendar` (WorkWeek), `Employees`, `TimeTracking` (TimeEntry), `TimeOff`, `Payroll` (schedules, `PayrollSettings`, `PayStatement` calculation, and `PayrollRun` locking).
- `tests/WageTracker.Domain.Tests` uses xUnit.
- Business rule violations throw `DomainException`.

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
- **Hours and money**: the model stores both as decimals with two decimal places (0.00). The UI may accept hour:minute input, but it converts that to decimal hours before it reaches the model.
- **Work week**: the spec says "Saturday midnight to Saturday midnight," which means **Sunday 00:00 up to, but not including, the next Sunday 00:00**. Sunday is the first day of the week and Saturday is the last.
- **Pay period**: the user picks one week, two weeks, or one month. Pay periods are built from whole weeks. For the monthly option, **a week belongs to the month its Sunday falls in**. The monthly period runs from the first Sunday of the month through the Saturday after the last Sunday of the month, so it can end in the next month. Any days before the month's first Sunday belong to the previous month's period. Example: September 2026 runs Sun Sept 6 – Sat Oct 3, and Sept 1–5 fall in August's period.
  - **Two-week option**: the user sets an anchor Sunday, and periods repeat every 14 days from it without regard to month boundaries.
- **Payout date**: a fixed day of the month from 1 to 28, set in settings. Capping it at 28 means the day exists in every month. A period is paid on the first payout day that falls strictly after its last Saturday. This applies to every schedule, so several weekly periods can share one payout date. It can also push the payout into the month after next. Example: September's period ends Oct 3, so with a payout day of the 1st it's paid Nov 1.
- **Settings page**: the payout day, pay schedule, overtime threshold, "time off counts toward overtime" option, time-off types, and holiday calendar are all set there (`PayrollSettings`).
- **Employee**: first and last name, birthdate, full-time or part-time, overtime percentage, vacation days permitted, and more fields to come.
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
