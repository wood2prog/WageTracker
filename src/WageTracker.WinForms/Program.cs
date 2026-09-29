using Microsoft.Extensions.DependencyInjection;
using WageTracker.Application;
using WageTracker.Application.Settings;
using WageTracker.Infrastructure;
using WageTracker.WinForms.Common;
using WageTracker.WinForms.Settings;
using WinFormsApp = System.Windows.Forms.Application;

namespace WageTracker.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        WinFormsApp.ThreadException += (_, e) => Ui.ShowError(null, e.Exception);

        using var services = new ServiceCollection()
            .AddWageTrackerApplication()
            .AddWageTrackerInfrastructure()
            .AddTransient<MainForm>()
            .BuildServiceProvider();

        try
        {
            if (EnsureSettings(services))
                WinFormsApp.Run(services.GetRequiredService<MainForm>());
        }
        catch (Exception ex)
        {
            Ui.ShowError(null, ex);
        }
        finally
        {
            Backup(services);
        }
    }

    /// <summary>Opens the settings page for first-time setup when there are no settings yet.</summary>
    /// <returns>False if the user closed setup without saving, so the app should exit.</returns>
    private static bool EnsureSettings(IServiceProvider services)
    {
        var settings = services.GetRequiredService<SettingsService>();
        if (Wait(settings.IsConfiguredAsync))
            return true;

        using (var setup = new SettingsForm(settings, services.GetRequiredService<HolidayService>(), firstTimeSetup: true))
            setup.ShowDialog();

        return Wait(settings.IsConfiguredAsync);
    }

    /// <summary>Backs up the database as the app exits.</summary>
    private static void Backup(IServiceProvider services)
    {
        try
        {
            Wait(services.GetRequiredService<BackupService>().BackupAsync);
        }
        catch (Exception ex)
        {
            Ui.ShowWarning(null, $"The database could not be backed up.\n\n{ex.Message}");
        }
    }

    /// <summary>
    /// Waits for async work outside a message loop. It runs on the thread pool, because once a form has been created the
    /// UI thread has a synchronization context that blocking on it would deadlock.
    /// </summary>
    private static T Wait<T>(Func<Task<T>> work) => Task.Run(work).GetAwaiter().GetResult();
}
