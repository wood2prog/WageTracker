using WageTracker.Application.Common;
using WageTracker.Domain.Common;

namespace WageTracker.WinForms.Common;

/// <summary>Runs UI actions and shows their errors.</summary>
internal static class Ui
{
    public const string AppName = "WageTracker";

    /// <summary>
    /// Runs <paramref name="action"/> with a wait cursor. Rule violations and missing records show their message;
    /// anything else is reported as unexpected.
    /// </summary>
    /// <returns>True if the action finished without an error.</returns>
    public static async Task<bool> RunAsync(IWin32Window? owner, Func<Task> action)
    {
        var previous = Cursor.Current;
        Cursor.Current = Cursors.WaitCursor;
        try
        {
            await action();
            return true;
        }
        catch (Exception ex)
        {
            Cursor.Current = previous;
            ShowError(owner, ex);
            return false;
        }
        finally
        {
            Cursor.Current = previous;
        }
    }

    /// <summary>Shows a message box. Tests replace it so nothing waits for a click.</summary>
    internal static Func<IWin32Window?, string, MessageBoxButtons, MessageBoxIcon, DialogResult> MessageBoxShow { get; set; } =
        (owner, text, buttons, icon) => MessageBox.Show(owner, text, AppName, buttons, icon);

    public static void ShowError(IWin32Window? owner, Exception ex)
    {
        if (IsExpected(ex))
            MessageBoxShow(owner, ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        else
            MessageBoxShow(owner, $"Something went wrong.\n\n{ex.Message}", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    public static void ShowWarning(IWin32Window? owner, string message) =>
        MessageBoxShow(owner, message, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static void ShowInfo(IWin32Window? owner, string message) =>
        MessageBoxShow(owner, message, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static bool Confirm(IWin32Window? owner, string message) =>
        MessageBoxShow(owner, message, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK;

    public static bool AskYesNo(IWin32Window? owner, string message) =>
        MessageBoxShow(owner, message, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    /// <returns>Yes, No, or Cancel.</returns>
    public static DialogResult AskYesNoCancel(IWin32Window? owner, string message) =>
        MessageBoxShow(owner, message, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

    /// <summary>The errors whose message is written for the user.</summary>
    public static bool IsExpected(Exception ex) =>
        ex is DomainException or NotFoundException or SettingsNotConfiguredException or InputException;
}
