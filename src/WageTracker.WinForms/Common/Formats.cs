using System.Globalization;
using WageTracker.Domain.Common;

namespace WageTracker.WinForms.Common;

/// <summary>How dates, hours, and money are shown and read.</summary>
internal static class Formats
{
    public const string DatePickerFormat = "ddd, MMM d, yyyy";
    public const string DateTimePickerFormat = "ddd, MMM d, yyyy   h:mm tt";

    public static string Date(DateOnly date) => date.ToString("ddd, MMM d, yyyy", CultureInfo.CurrentCulture);

    public static string ShortDate(DateOnly date) => date.ToString("MMM d, yyyy", CultureInfo.CurrentCulture);

    public static string Time(DateTime time) => time.ToString("h:mm tt", CultureInfo.CurrentCulture);

    public static string Range(DateOnly start, DateOnly end) => $"{ShortDate(start)} – {ShortDate(end)}";

    public static string Hours(decimal hours) => hours.ToString("0.00", CultureInfo.CurrentCulture);

    public static string Money(decimal amount) => amount.ToString("C2", CultureInfo.CurrentCulture);

    /// <summary>
    /// Reads hours typed as a decimal ("7.5") or as hours and minutes ("7:30"). Hours and minutes are converted to
    /// decimal hours rounded to two places, which is what the model stores.
    /// </summary>
    public static bool TryParseHours(string? text, out decimal hours)
    {
        hours = 0;
        var trimmed = text?.Trim() ?? "";
        if (trimmed.Length == 0)
            return false;

        var colon = trimmed.IndexOf(':');
        if (colon < 0)
            return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.CurrentCulture, out hours) && hours >= 0;

        if (!int.TryParse(trimmed[..colon], NumberStyles.None, CultureInfo.InvariantCulture, out var whole)
            || !int.TryParse(trimmed[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || trimmed.Length - colon - 1 != 2
            || minutes > 59)
            return false;

        hours = Rounding.Hours(whole + minutes / 60m);
        return true;
    }

    /// <summary>Reads hours the way <see cref="TryParseHours"/> does, or explains what is wrong.</summary>
    public static decimal RequireHours(string? text, string field) =>
        TryParseHours(text, out var hours)
            ? hours
            : throw new InputException($"Enter the {field} as hours, such as 7.5 or 7:30.");

    public static DateOnly ToDateOnly(this DateTimePicker picker) => DateOnly.FromDateTime(picker.Value);

    public static void SetDate(this DateTimePicker picker, DateOnly date) =>
        picker.Value = date.ToDateTime(TimeOnly.MinValue);

    /// <summary>The picker's value to the minute.</summary>
    public static DateTime ToMinute(this DateTimePicker picker)
    {
        var v = picker.Value;
        return new DateTime(v.Year, v.Month, v.Day, v.Hour, v.Minute, 0);
    }
}
