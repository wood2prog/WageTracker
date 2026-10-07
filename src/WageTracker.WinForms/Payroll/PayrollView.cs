using WageTracker.Application.Payroll;
using WageTracker.Application.Reports;
using WageTracker.Domain.Employees;
using WageTracker.WinForms.Common;

namespace WageTracker.WinForms.Payroll;

/// <summary>
/// One pay period's payroll: a live preview while the period is open, or the stored statements once it is finalized.
/// Finalizing locks the period and writes the report for the payroll accountant; other reports are picked from a list.
/// </summary>
internal sealed class PayrollView : UserControl, IView
{
    private readonly PayrollService _payroll;
    private readonly ReportService _reports;

    private readonly DateTimePicker _periodOf = Build.DatePicker();
    private readonly Label _period = Build.Text(bold: true);
    private readonly Label _status = Build.Text();
    private readonly Label _total = Build.Text(bold: true);
    private readonly Button _finalize;
    private readonly ComboBox _report = Build.DropDown(220);
    private readonly Button _createReport;
    private readonly DataGridView _statements;
    private readonly DataGridView _weeks;
    private readonly GroupBox _weeksGroup;

    private DateOnly _date = DateOnly.FromDateTime(DateTime.Today);
    private PayrollRunDto? _run;
    private bool _loading;

    public PayrollView(PayrollService payroll, ReportService reports)
    {
        _payroll = payroll;
        _reports = reports;

        _periodOf.SetDate(_date);
        _periodOf.ValueChanged += async (_, _) => { if (!_loading) await ShowPeriodAsync(_periodOf.ToDateOnly()); };

        _finalize = Build.Button("Finalize and create report...", OnFinalize);
        _report.SetChoices(reports.List().Select(r => new Choice<ReportDto>(r.Name, r)));
        if (_report.Items.Count > 0)
            _report.SelectedIndex = 0;
        _report.SelectedIndexChanged += (_, _) => EnableCreateReport();
        _createReport = Build.Button("Create report", OnCreateReport);

        _statements = Build.Grid()
            .Column("Employee", nameof(StatementRow.Employee), 150)
            .Column("Rate", nameof(StatementRow.Rate), 80, right: true)
            .Column("Worked", nameof(StatementRow.Worked), 65, right: true)
            .Column("Time off", nameof(StatementRow.TimeOff), 65, right: true)
            .Column("Overtime h", nameof(StatementRow.OvertimeHours), 75, right: true)
            .Column("Base pay", nameof(StatementRow.BasePay), 90, right: true)
            .Column("Overtime pay", nameof(StatementRow.OvertimePay), 90, right: true)
            .Column("Deferred OT", nameof(StatementRow.Deferred), 85, right: true)
            .Column("Vacation payout", nameof(StatementRow.VacationPayout), 95, right: true)
            .Column("Gross pay", nameof(StatementRow.Gross), 95, right: true);
        _statements.SelectionChanged += (_, _) => ShowWeeks();

        _weeks = Build.Grid()
            .Column("Week", nameof(WeekRow.Week), 180)
            .Column("Worked", nameof(WeekRow.Worked), 80, right: true)
            .Column("Time off", nameof(WeekRow.TimeOff), 80, right: true)
            .Column("Regular", nameof(WeekRow.Regular), 80, right: true)
            .Column("Overtime", nameof(WeekRow.Overtime), 80, right: true);
        _weeksGroup = Build.Group("Weeks", _weeks);

        var totals = Build.Bar(_total);
        totals.Dock = DockStyle.Bottom;
        var split = Build.Split(Build.Group("Pay statements", totals, _statements), _weeksGroup, topShare: 0.6);

        var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        header.Controls.Add(Build.Bar(
            Build.Text("Pay period containing"), _periodOf,
            Build.Button("◀ Previous period", async (_, _) => await ShowPeriodAsync((_run?.Start ?? _date).AddDays(-1))),
            Build.Button("Current period", async (_, _) => await ShowPeriodAsync(DateOnly.FromDateTime(DateTime.Today))),
            Build.Button("Next period ▶", async (_, _) => await ShowPeriodAsync((_run?.End ?? _date).AddDays(1)))));
        header.Controls.Add(_period);
        header.Controls.Add(_status);
        header.Controls.Add(Build.Bar(_finalize, Build.Text("Report"), _report, _createReport));

        Controls.Add(split);
        Controls.Add(header);
    }

