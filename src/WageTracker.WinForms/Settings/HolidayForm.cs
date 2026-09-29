using System.Globalization;
using WageTracker.Application.Settings;
using WageTracker.WinForms.Common;

namespace WageTracker.WinForms.Settings;

/// <summary>Adds a company holiday or changes one's name and rule.</summary>
internal sealed class HolidayForm : DialogForm
{
    private static readonly string[] Ordinals = ["First", "Second", "Third", "Fourth"];

    private readonly Func<string, HolidayRuleDto, Task> _save;
    private readonly TextBox _name = new() { Width = 220 };
    private readonly ComboBox _kind = Build.DropDown(220);
    private readonly ComboBox _occurrence = Build.DropDown(100);
    private readonly ComboBox _dayOfWeek = Build.DropDown(120);
    private readonly ComboBox _month = Build.DropDown(120);
    private readonly NumericUpDown _day = Build.Number(1, 31, width: 60);

    /// <param name="existing">The holiday to edit, or null to add one.</param>
    /// <param name="save">Saves the name and rule through the holiday service.</param>
    public HolidayForm(CompanyHolidayDto? existing, Func<string, HolidayRuleDto, Task> save)
        : base(existing is null ? "Add holiday" : $"Edit {existing.Name}")
    {
        _save = save;

        _kind.SetChoices([
            new("On the same date every year", HolidayRuleKind.FixedDate),
            new("On a weekday of the month", HolidayRuleKind.NthWeekday),
            new Choice<HolidayRuleKind>("On the last weekday of the month", HolidayRuleKind.LastWeekday),
        ]);
        _occurrence.SetChoices(Ordinals.Select((o, i) => new Choice<int>(o, i + 1)));
        _dayOfWeek.SetChoices(Enum.GetValues<DayOfWeek>().Select(d => new Choice<DayOfWeek>(DayName(d), d)));
        _month.SetChoices(Enumerable.Range(1, 12).Select(m => new Choice<int>(MonthName(m), m)));
        _kind.SelectedIndexChanged += (_, _) => ShowKind();

        Field("Name", _name);
        Field("Falls", _kind);
        Field("Which", Build.Row(_occurrence, _dayOfWeek));
        Field("Month", Build.Row(_month, _day));
        if (existing?.ObservedDates.Count > 0)
            Note("Changing the rule clears the observed dates set for this holiday.");

        var rule = existing?.Rule ?? new HolidayRuleDto(HolidayRuleKind.FixedDate, 1, Day: 1);
        _name.Text = existing?.Name ?? "";
        _kind.Select(rule.Kind);
        _month.Select(rule.Month);
        _day.Value = rule.Day ?? 1;
        _occurrence.Select(rule.Occurrence ?? 1);
        _dayOfWeek.Select(rule.DayOfWeek ?? DayOfWeek.Monday);
        ShowKind();
    }

    /// <summary>The rule in words, such as "4th Thursday of November".</summary>
    public static string Describe(HolidayRuleDto rule) => rule.Kind switch
    {
        HolidayRuleKind.FixedDate => $"Every {MonthName(rule.Month)} {rule.Day}",
        HolidayRuleKind.NthWeekday => $"{Ordinals[(rule.Occurrence ?? 1) - 1]} {DayName(rule.DayOfWeek ?? DayOfWeek.Monday)} of {MonthName(rule.Month)}",
        HolidayRuleKind.LastWeekday => $"Last {DayName(rule.DayOfWeek ?? DayOfWeek.Monday)} of {MonthName(rule.Month)}",
        _ => rule.Kind.ToString(),
    };

    protected override Task SaveAsync() => _save(_name.Text, ReadRule());

    /// <summary>The rule with only the parts its kind uses, so it compares equal to an unchanged rule.</summary>
    private HolidayRuleDto ReadRule()
    {
        var month = _month.SelectedValue<int>();
        return _kind.SelectedValue<HolidayRuleKind>() switch
        {
            HolidayRuleKind.FixedDate => new(HolidayRuleKind.FixedDate, month, Day: (int)_day.Value),
            HolidayRuleKind.NthWeekday => new(HolidayRuleKind.NthWeekday, month,
                DayOfWeek: _dayOfWeek.SelectedValue<DayOfWeek>(), Occurrence: _occurrence.SelectedValue<int>()),
            _ => new(HolidayRuleKind.LastWeekday, month, DayOfWeek: _dayOfWeek.SelectedValue<DayOfWeek>()),
        };
    }

    private void ShowKind()
    {
        var kind = _kind.SelectedValue<HolidayRuleKind>();
        _day.Visible = kind == HolidayRuleKind.FixedDate;
        _dayOfWeek.Enabled = kind != HolidayRuleKind.FixedDate;
        _occurrence.Enabled = kind == HolidayRuleKind.NthWeekday;
    }

    private static string MonthName(int month) => CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);

    private static string DayName(DayOfWeek day) => CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(day);
}
