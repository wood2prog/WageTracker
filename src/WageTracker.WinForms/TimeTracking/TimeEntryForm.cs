using WageTracker.WinForms.Common;

namespace WageTracker.WinForms.TimeTracking;

/// <summary>Records or changes when a stretch of work started and ended.</summary>
internal sealed class TimeEntryForm : DialogForm
{
    private readonly Func<DateTime, DateTime, Task> _save;
    private readonly DateTimePicker _start = DateAndTimePicker();
    private readonly DateTimePicker _end = DateAndTimePicker();
    private readonly Label _length = Build.Text();

    /// <param name="save">Saves the start and end through the time entry service.</param>
    public TimeEntryForm(string title, DateTime start, DateTime end, Func<DateTime, DateTime, Task> save)
        : base(title)
    {
        _save = save;
        _start.Value = start;
        _end.Value = end;
        _start.ValueChanged += (_, _) => ShowLength();
        _end.ValueChanged += (_, _) => ShowLength();

        Field("Start", _start);
        Field("End", _end);
        Field("Length", _length);
        Note("Type the time or use the arrow keys on each part. An entry can be at most 24 hours long, can't end in the future, and can't overlap another entry.");
        ShowLength();
    }

    protected override Task SaveAsync() => _save(_start.ToMinute(), _end.ToMinute());

    private void ShowLength()
    {
        var length = _end.ToMinute() - _start.ToMinute();
        _length.Text = length > TimeSpan.Zero
            ? $"{(int)length.TotalHours}:{length.Minutes:00}  ({Formats.Hours(Domain.Common.Rounding.Hours((decimal)length.TotalHours))} hours)"
            : "The end must be after the start.";
    }

    private static DateTimePicker DateAndTimePicker() => new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = Formats.DateTimePickerFormat,
        Width = 240,
    };
}
