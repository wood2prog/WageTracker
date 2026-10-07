using WageTracker.Application.Common;

namespace WageTracker.Application.Reports;

public sealed record ReportDto(string Name, bool RequiresFinalizedPeriod);

/// <summary>Lists the pay period reports and creates the one the user picks.</summary>
public sealed class ReportService(IEnumerable<IPeriodReport> reports)
{
    private readonly IReadOnlyList<IPeriodReport> _reports = reports.ToList();

    public IReadOnlyList<ReportDto> List() =>
        _reports.Select(r => new ReportDto(r.Name, r.RequiresFinalizedPeriod)).ToList();

    /// <summary>Creates the report named <paramref name="name"/> for the pay period containing <paramref name="date"/>.</summary>
    /// <returns>Where the report was written.</returns>
    public Task<string> CreateAsync(string name, DateOnly date)
    {
        var report = _reports.FirstOrDefault(r => r.Name == name)
            ?? throw new NotFoundException($"There is no report named \"{name}\".");
        return report.CreateAsync(date);
    }
}