    public async Task ReloadAsync()
    {
        var selectedId = _statements.Selected<StatementRow>()?.Statement.EmployeeId;
        _run = await _payroll.GetRunAsync(_date);

        _period.Text = $"{Formats.Range(_run.Start, _run.End)}   ·   {_run.WeekCount} week{(_run.WeekCount == 1 ? "" : "s")}" +
            $"   ·   Paid {Formats.Date(_run.PayoutDate)}";
        _status.Text = _run.IsLocked
            ? $"Finalized {_run.LockedAt:MMM d, yyyy h:mm tt}. These are the stored statements; nothing dated in this period can change."
            : "Not finalized. This is a preview from the current time entries and days off.";
        _status.ForeColor = _run.IsLocked ? SystemColors.ControlText : Color.DarkGoldenrod;
        _finalize.Enabled = !_run.IsLocked;
        EnableCreateReport();
        _total.Text = $"Total gross pay: {Formats.Money(_run.TotalGrossPay)}   ·   {_run.Statements.Count} employee{(_run.Statements.Count == 1 ? "" : "s")}";

        _statements.Bind(_run.Statements.Select(StatementRow.From).ToList(), r => r.Statement.EmployeeId == selectedId);
        ShowWeeks();
    }

    private async Task ShowPeriodAsync(DateOnly date)
    {
        _date = date;
        _loading = true;
        _periodOf.SetDate(date);
        _loading = false;
        await Ui.RunAsync(this, ReloadAsync);
    }

    private void ShowWeeks()
    {
        var statement = _statements.Selected<StatementRow>()?.Statement;
        _weeksGroup.Text = statement is null ? "Weeks" : $"Weeks for {statement.EmployeeName}";
        _weeks.Bind(statement?.Weeks.Select(WeekRow.From).ToList() ?? []);
    }

    private async void OnFinalize(object? sender, EventArgs e)
    {
        if (_run is not { } run)
            return;
        if (!Ui.Confirm(this,
                $"Finalize the pay period {Formats.Range(run.Start, run.End)}?\n\n" +
                "This stores each employee's pay statement and locks the period, so nothing dated in it can be changed afterward. " +
                "Then it creates the PDF report for the payroll accountant."))
            return;

        FinalizeResult? result = null;
        if (!await Ui.RunAsync(this, async () => result = await _payroll.FinalizeAsync(run.Start)))
            return;
        await Ui.RunAsync(this, ReloadAsync);
        OfferToOpen(result!.ReportLocation);
    }

    /// <summary>Reports that show stored statements can only be created once the period is finalized.</summary>
    private void EnableCreateReport() =>
        _createReport.Enabled = _run is { } run && _report.SelectedValue<ReportDto>() is { } report
            && (run.IsLocked || !report.RequiresFinalizedPeriod);

    private async void OnCreateReport(object? sender, EventArgs e)
    {
        if (_run is not { } run || _report.SelectedValue<ReportDto>() is not { } report)
            return;
        string? location = null;
        if (await Ui.RunAsync(this, async () => location = await _reports.CreateAsync(report.Name, run.Start)))
            OfferToOpen(location!);
    }

    private void OfferToOpen(string reportPath)
    {
        if (Ui.AskYesNo(this, $"The report was saved to:\n{reportPath}\n\nOpen it now?"))
        {
            try
            {
                MainForm.OpenFile(reportPath);
            }
            catch (Exception ex)
            {
                Ui.ShowError(this, ex);
            }
        }
    }

    private sealed record StatementRow(
        PayStatementDto Statement, string Employee, string Rate, string Worked, string TimeOff, string OvertimeHours,
        string BasePay, string OvertimePay, string Deferred, string VacationPayout, string Gross)
    {
        public static StatementRow From(PayStatementDto s) => new(
            s,
            s.EmployeeName,
            s.CompensationType == CompensationType.Hourly ? $"{Formats.Money(s.HourlyRate)}/h" : "Salary",
            Formats.Hours(s.WorkedHours),
            Formats.Hours(s.TimeOffHours),
            Formats.Hours(s.OvertimeHours),
            Formats.Money(s.BasePay),
            Formats.Money(s.OvertimePay),
            s.DeferredOvertimePay == 0 ? "" : Formats.Money(s.DeferredOvertimePay),
            s.VacationPayout == 0 ? "" : $"{Formats.Money(s.VacationPayout)} ({s.VacationDaysPaidOut} d)",
            Formats.Money(s.GrossPay));
    }

    private sealed record WeekRow(string Week, string Worked, string TimeOff, string Regular, string Overtime)
    {
        public static WeekRow From(WeekDto w) => new(
            Formats.Range(w.Start, w.End),
            Formats.Hours(w.WorkedHours),
            Formats.Hours(w.TimeOffHours),
            Formats.Hours(w.RegularHours),
            Formats.Hours(w.OvertimeHours));
    }
}
