using WageTracker.Application.Settings;
using WageTracker.Domain.Calendar;
using WageTracker.Domain.Payroll;
using WageTracker.Domain.TimeOff;
using WageTracker.WinForms.Common;

namespace WageTracker.WinForms.Settings;

/// <summary>
/// The settings page. The General tab is edited and then saved as a whole; changes on the Time-off types and Holidays
/// tabs are saved as they are made.
/// </summary>
/// <remarks>
/// In first-time setup only the General tab is shown, and saving it creates the settings. The other tabs appear once
/// that has happened.
/// </remarks>
internal sealed class SettingsForm : AppForm
{
    private readonly SettingsService _settings;
    private readonly HolidayService _holidays;
    private bool _configured;
    private bool _dirty;
    private bool _loading;

    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill, Padding = new Point(12, 4) };
    private readonly TabPage _typesPage = new("Time-off types") { Padding = new Padding(8), UseVisualStyleBackColor = true };
    private readonly TabPage _holidaysPage = new("Holidays") { Padding = new Padding(8), UseVisualStyleBackColor = true };

    // General
    private readonly ComboBox _frequency = Build.DropDown(180);
    private readonly DateTimePicker _anchor = Build.DatePicker();
    private readonly Label _scheduleNote = Note("");
    private readonly NumericUpDown _payoutDay = Build.Number(1, PayrollSettings.MaxPayoutDayOfMonth, width: 70);
    private readonly TextBox _overtimeThreshold = new() { Width = 70, TextAlign = HorizontalAlignment.Right };
    private readonly CheckBox _timeOffCounts = new() { Text = "Paid time off counts toward the overtime threshold", AutoSize = true };
    private readonly TextBox _companyName = new() { Width = 320, MaxLength = PayrollSettings.MaxCompanyNameLength };
    private readonly PictureBox _logo = new() { Size = new Size(200, 80), SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
    private readonly NumericUpDown _backupsToKeep = Build.Number(1, PayrollSettings.MaxBackupsToKeep, width: 70);
    private readonly Button _saveGeneral;
    private byte[]? _logoBytes;

    // Time-off types
    private readonly DataGridView _types;

    // Holidays
    private readonly NumericUpDown _holidayYear = Build.Number(1900, 2999, width: 80);
    private readonly DataGridView _holidayGrid;

    public SettingsForm(SettingsService settings, HolidayService holidays, bool firstTimeSetup)
    {
        _settings = settings;
        _holidays = holidays;
        _configured = !firstTimeSetup;

        Text = firstTimeSetup ? "Welcome to WageTracker — set up payroll" : "Settings";
        Size = new Size(760, 640);
        MinimumSize = new Size(640, 520);
        MinimizeBox = false;
        ShowInTaskbar = firstTimeSetup;
        if (firstTimeSetup)
            StartPosition = FormStartPosition.CenterScreen;

        _saveGeneral = Build.Button(firstTimeSetup ? "Save and continue" : "Save", OnSaveGeneral);
        _tabs.TabPages.Add(GeneralPage(firstTimeSetup));

        _types = Build.Grid()
            .Column("Name", nameof(TypeRow.Name), 160)
            .Column("Hours per day", nameof(TypeRow.Hours), 90, right: true)
            .Column("Kind", nameof(TypeRow.Kind), 200)
            .Column("Status", nameof(TypeRow.Status), 80);
        _types.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OnEditType(this, EventArgs.Empty); };
        _typesPage.Controls.Add(_types);
        _typesPage.Controls.Add(Note(
            "Hours per day are the defaults for every employee. An employee can have their own hours (Employees tab, " +
            "Time-off hours). Holiday hours are credited for each company holiday. Types in use are archived, not deleted.",
            DockStyle.Bottom));
        _typesPage.Controls.Add(Build.Bar(
            Build.Button("Add type...", OnAddType),
            Build.Button("Edit...", OnEditType),
            Build.Button("Archive", OnArchiveType),
            Build.Button("Restore", OnRestoreType)));

        _holidayYear.Value = DateTime.Today.Year;
        _holidayYear.ValueChanged += async (_, _) => await Ui.RunAsync(this, ReloadHolidaysAsync);
        _holidayGrid = Build.Grid()
            .Column("Holiday", nameof(HolidayRow.Name), 150)
            .Column("Rule", nameof(HolidayRow.Rule), 200)
            .Column("Date that year", nameof(HolidayRow.Date), 150)
            .Column("", nameof(HolidayRow.Observed), 90);
        _holidayGrid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OnEditHoliday(this, EventArgs.Empty); };
        _holidaysPage.Controls.Add(_holidayGrid);
        _holidaysPage.Controls.Add(Note(
            "Every full-time employee is credited the Holiday type's hours on each holiday. When a holiday falls on an " +
            "inconvenient day, set the date it is observed that year (within 7 days).",
            DockStyle.Bottom));
        _holidaysPage.Controls.Add(Build.Bar(
            Build.Text("Year"), _holidayYear,
            Build.Button("Add holiday...", OnAddHoliday),
            Build.Button("Edit...", OnEditHoliday),
            Build.Button("Set observed date...", OnSetObserved),
            Build.Button("Clear observed date", OnClearObserved),
            Build.Button("Remove", OnRemoveHoliday)));

        if (_configured)
            ShowAllTabs();

        var close = Build.Button("Close", (_, _) => Close());
        var bottom = Build.Bar(close);
        bottom.Dock = DockStyle.Bottom;
        bottom.FlowDirection = FlowDirection.RightToLeft;
        CancelButton = close;

        Controls.Add(_tabs);
        Controls.Add(bottom);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await Ui.RunAsync(this, async () =>
        {
            if (_configured)
            {
                ShowGeneral(await _settings.GetAsync());
                await ReloadHolidaysAsync();
            }
            else
            {
                ShowDefaults();
            }
        });
    }

    /// <summary>Offers to save unsaved changes on the General tab.</summary>
    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (!_dirty || e.Cancel)
            return;

        var answer = Ui.AskYesNoCancel(this, _configured
            ? "Save your changes to the general settings?"
            : "Payroll isn't set up yet, so WageTracker will close. Save the setup first?");
        if (answer == DialogResult.No)
            return;

        e.Cancel = true;
        if (answer == DialogResult.Yes && await SaveGeneralAsync(closing: true))
            Close();
    }

    // ---------------------------------------------------------------- General

    private TabPage GeneralPage(bool firstTimeSetup)
    {
        var fields = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Top, Padding = new Padding(4) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        void Add(string label, Control control)
        {
            control.Anchor = AnchorStyles.Left;
            fields.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 12, 3) });
            fields.Controls.Add(control);
        }

        void Explain(Label note)
        {
            fields.Controls.Add(new Label { AutoSize = true });
            fields.Controls.Add(note);
        }

        if (firstTimeSetup)
        {
            var welcome = Note("Choose how often employees are paid and when. You can change any of this later from File > Settings.");
            welcome.ForeColor = SystemColors.ControlText;
            fields.Controls.Add(welcome);
            fields.SetColumnSpan(welcome, 2);
        }

        _frequency.SetChoices([
            new("Every week", PayFrequency.Weekly),
            new("Every two weeks", PayFrequency.BiWeekly),
            new Choice<PayFrequency>("Monthly", PayFrequency.Monthly),
        ]);
        _frequency.SelectedIndexChanged += (_, _) => _anchor.Enabled = _frequency.SelectedValue<PayFrequency>() == PayFrequency.BiWeekly;

        Add("Pay schedule", _frequency);
        Explain(Note("Pay periods are whole weeks, Sunday through Saturday. A monthly period holds the weeks whose Sunday " +
            "falls in that month, so it can end a few days into the next month."));
        Add("Two-week cycle starts", _anchor);
        Explain(Note("For the two-week schedule: a Sunday that starts a pay period. Periods repeat every 14 days from it."));
        Explain(_scheduleNote);
        Add("Payout day of the month", _payoutDay);
        Explain(Note($"1 to {PayrollSettings.MaxPayoutDayOfMonth}. Each period is paid on the first payout day after it ends."));
        Add("Overtime after", Build.Row(_overtimeThreshold, Build.Text("hours per week")));
        Add("", _timeOffCounts);
        Add("Company name", _companyName);
        Add("Logo", Build.Row(_logo, Build.Button("Choose...", OnChooseLogo), Build.Button("Remove", OnRemoveLogo)));
        Explain(Note("The name and logo appear at the top of the payroll report. The logo must be a PNG or JPEG of at most 2 MB."));
        Add("Backups to keep", _backupsToKeep);
        Explain(Note("The database is backed up to Documents\\WageTracker Backups each time WageTracker closes."));
        fields.Controls.Add(new Label { AutoSize = true });
        fields.Controls.Add(_saveGeneral);

        foreach (var control in new Control[] { _frequency, _anchor, _payoutDay, _overtimeThreshold, _timeOffCounts, _companyName, _backupsToKeep })
            WatchForChanges(control);

        var page = new TabPage("General") { Padding = new Padding(8), UseVisualStyleBackColor = true, AutoScroll = true };
        page.Controls.Add(fields);
        return page;
    }

    private void WatchForChanges(Control control)
    {
        EventHandler changed = (_, _) => { if (!_loading) _dirty = true; };
        switch (control)
        {
            case ComboBox c: c.SelectedIndexChanged += changed; break;
            case DateTimePicker d: d.ValueChanged += changed; break;
            case NumericUpDown n: n.ValueChanged += changed; break;
            case CheckBox c: c.CheckedChanged += changed; break;
            default: control.TextChanged += changed; break;
        }
    }

    private void ShowDefaults()
    {
        _loading = true;
        _frequency.Select(PayFrequency.Weekly);
        _anchor.SetDate(WorkWeek.Containing(DateOnly.FromDateTime(DateTime.Today)).Start);
        _anchor.Enabled = false;
        _payoutDay.Value = 1;
        _overtimeThreshold.Text = Formats.Hours(PayrollSettings.DefaultOvertimeThresholdHours);
        _timeOffCounts.Checked = true;
        _backupsToKeep.Value = PayrollSettings.DefaultBackupsToKeep;
        _scheduleNote.Text = "";
        SetLogo(null);
        _loading = false;
        // Saving the defaults as they are is a valid setup, so offer to save them on close.
        _dirty = true;
    }

    private void ShowGeneral(PayrollSettingsDto s)
    {
        _loading = true;
        _frequency.Select(s.Frequency);
        _anchor.SetDate(s.BiWeeklyAnchor ?? WorkWeek.Containing(DateOnly.FromDateTime(DateTime.Today)).Start);
        _anchor.Enabled = s.Frequency == PayFrequency.BiWeekly;
        _payoutDay.Value = s.PayoutDayOfMonth;
        _overtimeThreshold.Text = Formats.Hours(s.OvertimeThresholdHours);
        _timeOffCounts.Checked = s.TimeOffCountsTowardOvertime;
        _companyName.Text = s.CompanyName ?? "";
        _backupsToKeep.Value = s.BackupsToKeep;
        _scheduleNote.Text = (s.ScheduleEffectiveFrom is { } from
            ? $"The current schedule took effect {Formats.Date(from)}. "
            : "") + "A new schedule takes effect the day after the last finalized pay period.";
        SetLogo(s.CompanyLogo);
        ShowTypes(s);
        _loading = false;
        _dirty = false;
    }

    private GeneralSettingsInput ReadGeneral()
    {
        var frequency = _frequency.SelectedValue<PayFrequency>();
        return new GeneralSettingsInput(
            new ScheduleInput(frequency, frequency == PayFrequency.BiWeekly ? _anchor.ToDateOnly() : null),
            (int)_payoutDay.Value,
            Formats.RequireHours(_overtimeThreshold.Text, "overtime threshold"),
            _timeOffCounts.Checked,
            _companyName.Text,
            _logoBytes,
            (int)_backupsToKeep.Value);
    }

    private async void OnSaveGeneral(object? sender, EventArgs e) => await SaveGeneralAsync(closing: false);

    /// <param name="closing">True when saving on the way out, so there is no point showing the other tabs.</param>
    private async Task<bool> SaveGeneralAsync(bool closing)
    {
        var firstSave = !_configured;
        var saved = await Ui.RunAsync(this, async () =>
        {
            var input = ReadGeneral();
            ShowGeneral(_configured ? await _settings.SaveGeneralAsync(input) : await _settings.InitializeAsync(input));
            _configured = true;
        });
        if (saved && firstSave && !closing)
        {
            _saveGeneral.Text = "Save";
            ShowAllTabs();
            await Ui.RunAsync(this, ReloadHolidaysAsync);
            Ui.ShowInfo(this, "Payroll is set up. You can add time-off types and company holidays on the other tabs now, " +
                "or close this window to start using WageTracker.");
        }
        return saved;
    }

    private void OnChooseLogo(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Title = "Choose a logo", Filter = "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        var info = new FileInfo(dialog.FileName);
        if (info.Length > PayrollSettings.MaxLogoBytes)
        {
            Ui.ShowWarning(this, "The logo image can be at most 2 MB.");
            return;
        }
        SetLogo(File.ReadAllBytes(dialog.FileName));
        _dirty = true;
    }

    private void OnRemoveLogo(object? sender, EventArgs e)
    {
        SetLogo(null);
        _dirty = true;
    }

    private void SetLogo(byte[]? bytes)
    {
        _logoBytes = bytes;
        var old = _logo.Image;
        _logo.Image = null;
        old?.Dispose();
        if (bytes is null)
            return;
        try
        {
            using var stream = new MemoryStream(bytes);
            using var image = Image.FromStream(stream);
            _logo.Image = new Bitmap(image);
        }
        catch (ArgumentException)
        {
            // Not an image GDI+ can read; saving reports whether it is acceptable.
        }
    }

    private void ShowAllTabs()
    {
        if (!_tabs.TabPages.Contains(_typesPage))
            _tabs.TabPages.AddRange([_typesPage, _holidaysPage]);
    }

    // ---------------------------------------------------------------- Time-off types

    private void ShowTypes(PayrollSettingsDto s) =>
        _types.Bind(s.TimeOffTypes.Select(TypeRow.From).ToList(), r => r.Type.Id == _types.Selected<TypeRow>()?.Type.Id);

    private void OnAddType(object? sender, EventArgs e)
    {
        using var form = new TimeOffTypeForm(null, async (name, hours) => ShowTypes(await _settings.AddTimeOffTypeAsync(name, hours)));
        form.ShowDialog(this);
    }

    private void OnEditType(object? sender, EventArgs e)
    {
        if (_types.Selected<TypeRow>() is not { } row)
            return;
        var type = row.Type;
        using var form = new TimeOffTypeForm(type, async (name, hours) =>
        {
            if (name.Trim() != type.Name)
                ShowTypes(await _settings.RenameTimeOffTypeAsync(type.Id, name));
            if (hours != type.DefaultHoursPerDay)
                ShowTypes(await _settings.SetTimeOffTypeHoursAsync(type.Id, hours));
        });
        form.ShowDialog(this);
    }

    private async void OnArchiveType(object? sender, EventArgs e)
    {
        if (_types.Selected<TypeRow>() is not { } row)
            return;
        if (Ui.Confirm(this, $"Archive {row.Name}? Days already booked keep it, but it can't be booked again until it is restored."))
            await Ui.RunAsync(this, async () => ShowTypes(await _settings.ArchiveTimeOffTypeAsync(row.Type.Id)));
    }

    private async void OnRestoreType(object? sender, EventArgs e)
    {
        if (_types.Selected<TypeRow>() is { } row)
            await Ui.RunAsync(this, async () => ShowTypes(await _settings.RestoreTimeOffTypeAsync(row.Type.Id)));
    }

    // ---------------------------------------------------------------- Holidays

    private async Task ReloadHolidaysAsync()
    {
        var year = (int)_holidayYear.Value;
        var holidays = await _holidays.ListAsync();
        var dates = (await _holidays.ListForYearAsync(year)).ToDictionary(h => h.HolidayId, h => h.Date);
        var selectedId = _holidayGrid.Selected<HolidayRow>()?.Holiday.Id;
        _holidayGrid.Bind(
            holidays.Select(h => HolidayRow.From(h, year, dates[h.Id])).OrderBy(r => r.SortDate).ToList(),
            r => r.Holiday.Id == selectedId);
    }

    private async void OnAddHoliday(object? sender, EventArgs e)
    {
        using var form = new HolidayForm(null, (name, rule) => _holidays.AddAsync(name, rule));
        if (form.ShowDialog(this) == DialogResult.OK)
            await Ui.RunAsync(this, ReloadHolidaysAsync);
    }

    private async void OnEditHoliday(object? sender, EventArgs e)
    {
        if (_holidayGrid.Selected<HolidayRow>() is not { } row)
            return;
        var holiday = row.Holiday;
        using var form = new HolidayForm(holiday, async (name, rule) =>
        {
            if (name.Trim() != holiday.Name)
                await _holidays.RenameAsync(holiday.Id, name);
            if (rule != holiday.Rule)
                await _holidays.ChangeRuleAsync(holiday.Id, rule);
        });
        form.ShowDialog(this);
        await Ui.RunAsync(this, ReloadHolidaysAsync);
    }

    private async void OnSetObserved(object? sender, EventArgs e)
    {
        if (_holidayGrid.Selected<HolidayRow>() is not { } row)
            return;
        var year = (int)_holidayYear.Value;
        using var form = new ObservedDateForm(row.Name, year, row.SortDate,
            date => _holidays.SetObservedDateAsync(row.Holiday.Id, year, date));
        if (form.ShowDialog(this) == DialogResult.OK)
            await Ui.RunAsync(this, ReloadHolidaysAsync);
    }

    private async void OnClearObserved(object? sender, EventArgs e)
    {
        if (_holidayGrid.Selected<HolidayRow>() is not { } row)
            return;
        var year = (int)_holidayYear.Value;
        await Ui.RunAsync(this, async () =>
        {
            await _holidays.ClearObservedDateAsync(row.Holiday.Id, year);
            await ReloadHolidaysAsync();
        });
    }

    private async void OnRemoveHoliday(object? sender, EventArgs e)
    {
        if (_holidayGrid.Selected<HolidayRow>() is not { } row)
            return;
        if (!Ui.Confirm(this, $"Remove {row.Name}? Future pay periods will no longer credit it. Finalized periods keep what they paid."))
            return;
        await Ui.RunAsync(this, async () =>
        {
            await _holidays.RemoveAsync(row.Holiday.Id);
            await ReloadHolidaysAsync();
        });
    }

    private static Label Note(string text, DockStyle dock = DockStyle.None)
    {
        var note = new Label
        {
            Text = text,
            AutoSize = dock == DockStyle.None,
            MaximumSize = dock == DockStyle.None ? new Size(440, 0) : Size.Empty,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 0, 3, 8),
            Dock = dock,
        };
        if (dock != DockStyle.None)
        {
            note.Height = 48;
            note.Padding = new Padding(0, 6, 0, 0);
        }
        return note;
    }

    private sealed record TypeRow(TimeOffTypeDto Type, string Name, string Hours, string Kind, string Status)
    {
        public static TypeRow From(TimeOffTypeDto t) => new(
            t,
            t.Name,
            Formats.Hours(t.DefaultHoursPerDay),
            t.Kind switch
            {
                TimeOffKind.Holiday => "Built in: company holidays",
                TimeOffKind.Vacation => "Built in: yearly allowance",
                _ => "Custom",
            },
            t.IsArchived ? "Archived" : "Active");
    }

    private sealed record HolidayRow(CompanyHolidayDto Holiday, string Name, string Rule, string Date, string Observed, DateOnly SortDate)
    {
        public static HolidayRow From(CompanyHolidayDto h, int year, DateOnly date) => new(
            h,
            h.Name,
            HolidayForm.Describe(h.Rule),
            Formats.Date(date),
            h.ObservedDates.ContainsKey(year) ? "Observed" : "",
            date);
    }
}
