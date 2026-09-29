using WageTracker.Application.Employees;
using WageTracker.Domain.Employees;
using WageTracker.WinForms.Common;

namespace WageTracker.WinForms.Employees;

/// <summary>Adds or edits an employee.</summary>
internal sealed class EmployeeForm : DialogForm
{
    private readonly Func<EmployeeInput, Task> _save;
    private readonly TextBox _firstName = new() { Width = 220 };
    private readonly TextBox _lastName = new() { Width = 220 };
    private readonly DateTimePicker _birthDate = Build.DatePicker();
    private readonly DateTimePicker _hireDate = Build.DatePicker();
    private readonly DateTimePicker _endDate = Build.DatePicker(180);
    private readonly ComboBox _employmentType = Build.DropDown(140);
    private readonly ComboBox _compensationType = Build.DropDown(140);
    private readonly NumericUpDown _amount = Build.Number(0, 100_000_000, places: 2, width: 140);
    private readonly Label _amountUnit = Build.Text();
    private readonly CheckBox _overtimeEligible = new() { Text = "Eligible for overtime", AutoSize = true };
    private readonly NumericUpDown _overtimePercentage = Build.Number(0, 1000, places: 2);
    private readonly NumericUpDown _vacationDays = Build.Number(0, 366);

    /// <param name="existing">The employee to edit, or null to add one.</param>
    /// <param name="save">Saves the input through the employee service.</param>
    public EmployeeForm(EmployeeDto? existing, Func<EmployeeInput, Task> save)
        : base(existing is null ? "Add employee" : $"Edit {existing.FullName}")
    {
        _save = save;

        _employmentType.SetChoices([new("Full-time", EmploymentType.FullTime), new Choice<EmploymentType>("Part-time", EmploymentType.PartTime)]);
        _compensationType.SetChoices([new("Hourly", CompensationType.Hourly), new Choice<CompensationType>("Salary", CompensationType.Salary)]);
        _compensationType.SelectedIndexChanged += (_, _) => ShowCompensationType();
        _endDate.ShowCheckBox = true;

        Field("First name", _firstName);
        Field("Last name", _lastName);
        Field("Birth date", _birthDate);
        Field("Hire date", _hireDate);
        Field("Last day", _endDate);
        Note("Tick the box to set the last day worked. Leave it clear while the employee is still employed.");
        Field("Employment", _employmentType);
        Note("Only full-time employees get holidays, vacation, and other paid time off.");
        Field("Pay type", _compensationType);
        Field("Pay", Build.Row(_amount, _amountUnit));
        Field("", _overtimeEligible);
        Field("Overtime premium (%)", _overtimePercentage);
        Note("The premium on top of the regular rate. 50 means overtime pays 1.5 times the rate.");
        Field("Vacation days per year", _vacationDays);

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (existing is null)
        {
            _birthDate.SetDate(today.AddYears(-30));
            _hireDate.SetDate(today);
            _endDate.SetDate(today);
            _endDate.Checked = false;
            _employmentType.Select(EmploymentType.FullTime);
            _compensationType.Select(CompensationType.Hourly);
            _overtimePercentage.Value = 50;
            _vacationDays.Value = 10;
        }
        else
        {
            _firstName.Text = existing.FirstName;
            _lastName.Text = existing.LastName;
            _birthDate.SetDate(existing.BirthDate);
            _hireDate.SetDate(existing.HireDate);
            _endDate.SetDate(existing.EndDate ?? today);
            _endDate.Checked = existing.EndDate is not null;
            _employmentType.Select(existing.EmploymentType);
            _compensationType.Select(existing.CompensationType);
            _amount.Value = existing.CompensationAmount;
            _overtimeEligible.Checked = existing.OvertimeEligible;
            _overtimePercentage.Value = existing.OvertimePercentage;
            _vacationDays.Value = existing.VacationDaysPermitted;
        }
        ShowCompensationType();
    }

    protected override Task SaveAsync() => _save(new EmployeeInput(
        _firstName.Text,
        _lastName.Text,
        _birthDate.ToDateOnly(),
        _hireDate.ToDateOnly(),
        _endDate.Checked ? _endDate.ToDateOnly() : null,
        _employmentType.SelectedValue<EmploymentType>(),
        _compensationType.SelectedValue<CompensationType>(),
        _amount.Value,
        _overtimeEligible.Checked,
        _overtimePercentage.Value,
        (int)_vacationDays.Value));

    /// <summary>Hourly employees always earn overtime; salaried employees only when marked eligible.</summary>
    private void ShowCompensationType()
    {
        var hourly = _compensationType.SelectedValue<CompensationType>() == CompensationType.Hourly;
        _amountUnit.Text = hourly ? "per hour" : "per year";
        _overtimeEligible.Enabled = !hourly;
        if (hourly)
            _overtimeEligible.Checked = true;
    }
}
