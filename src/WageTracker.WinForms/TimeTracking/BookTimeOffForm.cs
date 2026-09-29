using WageTracker.Application.Settings;
using WageTracker.WinForms.Common;

namespace WageTracker.WinForms.TimeTracking;

/// <summary>Books one whole day off.</summary>
internal sealed class BookTimeOffForm : DialogForm
{
    private readonly Func<DateOnly, Guid, Task> _save;
    private readonly DateTimePicker _date = Build.DatePicker();
    private readonly ComboBox _type = Build.DropDown(180);

    /// <param name="types">The types that can be booked.</param>
    /// <param name="save">Books the date and type through the time-off service.</param>
    public BookTimeOffForm(string employeeName, IReadOnlyList<TimeOffTypeDto> types, DateOnly date, Func<DateOnly, Guid, Task> save)
        : base($"Book a day off for {employeeName}")
    {
        _save = save;
        _date.SetDate(date);
        _type.SetChoices(types.Select(t => new Choice<Guid>($"{t.Name}", t.Id)));
        if (_type.Items.Count > 0)
            _type.SelectedIndex = 0;

        Field("Date", _date);
        Field("Type", _type);
        Note("Days off are whole days, one per date. Company holidays are credited automatically and can't be booked.");
    }

    /// <summary>The date booked, once the dialog closes with OK.</summary>
    public DateOnly BookedDate => _date.ToDateOnly();

    protected override Task SaveAsync() =>
        _type.SelectedItem is Choice<Guid> type
            ? _save(_date.ToDateOnly(), type.Value)
            : throw new InputException("Choose a type of time off.");
}
