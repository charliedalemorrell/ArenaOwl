namespace ArenaOwl;

class MainForm : Form
{
    const int MaxScreens = 4;

    readonly Screen[] screens = Screen.AllScreens
        .OrderBy(s => s.Bounds.Left).ThenBy(s => s.Bounds.Top).ToArray();

    readonly ComboBox monitorBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    readonly ComboBox browserBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    readonly NumericUpDown countBox = new() { Minimum = 1, Maximum = MaxScreens, Dock = DockStyle.Left, Width = 60 };
    readonly CheckBox copyLoginsBox = new()
    {
        Text = "Copy my browser logins into new windows",
        AutoSize = true,
    };
    readonly ComboBox[] slotBoxes = new ComboBox[MaxScreens];
    readonly PreviewPanel preview = new() { Dock = DockStyle.Fill };
    readonly Button launchButton = new() { Text = "Launch", Dock = DockStyle.Fill, Height = 44, Font = new Font("Segoe UI", 11f, FontStyle.Bold) };
    readonly TextBox logBox = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };

    public MainForm()
    {
        Text = "ArenaOwl";
        Font = new Font("Segoe UI", 10f);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(820, 520);
        MinimumSize = new Size(780, 500);

        // ---- left column: controls ----
        var controls = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
        };
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(controls, "Monitor", monitorBox);
        AddRow(controls, "Browser", browserBox);
        AddRow(controls, "Screens", countBox);

        controls.Controls.Add(copyLoginsBox, 0, controls.RowCount);
        controls.SetColumnSpan(copyLoginsBox, 2);
        controls.RowCount++;

        for (int i = 0; i < MaxScreens; i++)
        {
            var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            box.Items.AddRange(Platform.All);
            box.SelectedIndex = i % Platform.All.Length;
            box.SelectedIndexChanged += (_, _) => RefreshPreview();
            slotBoxes[i] = box;
            AddRow(controls, $"Screen {i + 1}", box);
        }

        controls.Controls.Add(launchButton, 0, controls.RowCount);
        controls.SetColumnSpan(launchButton, 2);
        controls.RowCount++;

        controls.Controls.Add(logBox, 0, controls.RowCount);
        controls.SetColumnSpan(logBox, 2);
        controls.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        controls.RowCount++;

        // ---- layout ----
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(controls, 0, 0);
        preview.Margin = new Padding(0, 12, 12, 12);
        root.Controls.Add(preview, 1, 0);
        Controls.Add(root);

        // ---- initial values ----
        for (int i = 0; i < screens.Length; i++)
        {
            var b = screens[i].Bounds;
            monitorBox.Items.Add($"{i + 1}: {b.Width}x{b.Height}{(screens[i].Primary ? " (primary)" : "")}");
        }
        browserBox.Items.AddRange(Enum.GetNames<BrowserKind>());

        var settings = AppSettings.Load();
        int monitorIndex = Array.FindIndex(screens, s => s.DeviceName == settings.Monitor);
        if (monitorIndex < 0)
            monitorIndex = Math.Max(0, Array.FindIndex(screens, s => s.Primary));
        monitorBox.SelectedIndex = monitorIndex;
        browserBox.SelectedIndex = Math.Max(0, browserBox.FindStringExact(settings.Browser));
        countBox.Value = Math.Clamp(settings.Count, 1, MaxScreens);
        copyLoginsBox.Checked = settings.CopyLogins;
        for (int i = 0; i < MaxScreens && i < settings.Slots.Length; i++)
        {
            int idx = Array.FindIndex(Platform.All, p => p.ProfileKey == settings.Slots[i]);
            if (idx >= 0)
                slotBoxes[i].SelectedIndex = idx;
        }

        monitorBox.SelectedIndexChanged += (_, _) => RefreshPreview();
        countBox.ValueChanged += (_, _) => RefreshPreview();
        launchButton.Click += async (_, _) => await LaunchAsync();
        RefreshPreview();
    }

    static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        int row = table.RowCount;
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 12, 8) }, 0, row);
        control.Margin = new Padding(0, 4, 0, 4);
        table.Controls.Add(control, 1, row);
        table.RowCount++;
    }

    int Count => (int)countBox.Value;

    List<Platform> SelectedSlots() =>
        slotBoxes.Take(Count).Select(b => (Platform)b.SelectedItem!).ToList();

    void RefreshPreview()
    {
        for (int i = 0; i < MaxScreens; i++)
            slotBoxes[i].Enabled = i < Count;

        var area = screens[monitorBox.SelectedIndex].WorkingArea;
        preview.Update(area.Size, SelectedSlots().Select(p => p.Name).ToList());
    }

    async Task LaunchAsync()
    {
        var screen = screens[monitorBox.SelectedIndex];
        var browser = Enum.Parse<BrowserKind>((string)browserBox.SelectedItem!);
        var slots = SelectedSlots();

        new AppSettings
        {
            Browser = browser.ToString(),
            Monitor = screen.DeviceName,
            Count = Count,
            CopyLogins = copyLoginsBox.Checked,
            Slots = slotBoxes.Select(b => ((Platform)b.SelectedItem!).ProfileKey).ToArray(),
        }.Save();

        launchButton.Enabled = false;
        logBox.Clear();
        void Log(string message) => BeginInvoke(() => logBox.AppendText(message + Environment.NewLine));

        try
        {
            var windows = await Task.Run(() => Launcher.Launch(browser, screen.WorkingArea, slots, copyLoginsBox.Checked, Log));
            if (windows.Count > 0)
                ShowControls(windows, screen.WorkingArea);
        }
        finally
        {
            launchButton.Enabled = true;
        }
    }

    // Puts the sound/close controls in the middle of the tiles and gets this window out of the way.
    void ShowControls(List<LaunchedWindow> windows, Rectangle workArea)
    {
        var center = new Point(workArea.Left + workArea.Width / 2, workArea.Top + workArea.Height / 2);
        var overlay = new ControlOverlay(windows, center);
        overlay.FormClosed += (_, _) =>
        {
            WindowState = FormWindowState.Normal;
            Activate();
        };
        overlay.Show();
        WindowState = FormWindowState.Minimized;
    }

    // Draws a miniature of the chosen monitor with each screen's tile and platform name.
    class PreviewPanel : Panel
    {
        Size monitorSize = new(16, 9);
        List<string> labels = new();

        public PreviewPanel() => DoubleBuffered = true;

        public void Update(Size monitor, List<string> names)
        {
            monitorSize = monitor;
            labels = names;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Fit the monitor's aspect ratio inside the panel.
            float scale = Math.Min((float)(Width - 2) / monitorSize.Width, (float)(Height - 2) / monitorSize.Height);
            int w = (int)(monitorSize.Width * scale), h = (int)(monitorSize.Height * scale);
            var origin = new Point((Width - w) / 2, (Height - h) / 2);

            using var outline = new Pen(Color.DimGray, 2);
            using var fill = new SolidBrush(Color.FromArgb(40, 60, 90));
            using var border = new Pen(Color.White, 1);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var textBrush = new SolidBrush(Color.White);

            g.FillRectangle(Brushes.Black, origin.X, origin.Y, w, h);

            var tiles = Launcher.Layout(new Rectangle(0, 0, w, h), labels.Count);
            for (int i = 0; i < tiles.Count; i++)
            {
                var t = tiles[i];
                var r = new Rectangle(origin.X + t.X + 2, origin.Y + t.Y + 2, t.Width - 4, t.Height - 4);
                g.FillRectangle(fill, r);
                g.DrawRectangle(border, r);
                g.DrawString($"{i + 1}\n{labels[i]}", Font, textBrush, r, format);
            }
            g.DrawRectangle(outline, origin.X, origin.Y, w, h);
        }
    }
}
