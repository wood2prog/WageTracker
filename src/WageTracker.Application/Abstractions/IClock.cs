namespace WageTracker.Application.Abstractions;

/// <summary>The current local time. Replaced in tests.</summary>
public interface IClock
{
    DateTime Now { get; }

    DateOnly Today => DateOnly.FromDateTime(Now);
}

public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;
}
