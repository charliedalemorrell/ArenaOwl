using System.Runtime.InteropServices;

namespace ArenaOwl;

// A small always-on-top strip at the center of the tiled windows: one toggle per window to pick
// which one plays sound, plus a button that closes everything. It never takes focus, so clicking
// it doesn't disturb the video windows.
class ControlOverlay : Form
{
    const int WS_EX_TOOLWINDOW = 0x00000080;
    const int WS_EX_NOACTIVATE = 0x08000000;
    const int WS_EX_TRANSPARENT = 0x00000020;
    const int WS_EX_LAYERED = 0x00080000;

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    static readonly IntPtr HWND_TOPMOST = new(-1);
    const uint SWP_NOSIZE = 0x0001;
    const uint SWP_NOMOVE = 0x0002;
    const uint SWP_NOACTIVATE = 0x0010;

    static readonly Color OnColor = Color.FromArgb(40, 160, 70);
    static readonly Color OffColor = Color.FromArgb(70, 70, 70);

    readonly List<LaunchedWindow> windows;
    readonly CheckBox[] toggles;
    readonly FrameOverlay frame = new();
    readonly System.Windows.Forms.Timer watchTimer = new() { Interval = 1500 };
    bool updating;
    int active = 0;   // index of the window with sound, or -1 for none

    public ControlOverlay(List<LaunchedWindow> windows, Point center)
    {
        this.windows = windows;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(30, 30, 30);

        const int toggleSize = 40, gap = 6, pad = 6, closeWidth = 90;
        var tips = new ToolTip();
        toggles = new CheckBox[windows.Count];

        int x = pad;
        for (int i = 0; i < windows.Count; i++)
        {
            int index = i;
            var toggle = new CheckBox
            {
                Appearance = Appearance.Button,
                Text = (i + 1).ToString(),
                TextAlign = ContentAlignment.MiddleCenter,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.White,
                Size = new Size(toggleSize, toggleSize),
                Location = new Point(x, pad),
                Cursor = Cursors.Hand,
            };
            toggle.FlatAppearance.BorderSize = 0;
            toggle.CheckedChanged += (_, _) => OnToggled(index);
            tips.SetToolTip(toggle, $"Sound: {windows[i].Label}");
            toggles[i] = toggle;
            Controls.Add(toggle);
            x += toggleSize + gap;
        }

        var close = new Button
        {
            Text = "✕ Close all",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(180, 40, 40),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Size = new Size(closeWidth, toggleSize),
            Location = new Point(x + gap, pad),
            Cursor = Cursors.Hand,
        };
        close.FlatAppearance.BorderSize = 0;
        close.Click += (_, _) => CloseAll();
        Controls.Add(close);

        Size = new Size(x + gap + closeWidth + pad, toggleSize + pad * 2);
        Location = new Point(center.X - Width / 2, center.Y - Height / 2);

        SetActive(0);

        // Audio sessions only appear once a page starts playing, and pages can restart them,
        // so re-apply the mute state regularly. Also notice when everything has been closed.
        watchTimer.Tick += (_, _) =>
        {
            if (windows.All(w => HasExited(w)))
            {
                Close();
                return;
            }
            AudioRouter.Apply(windows.Select(w => w.Process).ToList(), active);
        };
        watchTimer.Start();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        UpdateFrame();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        watchTimer.Stop();
        watchTimer.Dispose();
        frame.Close();
        base.OnFormClosed(e);
    }

    // Checking one box unchecks the others; unchecking the current one leaves everything muted.
    void OnToggled(int index)
    {
        if (updating)
            return;
        SetActive(toggles[index].Checked ? index : -1);
    }

    void SetActive(int index)
    {
        active = index;

        updating = true;
        for (int i = 0; i < toggles.Length; i++)
        {
            toggles[i].Checked = i == index;
            toggles[i].BackColor = i == index ? OnColor : OffColor;
        }
        updating = false;

        AudioRouter.Apply(windows.Select(w => w.Process).ToList(), active);
        UpdateFrame();
    }

    // Draws a colored frame around the window that has sound.
    void UpdateFrame()
    {
        if (!IsHandleCreated)
            return;

        if (active < 0)
        {
            frame.Hide();
            return;
        }

        frame.Bounds = windows[active].Tile;
        if (!frame.Visible)
            frame.Show();

        // Keep the controls above the frame.
        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    static bool HasExited(LaunchedWindow w)
    {
        try { return w.Process.HasExited; }
        catch (InvalidOperationException) { return true; }
    }

    async void CloseAll()
    {
        watchTimer.Stop();

        // Ask nicely first so the browsers can save their state...
        foreach (var w in windows)
        {
            try { if (!w.Process.HasExited) w.Process.CloseMainWindow(); }
            catch (InvalidOperationException) { }
        }

        await Task.Delay(3000);

        // ...then force-close anything that's still running.
        foreach (var w in windows)
        {
            try { if (!w.Process.HasExited) w.Process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        }

        Close();
    }

    // A see-through window that only paints a colored border, and lets clicks pass through
    // to the video underneath.
    class FrameOverlay : Form
    {
        const int BorderWidth = 5;
        static readonly Color Key = Color.Magenta;

        public FrameOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Key;
            TransparencyKey = Key;
            DoubleBuffered = true;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_LAYERED;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var brush = new SolidBrush(OnColor);
            var g = e.Graphics;
            g.FillRectangle(brush, 0, 0, Width, BorderWidth);
            g.FillRectangle(brush, 0, Height - BorderWidth, Width, BorderWidth);
            g.FillRectangle(brush, 0, 0, BorderWidth, Height);
            g.FillRectangle(brush, Width - BorderWidth, 0, BorderWidth, Height);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }
    }
}
