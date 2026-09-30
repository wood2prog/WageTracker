namespace WageTracker.WinForms.Common;

/// <summary>A form laid out in code at 96 DPI and scaled to the screen.</summary>
internal class AppForm : Form
{
    /// <summary>The app icon, embedded in the assembly so every form's title bar and taskbar button show it.</summary>
    private static readonly Icon AppIcon = new(typeof(AppForm).Assembly.GetManifestResourceStream("WageTracker.ico")!);

    public AppForm()
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        Icon = AppIcon;
    }
}

/// <summary>
/// A dialog of labeled fields with OK and Cancel. OK calls <see cref="SaveAsync"/>; if that throws, the message is shown
/// and the dialog stays open so the user can fix the input.
/// </summary>
internal abstract class DialogForm : AppForm
{
    private readonly TableLayoutPanel _fields;
    private readonly Button _ok;

    protected DialogForm(string title)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        _fields = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Top };
        _fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _ok = new Button { Text = "OK", AutoSize = true, MinimumSize = new Size(88, 0) };
        _ok.Click += OnOk;
        var cancel = new Button { Text = "Cancel", AutoSize = true, MinimumSize = new Size(88, 0), DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 12, 0, 0),
        };
        buttons.Controls.AddRange([cancel, _ok]);

        var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Location = new Point(12, 12) };
        root.Controls.Add(_fields);
        root.Controls.Add(buttons);
        Controls.Add(root);

        AcceptButton = _ok;
        CancelButton = cancel;
    }

    /// <summary>Adds a labeled field on its own row.</summary>
    protected T Field<T>(string label, T control) where T : Control
    {
        control.Anchor = AnchorStyles.Left;
        _fields.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 12, 3) });
        _fields.Controls.Add(control);
        return control;
    }

    /// <summary>Adds explanatory text under the fields added so far.</summary>
    protected Label Note(string text)
    {
        var note = new Label
        {
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(360, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 0, 3, 6),
        };
        _fields.Controls.Add(new Label { AutoSize = true });
        _fields.Controls.Add(note);
        return note;
    }

    /// <summary>Validates the input and saves it through a service. Throw to keep the dialog open.</summary>
    protected abstract Task SaveAsync();

    private async void OnOk(object? sender, EventArgs e)
    {
        _ok.Enabled = false;
        try
        {
            if (await Ui.RunAsync(this, SaveAsync))
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        finally
        {
            _ok.Enabled = true;
        }
    }
}

/// <summary>A problem with what the user typed, caught before it reaches a service.</summary>
internal sealed class InputException(string message) : Exception(message);
