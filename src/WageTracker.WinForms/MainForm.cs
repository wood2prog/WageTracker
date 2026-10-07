using System.Diagnostics;
using WageTracker.Application.Employees;
using WageTracker.Application.Payroll;
using WageTracker.Application.Reports;
using WageTracker.Application.Settings;
using WageTracker.Application.TimeOff;
using WageTracker.Application.TimeTracking;
using WageTracker.Infrastructure;
using WageTracker.WinForms.Common;
using WageTracker.WinForms.Employees;
using WageTracker.WinForms.Payroll;
using WageTracker.WinForms.Settings;
using WageTracker.WinForms.TimeTracking;

namespace WageTracker.WinForms;

/// <summary>A tab that shows data from the services and reloads it when it comes into view.</summary>
internal interface IView
{
    Task ReloadAsync();
}

internal sealed class MainForm : AppForm
{
    private readonly SettingsService _settings;
    private readonly HolidayService _holidays;
    private readonly TabControl _tabs;

    public MainForm(
        EmployeeService employees,
        TimeEntryService timeEntries,
        TimeOffService timeOff,
        SettingsService settings,
        HolidayService holidays,
        PayrollService payroll,
        ReportService reports,
        StorageOptions storage)
    {
        _settings = settings;
        _holidays = holidays;

        Text = Ui.AppName;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1100, 720);
        MinimumSize = new Size(800, 520);

        _tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 4) };
        AddTab("Employees", new EmployeesView(employees, settings));
        AddTab("Time", new TimeView(employees, timeEntries, timeOff, settings, holidays));
        AddTab("Payroll", new PayrollView(payroll, reports));
        _tabs.SelectedIndexChanged += async (_, _) => await ReloadCurrentAsync();

        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add("&Settings...", null, OnSettings);
        file.DropDownItems.Add("Open &reports folder", null, (_, _) => OpenFolder(storage.ReportsFolder));
        file.DropDownItems.Add("Open &backups folder", null, (_, _) => OpenFolder(storage.BackupsFolder));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("E&xit", null, (_, _) => Close());
        var menu = new MenuStrip();
        menu.Items.Add(file);

        Controls.Add(_tabs);
        Controls.Add(menu);
        MainMenuStrip = menu;
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await ReloadCurrentAsync();
    }

    public static void OpenFolder(string folder)
    {
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }

    public static void OpenFile(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    private void AddTab(string title, UserControl view)
    {
        view.Dock = DockStyle.Fill;
        var page = new TabPage(title) { Padding = new Padding(8), UseVisualStyleBackColor = true };
        page.Controls.Add(view);
        _tabs.TabPages.Add(page);
    }

    private Task ReloadCurrentAsync() =>
        _tabs.SelectedTab?.Controls[0] is IView view
            ? Ui.RunAsync(this, view.ReloadAsync)
            : Task.CompletedTask;

    private async void OnSettings(object? sender, EventArgs e)
    {
        using (var form = new SettingsForm(_settings, _holidays, firstTimeSetup: false))
            form.ShowDialog(this);
        await ReloadCurrentAsync();
    }
}
