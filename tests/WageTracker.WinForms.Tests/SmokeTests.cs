using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using WageTracker.Application;
using WageTracker.Application.Employees;
using WageTracker.Application.Settings;
using WageTracker.Application.TimeTracking;
using WageTracker.Domain.Calendar;
using WageTracker.Domain.Employees;
using WageTracker.Domain.Payroll;
using WageTracker.Infrastructure;
using WageTracker.WinForms.Common;
using WageTracker.WinForms.Settings;
using WinFormsApp = System.Windows.Forms.Application;

namespace WageTracker.WinForms.Tests;

/// <summary>
/// Opens the real forms against a temp database and clicks through them, pumping messages so the async loads run.
/// Message boxes are recorded instead of shown; any recorded message is a failure unless the test expects it.
/// </summary>
public sealed class SmokeTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "WageTrackerUiTests", Guid.NewGuid().ToString("N"));
    private readonly List<string> _messages = [];

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void First_time_setup_saves_the_settings_and_opens_the_other_tabs()
    {
        using var services = BuildServices();
        var settings = services.GetRequiredService<SettingsService>();

        RunSta(() =>
        {
            using var form = new SettingsForm(settings, services.GetRequiredService<HolidayService>(), firstTimeSetup: true);
            form.Show();
            PumpUntil(() => form.Visible);

            Find<Button>(form, b => b.Text == "Save and continue").PerformClick();
            PumpUntil(() => _messages.Count > 0);

            Assert.StartsWith("Information: Payroll is set up.", Assert.Single(_messages));
            Assert.Equal(["General", "Time-off types", "Holidays"], Find<TabControl>(form).TabPages.Cast<TabPage>().Select(p => p.Text));
            form.Close();
        });

        var saved = Wait(settings.GetAsync);
        Assert.Equal(PayFrequency.Weekly, saved.Frequency);
        Assert.Equal(40m, saved.OvertimeThresholdHours);
    }

    [Fact]
    public void The_main_window_shows_employees_time_and_payroll()
    {
        using var services = BuildServices();
        var lastMonday = WorkWeek.Containing(DateOnly.FromDateTime(DateTime.Today)).Previous().Start.AddDays(1).ToDateTime(new TimeOnly(8, 0));
        Wait(async () =>
        {
            await services.GetRequiredService<SettingsService>().InitializeAsync(new ScheduleInput(PayFrequency.Weekly), 10);
            var ada = await services.GetRequiredService<EmployeeService>().CreateAsync(new EmployeeInput(
                "Ada", "Lovelace", new DateOnly(1990, 12, 10), new DateOnly(2020, 1, 6), null,
                EmploymentType.FullTime, CompensationType.Hourly, 20m, false, 50m, 10));
            return await services.GetRequiredService<TimeEntryService>().RecordAsync(ada.Id, lastMonday, lastMonday.AddHours(8));
        });

        RunSta(() =>
        {
            using var form = services.GetRequiredService<MainForm>();
            form.Show();
            var tabs = Find<TabControl>(form);

            PumpUntil(() => Grids(tabs.TabPages[0])[0].Rows.Count == 1);

            tabs.SelectedIndex = 1;
            PumpUntil(() => Find<ComboBox>(tabs.TabPages[1]).SelectedItem is not null);
            Find<Button>(tabs.TabPages[1], b => b.Text.Contains("Previous week")).PerformClick();
            PumpUntil(() => Grids(tabs.TabPages[1])[0].Rows.Count == 1);

            tabs.SelectedIndex = 2;
            PumpUntil(() => Grids(tabs.TabPages[2])[0].Rows.Count == 1);
            Find<Button>(tabs.TabPages[2], b => b.Text.Contains("Previous period")).PerformClick();
            PumpUntil(() => (string?)Grids(tabs.TabPages[2])[0].Rows[0].Cells[2].Value == "8.00");

            form.Close();
        });
    }

    [Fact]
    public void Weekly_hours_are_entered_as_one_total_on_the_time_tab()
    {
        using var services = BuildServices();
        var lastWeek = WorkWeek.Containing(DateOnly.FromDateTime(DateTime.Today)).Previous();
        var ada = Wait(async () =>
        {
            await services.GetRequiredService<SettingsService>().InitializeAsync(new ScheduleInput(PayFrequency.Weekly), 10);
            return await services.GetRequiredService<EmployeeService>().CreateAsync(new EmployeeInput(
                "Ada", "Lovelace", new DateOnly(1990, 12, 10), new DateOnly(2020, 1, 6), null,
                EmploymentType.FullTime, CompensationType.Hourly, 20m, false, 50m, 10, TimeRecording.Weekly));
        });

        RunSta(() =>
        {
            using var form = services.GetRequiredService<MainForm>();
            form.Show();
            var tabs = Find<TabControl>(form);
            tabs.SelectedIndex = 1;
            var page = tabs.TabPages[1];
            PumpUntil(() => Find<ComboBox>(page).SelectedItem is not null);
            Find<Button>(page, b => b.Text.Contains("Previous week")).PerformClick();
            PumpUntil(() => Find<Label>(page, l => l.Text.StartsWith("Not entered yet")).Visible);

            Assert.False(Grids(page)[0].Visible);
            Find<TextBox>(page).Text = "41:30";
            Find<Button>(page, b => b.Text == "Save hours").PerformClick();
            PumpUntil(() => Descendants(page).OfType<Label>().Any(l => l.Text.Contains("Worked 41.50 h")));

            form.Close();
        });

        var saved = Wait(() => services.GetRequiredService<TimeEntryService>().GetWeeklyHoursAsync(ada.Id, lastWeek.Start));
        Assert.Equal(41.5m, saved?.Hours);
    }

    private ServiceProvider BuildServices() => new ServiceCollection()
        .AddWageTrackerApplication()
        .AddWageTrackerInfrastructure(o =>
        {
            o.DatabasePath = Path.Combine(_folder, "test.db");
            o.ReportsFolder = Path.Combine(_folder, "Reports");
            o.BackupsFolder = Path.Combine(_folder, "Backups");
        })
        .AddTransient<MainForm>()
        .BuildServiceProvider();

    /// <summary>Runs <paramref name="test"/> on a UI thread, recording message boxes and unhandled errors.</summary>
    private void RunSta(Action test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var previous = Ui.MessageBoxShow;
            Ui.MessageBoxShow = (_, text, _, icon) =>
            {
                // Information and Asterisk are the same value, so compare rather than print the name.
                _messages.Add($"{(icon == MessageBoxIcon.Information ? "Information" : "Problem")}: {text}");
                return DialogResult.OK;
            };
            WinFormsApp.ThreadException += (_, e) => _messages.Add($"Unhandled: {e.Exception}");
            try
            {
                test();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                Ui.MessageBoxShow = previous;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
            ExceptionDispatchInfo.Throw(failure);
    }

    private void PumpUntil(Func<bool> done)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            WinFormsApp.DoEvents();
            if (_messages.Any(m => !m.StartsWith("Information:")))
                Assert.Fail(string.Join("\n", _messages));
            if (done())
                return;
            if (clock.Elapsed > TimeSpan.FromSeconds(15))
                Assert.Fail("Timed out waiting for the UI." + string.Join("\n", _messages));
            Thread.Sleep(10);
        }
    }

    private static T Wait<T>(Func<Task<T>> work) => Task.Run(work).GetAwaiter().GetResult();

    private static IEnumerable<Control> Descendants(Control root) =>
        root.Controls.Cast<Control>().SelectMany(c => Descendants(c).Prepend(c));

    private static T Find<T>(Control root, Func<T, bool>? match = null) where T : Control =>
        Descendants(root).OfType<T>().First(c => match?.Invoke(c) ?? true);

    private static List<DataGridView> Grids(Control root) => Descendants(root).OfType<DataGridView>().ToList();
}
