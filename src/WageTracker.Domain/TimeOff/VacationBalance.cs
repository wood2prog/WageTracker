namespace WageTracker.Domain.TimeOff;

/// <summary>An employee's vacation days for one calendar year.</summary>
public sealed record VacationBalance(int Year, int Permitted, int Used)
{
    /// <summary>Days still available. Unused days are paid out in the last pay period of the year.</summary>
    public int Remaining => Math.Max(0, Permitted - Used);
}
