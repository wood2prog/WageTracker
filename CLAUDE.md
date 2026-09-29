# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

WageTracker is at the specification stage. The only source of requirements is [Specs.txt](Specs.txt), and there is no solution, project, or build tooling yet. When you scaffold or add build, test, or run commands, update this file to match.

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

- **TimeEntry**: a start date/time and an end date/time.
- **Work week**: the spec says "Saturday midnight to Saturday midnight," which means **Sunday 00:00 up to, but not including, the next Sunday 00:00**. Sunday is the first day of the week and Saturday is the last.
- **Pay period**: the user picks one week, two weeks, or one month. Pay periods are built from whole weeks. For the monthly option, **a week belongs to the month its Sunday falls in**. The monthly period runs from the first Sunday of the month through the Saturday after the last Sunday of the month, so it can end in the next month. Any days before the month's first Sunday belong to the previous month's period. Example: September 2026 runs Sun Sept 6 – Sat Oct 3, and Sept 1–5 fall in August's period.
  - **Two-week option**: the user sets an anchor Sunday, and periods repeat every 14 days from it without regard to month boundaries.
- **Payout date**: the user can choose it.
- **Employee**: first and last name, birthdate, full-time or part-time, overtime percentage, vacation days permitted, and more fields to come.
- **Compensation types**: hourly and salary.
- **Compensated time-off hours**:
  - A global table sets how many hours each type of time off is worth. For example, a holiday adds 8 hours to the weekly total, and a vacation day adds 10 hours.
  - Each employee can have their own table with the same shape. It is optional.
  - Lookup rule: use the employee's value if one is set, and otherwise fall back to the global value. Keep this resolution in the domain layer.
