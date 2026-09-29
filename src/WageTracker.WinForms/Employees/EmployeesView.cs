using WageTracker.Application.Employees;
using WageTracker.Application.Settings;
using WageTracker.Domain.Employees;
using WageTracker.WinForms.Common;

namespace WageTracker.WinForms.Employees;

internal sealed class EmployeesView : UserControl, IView
{
    private readonly EmployeeService _employees;
    private readonly SettingsService _settings;
    private readonly DataGridView _grid;
    private readonly CheckBox _showFormer;

    public EmployeesView(EmployeeService employees, SettingsService settings)
    {
        _employees = employees;
        _settings = settings;

        _grid = Build.Grid()
            .Column("Name", nameof(EmployeeRow.Name), 160)
            .Column("Status", nameof(EmployeeRow.Status), 80)
            .Column("Pay", nameof(EmployeeRow.Pay), 130, right: true)
            .Column("Overtime", nameof(EmployeeRow.Overtime), 90)
            .Column("Hired", nameof(EmployeeRow.Hired), 100)
            .Column("Last day", nameof(EmployeeRow.Ended), 100)
            .Column($"Vacation {DateTime.Today.Year}", nameof(EmployeeRow.Vacation), 120);
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OnEdit(this, EventArgs.Empty); };

        _showFormer = new CheckBox { Text = "Show former employees", AutoSize = true, Margin = new Padding(12, 7, 3, 3) };
        _showFormer.CheckedChanged += async (_, _) => await Ui.RunAsync(this, ReloadAsync);

        Controls.Add(_grid);
        Controls.Add(Build.Bar(
            Build.Button("Add employee...", OnAdd),
            Build.Button("Edit...", OnEdit),
            Build.Button("Time-off hours...", OnTimeOffHours),
            _showFormer));
    }

    public Task ReloadAsync() => ReloadAsync(_grid.Selected<EmployeeRow>()?.Employee.Id);

    private async Task ReloadAsync(Guid? select)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var rows = new List<EmployeeRow>();
        foreach (var employee in await _employees.ListAsync())
        {
            if (employee.EndDate < today && !_showFormer.Checked && employee.Id != select)
                continue;
            var vacation = await _employees.GetVacationBalanceAsync(employee.Id, today.Year);
            rows.Add(EmployeeRow.From(employee, vacation));
        }
        _grid.Bind(rows, r => r.Employee.Id == select);
    }

    private async void OnAdd(object? sender, EventArgs e)
    {
        Guid? added = null;
        using var form = new EmployeeForm(null, async input => added = (await _employees.CreateAsync(input)).Id);
        if (form.ShowDialog(this) == DialogResult.OK)
            await Ui.RunAsync(this, () => ReloadAsync(added));
    }

    private async void OnEdit(object? sender, EventArgs e)
    {
        if (_grid.Selected<EmployeeRow>() is not { } row)
            return;
        using var form = new EmployeeForm(row.Employee, input => _employees.UpdateAsync(row.Employee.Id, input));
        if (form.ShowDialog(this) == DialogResult.OK)
            await Ui.RunAsync(this, ReloadAsync);
    }

    private async void OnTimeOffHours(object? sender, EventArgs e)
    {
        if (_grid.Selected<EmployeeRow>() is not { } row)
            return;
        IReadOnlyList<TimeOffTypeDto> types = [];
        if (!await Ui.RunAsync(this, async () => types = (await _settings.GetAsync()).TimeOffTypes))
            return;

        using var form = new TimeOffHoursForm(row.Employee, types.Where(t => !t.IsArchived).ToList(),
            (typeId, hours) => _employees.SetTimeOffHoursAsync(row.Employee.Id, typeId, hours));
        if (form.ShowDialog(this) == DialogResult.OK)
            await Ui.RunAsync(this, ReloadAsync);
    }

    private sealed record EmployeeRow(
        EmployeeDto Employee, string Name, string Status, string Pay, string Overtime, string Hired, string Ended, string Vacation)
    {
        public static EmployeeRow From(EmployeeDto e, VacationBalanceDto vacation) => new(
            e,
            $"{e.LastName}, {e.FirstName}",
            e.EmploymentType == EmploymentType.FullTime ? "Full-time" : "Part-time",
            e.CompensationType == CompensationType.Hourly
                ? $"{Formats.Money(e.CompensationAmount)} / hour"
                : $"{Formats.Money(e.CompensationAmount)} / year",
            e.OvertimeEligible ? $"+{e.OvertimePercentage:0.##}%" : "Not eligible",
            Formats.ShortDate(e.HireDate),
            e.EndDate is { } end ? Formats.ShortDate(end) : "",
            e.EmploymentType == EmploymentType.FullTime ? $"{vacation.Used} of {vacation.Permitted} used" : "—");
    }
}
