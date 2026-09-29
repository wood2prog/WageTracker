using WageTracker.Application.Employees;
using WageTracker.Application.Settings;
using WageTracker.WinForms.Common;

namespace WageTracker.WinForms.Employees;

/// <summary>The employee's own hours per day for each time-off type. A blank box uses the type's default.</summary>
internal sealed class TimeOffHoursForm : DialogForm
{
    private readonly EmployeeDto _employee;
    private readonly Func<Guid, decimal?, Task> _set;
    private readonly List<(TimeOffTypeDto Type, TextBox Box)> _rows = [];

    /// <param name="set">Sets the employee's hours for a type, or clears them when the hours are null.</param>
    public TimeOffHoursForm(EmployeeDto employee, IReadOnlyList<TimeOffTypeDto> types, Func<Guid, decimal?, Task> set)
        : base($"Time-off hours for {employee.FullName}")
    {
        _employee = employee;
        _set = set;

        foreach (var type in types)
        {
            var box = new TextBox { Width = 80, TextAlign = HorizontalAlignment.Right };
            if (employee.TimeOffHoursOverrides.TryGetValue(type.Id, out var hours))
                box.Text = Formats.Hours(hours);
            Field(type.Name, Build.Row(box, Build.Text($"hours per day (default {Formats.Hours(type.DefaultHoursPerDay)})")));
            _rows.Add((type, box));
        }
        Note("Leave a box blank to use the default from settings. Hours can be typed as 7.5 or 7:30.");
    }

    protected override async Task SaveAsync()
    {
        // Read every box first so a typo saves nothing.
        var changes = new List<(Guid TypeId, decimal? Hours)>();
        foreach (var (type, box) in _rows)
        {
            decimal? hours = string.IsNullOrWhiteSpace(box.Text) ? null : Formats.RequireHours(box.Text, $"{type.Name} hours");
            decimal? current = _employee.TimeOffHoursOverrides.TryGetValue(type.Id, out var h) ? h : null;
            if (hours != current)
                changes.Add((type.Id, hours));
        }
        foreach (var (typeId, hours) in changes)
            await _set(typeId, hours);
    }
}
