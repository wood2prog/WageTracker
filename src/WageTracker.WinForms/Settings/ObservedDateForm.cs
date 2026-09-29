using WageTracker.WinForms.Common;

namespace WageTracker.WinForms.Settings;

/// <summary>Moves a holiday to the date it is observed in one year, such as a weekday instead of a weekend.</summary>
internal sealed class ObservedDateForm : DialogForm
{
    private readonly Func<DateOnly, Task> _save;
    private readonly DateTimePicker _date = Build.DatePicker();

    /// <param name="current">The date the holiday is credited that year now.</param>
    /// <param name="save">Saves the observed date through the holiday service.</param>
    public ObservedDateForm(string holidayName, int year, DateOnly current, Func<DateOnly, Task> save)
        : base($"{holidayName} {year}")
    {
        _save = save;
        _date.SetDate(current);

        Field("Observed on", _date);
        Note($"It must be within 7 days of the holiday's usual date in {year}, and it can fall in the neighboring year.");
    }

    protected override Task SaveAsync() => _save(_date.ToDateOnly());
}
