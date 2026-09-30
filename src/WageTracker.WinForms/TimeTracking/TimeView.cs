using WageTracker.Application.Employees;
using WageTracker.Application.Settings;
using WageTracker.Application.TimeOff;
using WageTracker.Application.TimeTracking;
using WageTracker.Domain.Calendar;
using WageTracker.Domain.Employees;
using WageTracker.Domain.TimeOff;
using WageTracker.WinForms.Common;

namespace WageTracker.WinForms.TimeTracking;

/// <summary>
/// One employee's hours and days off for one work week (Sunday through Saturday). Hours are time entries, or one
/// weekly total for employees whose hours are entered weekly.
/// </summary>
internal sealed class TimeView : UserControl, IView
{
    private readonly EmployeeService _employees;
    private readonly TimeEntryService _timeEntries;
    private readonly TimeOffService _timeOff;
    private readonly SettingsService _settings;
    private readonly HolidayService _holidays;

    private readonly ComboBox _employee = Build.DropDown(240);
    private readonly DateTimePicker _weekOf = Build.DatePicker();
    private readonly Label _summary = Build.Text(bold: true);
    private readonly Label _details = Build.Text();
    private readonly TextBox _weeklyHours = new() { Width = 80, TextAlign = HorizontalAlignment.Right };
    private readonly Label _weeklyStatus = Build.Text();
    private readonly FlowLayoutPanel _weeklyBar;
    private readonly FlowLayoutPanel _dailyBar;
    private readonly DataGridView _entries;
    private readonly DataGridView _daysOff;

    private WorkWeek _week = WorkWeek.Containing(DateOnly.FromDateTime(DateTime.Today));
    private bool _loading;

    public TimeView(EmployeeService employees, TimeEntryService timeEntries, TimeOffService timeOff, SettingsService settings, HolidayService holidays)
    {
        _employees = employees;
        _timeEntries = timeEntries;
        _timeOff = timeOff;
        _settings = settings;
        _holidays = holidays;

        // Set before the handler is attached, so building the view doesn't start a load.
        _weekOf.SetDate(_week.Start);
        _employee.SelectedIndexChanged += async (_, _) => { if (!_loading) await Ui.RunAsync(this, LoadWeekAsync); };
        _weekOf.ValueChanged += async (_, _) => { if (!_loading) await ShowWeekAsync(WorkWeek.Containing(_weekOf.ToDateOnly())); };

        _entries = Build.Grid()
            .Column("Day", nameof(EntryRow.Day), 120)
            .Column("Start", nameof(EntryRow.Start), 90)
            .Column("End", nameof(EntryRow.End), 110)
            .Column("Hours", nameof(EntryRow.Hours), 70, right: true);
        _entries.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OnEditEntry(this, EventArgs.Empty); };

