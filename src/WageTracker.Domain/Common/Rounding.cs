namespace WageTracker.Domain.Common;

/// <summary>The model stores hours and money as decimals rounded to two places.</summary>
public static class Rounding
{
    public const int Places = 2;

    public static decimal Hours(decimal hours) => Math.Round(hours, Places, MidpointRounding.AwayFromZero);

    public static decimal Money(decimal amount) => Math.Round(amount, Places, MidpointRounding.AwayFromZero);

    public static bool HasAtMostTwoPlaces(decimal value) => Math.Round(value, Places) == value;
}
