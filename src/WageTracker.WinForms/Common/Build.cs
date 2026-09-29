namespace WageTracker.WinForms.Common;

/// <summary>Builds the controls the forms share, so they look and behave alike.</summary>
internal static class Build
{
    public static DataGridView Grid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.FixedSingle,
            StandardTab = true,
        };
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        return grid;
    }

    /// <param name="property">The row object's property the column shows.</param>
    /// <param name="weight">The column's share of the grid's width.</param>
    public static DataGridView Column(this DataGridView grid, string header, string property, float weight = 100, bool right = false)
    {
        var column = new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            DataPropertyName = property,
            FillWeight = weight,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        if (right)
        {
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        grid.Columns.Add(column);
        return grid;
    }

    /// <summary>Shows <paramref name="rows"/>, keeping the selection on the first row that matches <paramref name="select"/>.</summary>
    public static void Bind<T>(this DataGridView grid, IReadOnlyList<T> rows, Func<T, bool>? select = null)
    {
        grid.DataSource = rows.ToList();
        if (select is null)
            return;
        for (var i = 0; i < rows.Count; i++)
        {
            if (select(rows[i]))
            {
                grid.CurrentCell = grid.Rows[i].Cells[0];
                return;
            }
        }
    }

    public static T? Selected<T>(this DataGridView grid) where T : class => grid.CurrentRow?.DataBoundItem as T;

    public static Button Button(string text, EventHandler onClick)
    {
        var button = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(88, 0) };
        button.Click += onClick;
        return button;
    }

    /// <summary>A row of controls, left to right.</summary>
    public static FlowLayoutPanel Bar(params Control[] controls)
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Padding = new Padding(0, 4, 0, 4),
        };
        bar.Controls.AddRange(controls);
        return bar;
    }

    /// <summary>Controls side by side in one field of a dialog.</summary>
    public static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        row.Controls.AddRange(controls);
        return row;
    }

    /// <summary>A label that lines up with the controls in a <see cref="Bar"/>.</summary>
    public static Label Text(string text = "", bool bold = false)
    {
        var label = new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 3, 3) };
        if (bold)
            label.Font = new Font(label.Font, FontStyle.Bold);
        return label;
    }

    public static DateTimePicker DatePicker(int width = 160) =>
        new() { Format = DateTimePickerFormat.Custom, CustomFormat = Formats.DatePickerFormat, Width = width };

    public static ComboBox DropDown(int width = 200) =>
        new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = width };

    public static NumericUpDown Number(decimal min, decimal max, int places = 0, int width = 100) =>
        new() { Minimum = min, Maximum = max, DecimalPlaces = places, Width = width, ThousandsSeparator = places > 0, TextAlign = HorizontalAlignment.Right };

    /// <summary>
    /// One control above another with a movable divider, the top taking <paramref name="topShare"/> of the height. The
    /// divider is placed once the container has a real size, since placing it earlier can be out of range.
    /// </summary>
    public static SplitContainer Split(Control top, Control bottom, double topShare)
    {
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6 };
        top.Dock = DockStyle.Fill;
        bottom.Dock = DockStyle.Fill;
        split.Panel1.Controls.Add(top);
        split.Panel2.Controls.Add(bottom);

        var placed = false;
        split.SizeChanged += (_, _) =>
        {
            if (placed || split.Height < 4 * (split.Panel1MinSize + split.Panel2MinSize))
                return;
            split.SplitterDistance = (int)(split.Height * topShare);
            placed = true;
        };
        return split;
    }

    /// <summary>A titled box that fills its space. Docked edge controls go before the one that fills.</summary>
    public static GroupBox Group(string title, params Control[] controls)
    {
        var group = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(8) };
        // Docked controls are laid out last-added first, so add them in reverse.
        for (var i = controls.Length - 1; i >= 0; i--)
            group.Controls.Add(controls[i]);
        return group;
    }
}

/// <summary>An item in a drop-down list: what is shown, and the value behind it.</summary>
internal sealed record Choice<T>(string Text, T Value)
{
    public override string ToString() => Text;
}

internal static class ChoiceExtensions
{
    public static void SetChoices<T>(this ComboBox box, IEnumerable<Choice<T>> choices)
    {
        box.Items.Clear();
        foreach (var choice in choices)
            box.Items.Add(choice);
    }

    public static T? SelectedValue<T>(this ComboBox box) => box.SelectedItem is Choice<T> choice ? choice.Value : default;

    /// <summary>Selects the first item with <paramref name="value"/>; selects nothing if there is none.</summary>
    public static void Select<T>(this ComboBox box, T value)
    {
        box.SelectedIndex = -1;
        for (var i = 0; i < box.Items.Count; i++)
        {
            if (box.Items[i] is Choice<T> choice && EqualityComparer<T>.Default.Equals(choice.Value, value))
            {
                box.SelectedIndex = i;
                return;
            }
        }
    }
}
