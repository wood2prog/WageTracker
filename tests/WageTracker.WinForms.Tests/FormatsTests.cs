using System.Globalization;
using WageTracker.WinForms.Common;

namespace WageTracker.WinForms.Tests;

public class FormatsTests
{
    [Theory]
    [InlineData("8", 8)]
    [InlineData(" 7.5 ", 7.5)]
    [InlineData("7:30", 7.5)]
    [InlineData("0:45", 0.75)]
    [InlineData("8:20", 8.33)]
    [InlineData("10:00", 10)]
    public void Hours_are_read_as_decimals_or_hours_and_minutes(string text, decimal expected)
    {
        using var culture = new CultureScope("en-US");

        Assert.True(Formats.TryParseHours(text, out var hours));
        Assert.Equal(expected, hours);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("7:60")]
    [InlineData("7:5")]
    [InlineData("7:")]
    [InlineData(":30")]
    [InlineData("7:30:00")]
    public void Bad_hours_are_refused(string text)
    {
        using var culture = new CultureScope("en-US");

        Assert.False(Formats.TryParseHours(text, out _));
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

        public CultureScope(string name) => CultureInfo.CurrentCulture = new CultureInfo(name);

        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }
}
