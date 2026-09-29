using WageTracker.Application.Settings;
using WageTracker.WinForms.Common;

namespace WageTracker.WinForms.Settings;

/// <summary>Adds a time-off type or changes one's name and default hours.</summary>
internal sealed class TimeOffTypeForm : DialogForm
{
    private readonly Func<string, decimal, Task> _save;
    private readonly TextBox _name = new() { Width = 220 };
    private readonly TextBox _hours = new() { Width = 70, TextAlign = HorizontalAlignment.Right };

    /// <param name="existing">The type to edit, or null to add one.</param>
    /// <param name="save">Saves the name and default hours per day through the settings service.</param>
    public TimeOffTypeForm(TimeOffTypeDto? existing, Func<string, decimal, Task> save)
        : base(existing is null ? "Add time-off type" : $"Edit {existing.Name}")
    {
        _save = save;
        _name.Text = existing?.Name ?? "";
        _hours.Text = Formats.Hours(existing?.DefaultHoursPerDay ?? 8m);

        Field("Name", _name);
        Field("Hours per day", _hours);
        Note("The default for every employee. Type 7.5 or 7:30 for seven and a half hours.");
    }

    protected override Task SaveAsync() => _save(_name.Text, Formats.RequireHours(_hours.Text, "hours per day"));
}