        _weeklyHours.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            OnSaveWeeklyHours(this, EventArgs.Empty);
        };
        _weeklyBar = Build.Bar(
            Build.Text("Hours worked this week"), _weeklyHours,
            Build.Button("Save hours", OnSaveWeeklyHours),
            Build.Button("Clear", OnClearWeeklyHours),
            _weeklyStatus);
        _dailyBar = Build.Bar(
            Build.Button("Add entry...", OnAddEntry),
            Build.Button("Edit...", OnEditEntry),
            Build.Button("Delete", OnDeleteEntry));

        _daysOff = Build.Grid()
            .Column("Day", nameof(DayOffRow.Day), 120)
            .Column("Type", nameof(DayOffRow.Type), 120)
            .Column("Hours", nameof(DayOffRow.Hours), 70, right: true);

        var split = Build.Split(
            Build.Group("Time worked", _weeklyBar, _dailyBar, _entries),
            Build.Group("Days off", Build.Bar(
                Build.Button("Book day off...", OnBookDayOff),
                Build.Button("Cancel day off", OnCancelDayOff)), _daysOff),
            topShare: 0.6);

        var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        header.Controls.Add(Build.Bar(
            Build.Text("Employee"), _employee,
            Build.Text("Week of"), _weekOf,
            Build.Button("◀ Previous week", async (_, _) => await ShowWeekAsync(_week.Previous())),
            Build.Button("This week", async (_, _) => await ShowWeekAsync(WorkWeek.Containing(DateOnly.FromDateTime(DateTime.Today)))),
            Build.Button("Next week ▶", async (_, _) => await ShowWeekAsync(_week.Next()))));
        header.Controls.Add(_summary);
        header.Controls.Add(_details);

        Controls.Add(split);
        Controls.Add(header);
    }

    private EmployeeDto? Employee => _employee.SelectedValue<EmployeeDto>();

    /// <summary>Reloads the employee list, keeping the selected employee, then the week.</summary>
    public async Task ReloadAsync()
    {
        var selectedId = Employee?.Id;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var employees = await _employees.ListAsync();

        _loading = true;
        try
        {
            _employee.SetChoices(employees.Select(e => new Choice<EmployeeDto>(
                e.EndDate < today ? $"{e.LastName}, {e.FirstName} (former)" : $"{e.LastName}, {e.FirstName}", e)));
            _employee.Select(employees.FirstOrDefault(e => e.Id == selectedId) ?? employees.FirstOrDefault(e => !(e.EndDate < today)));
        }
        finally
        {
            _loading = false;
        }
        await LoadWeekAsync();
    }

    private async Task ShowWeekAsync(WorkWeek week)
    {
        _week = week;
        _loading = true;
        _weekOf.SetDate(week.Start);
        _loading = false;
        await Ui.RunAsync(this, LoadWeekAsync);
    }

    private async Task LoadWeekAsync()
    {
        var holidays = await _holidays.ListObservedAsync(_week.Start, _week.End);
        var holidayText = holidays.Count == 0
            ? ""
            : "   Company holidays: " + string.Join(", ", holidays.Select(h => $"{h.Name} ({h.Date:ddd MMM d})"));

        if (Employee is not { } employee)
        {
            ShowTimeRecording(weekly: false, hasEntries: false);
            _entries.Bind(Array.Empty<EntryRow>());
            _daysOff.Bind(Array.Empty<DayOffRow>());
            _summary.Text = $"Week of {Formats.Range(_week.Start, _week.End)}";
            _details.Text = "Add an employee on the Employees tab to record time." + holidayText;
            return;
        }

        var selectedEntry = _entries.Selected<EntryRow>()?.Entry.Id;
        var entries = await _timeEntries.ListAsync(employee.Id, _week.Start, _week.End);
        var daysOff = await _timeOff.ListAsync(employee.Id, _week.Start, _week.End);
        _entries.Bind(entries.Select(EntryRow.From).ToList(), r => r.Entry.Id == selectedEntry);
        _daysOff.Bind(daysOff.Select(DayOffRow.From).ToList());

        var weekly = employee.TimeRecording == TimeRecording.Weekly;
        ShowTimeRecording(weekly, entries.Count > 0);
        if (weekly)
        {
            var total = await _timeEntries.GetWeeklyHoursAsync(employee.Id, _week.Start);
            _weeklyHours.Text = total is null ? "" : Formats.Hours(total.Hours);
            _weeklyStatus.Text = entries.Count > 0
                ? "This week has time entries from daily entry. Delete them to enter a weekly total."
                : total is null ? "Not entered yet. Type the total, such as 40 or 38:30, and press Enter." : "Saved.";
        }

        // An entry that crosses Saturday midnight counts partly toward each week.
        var worked = await _timeEntries.HoursWorkedAsync(employee.Id, _week.Start, _week.End);
        _summary.Text = $"Week of {Formats.Range(_week.Start, _week.End)}   ·   Worked {Formats.Hours(worked)} h" +
            $"   ·   Days off {Formats.Hours(daysOff.Sum(d => d.Hours))} h";

        if (employee.EmploymentType == EmploymentType.FullTime)
        {
            var vacation = await _employees.GetVacationBalanceAsync(employee.Id, _week.Start.Year);
            _details.Text = $"Vacation {vacation.Year}: {vacation.Used} of {vacation.Permitted} days used, {vacation.Remaining} left.{holidayText}";
        }
        else
        {
            _details.Text = "Part-time: paid for hours worked only." + holidayText;
        }
    }

    /// <summary>
    /// Shows the weekly total for employees whose hours are entered weekly, and the time entries for everyone else.
    /// Entries recorded before an employee switched to weekly stay visible so they can be deleted.
    /// </summary>
    private void ShowTimeRecording(bool weekly, bool hasEntries)
    {
        _weeklyBar.Visible = weekly;
        _dailyBar.Visible = _entries.Visible = !weekly || hasEntries;
    }

    private async void OnSaveWeeklyHours(object? sender, EventArgs e)
    {
        if (Employee is not { } employee)
            return;
        await Ui.RunAsync(this, async () =>
        {
            var hours = Formats.RequireHours(_weeklyHours.Text, "weekly total");
            await _timeEntries.SetWeeklyHoursAsync(employee.Id, _week.Start, hours);
            await LoadWeekAsync();
        });
    }

    private async void OnClearWeeklyHours(object? sender, EventArgs e)
    {
        if (Employee is not { } employee)
            return;
        if (!Ui.Confirm(this, $"Clear {employee.FullName}'s hours for the week of {Formats.Range(_week.Start, _week.End)}?"))
            return;
        await Ui.RunAsync(this, async () =>
        {
            await _timeEntries.ClearWeeklyHoursAsync(employee.Id, _week.Start);
            await LoadWeekAsync();
        });
    }

    private async void OnAddEntry(object? sender, EventArgs e)
    {
        if (Employee is not { } employee)
            return;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var day = _week.Contains(today) ? today : _week.Start.AddDays(1);
        var start = day.ToDateTime(new TimeOnly(8, 0));

        using var form = new TimeEntryForm($"Add time for {employee.FullName}", start, start.AddHours(8),
            (s, end) => _timeEntries.RecordAsync(employee.Id, s, end));
        if (form.ShowDialog(this) == DialogResult.OK)
            await Ui.RunAsync(this, LoadWeekAsync);
    }

    private async void OnEditEntry(object? sender, EventArgs e)
    {
        if (Employee is not { } employee || _entries.Selected<EntryRow>() is not { } row)
            return;
        using var form = new TimeEntryForm($"Edit time for {employee.FullName}", row.Entry.Start, row.Entry.End,
            (start, end) => _timeEntries.RescheduleAsync(row.Entry.Id, start, end));
        if (form.ShowDialog(this) == DialogResult.OK)
            await Ui.RunAsync(this, LoadWeekAsync);
    }

    private async void OnDeleteEntry(object? sender, EventArgs e)
    {
        if (_entries.Selected<EntryRow>() is not { } row)
            return;
        if (!Ui.Confirm(this, $"Delete the entry for {row.Day}, {row.Start} to {row.End}?"))
            return;
        await Ui.RunAsync(this, async () =>
        {
            await _timeEntries.DeleteAsync(row.Entry.Id);
            await LoadWeekAsync();
        });
    }

    private async void OnBookDayOff(object? sender, EventArgs e)
    {
        if (Employee is not { } employee)
            return;
        if (employee.EmploymentType != EmploymentType.FullTime)
        {
            Ui.ShowWarning(this, $"{employee.FullName} is part-time and does not get paid time off.");
            return;
        }

        IReadOnlyList<TimeOffTypeDto> types = [];
        if (!await Ui.RunAsync(this, async () => types = (await _settings.GetAsync()).TimeOffTypes))
            return;
        // Holidays are credited from the company calendar, never booked.
        var bookable = types.Where(t => t.Kind != TimeOffKind.Holiday && !t.IsArchived).ToList();

        var today = DateOnly.FromDateTime(DateTime.Today);
        using var form = new BookTimeOffForm(employee.FullName, bookable, _week.Contains(today) ? today : _week.Start.AddDays(1),
            (date, typeId) => _timeOff.BookAsync(employee.Id, date, typeId));
        if (form.ShowDialog(this) == DialogResult.OK)
            await ShowWeekAsync(WorkWeek.Containing(form.BookedDate));
    }

    private async void OnCancelDayOff(object? sender, EventArgs e)
    {
        if (_daysOff.Selected<DayOffRow>() is not { } row)
            return;
        if (!Ui.Confirm(this, $"Cancel {row.Type} on {row.Day}?"))
            return;
        await Ui.RunAsync(this, async () =>
        {
            await _timeOff.CancelAsync(row.DayOff.Id);
            await LoadWeekAsync();
        });
    }

    private sealed record EntryRow(TimeEntryDto Entry, string Day, string Start, string End, string Hours)
    {
        public static EntryRow From(TimeEntryDto e) => new(
            e,
            Formats.Date(DateOnly.FromDateTime(e.Start)),
            Formats.Time(e.Start),
            e.End.Date == e.Start.Date ? Formats.Time(e.End) : $"{e.End:ddd} {Formats.Time(e.End)}",
            Formats.Hours(e.Hours));
    }

    private sealed record DayOffRow(TimeOffDto DayOff, string Day, string Type, string Hours)
    {
        public static DayOffRow From(TimeOffDto t) => new(t, Formats.Date(t.Date), t.TypeName, Formats.Hours(t.Hours));
    }
}
