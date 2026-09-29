using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ArenaOwl;

// One launched browser window: what it shows, the process that owns it, and where it sits.
record LaunchedWindow(string Label, Process Process, Rectangle Tile);

static class Launcher
{
    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("dwmapi.dll")]
    static extern int DwmGetWindowAttribute(IntPtr hWnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    const uint SWP_NOZORDER = 0x0004;
    const uint SWP_NOACTIVATE = 0x0010;

    // Splits an area into tiles for 1-4 screens:
    //   1: full   2: side by side   3: one big on the left, two stacked on the right   4: 2x2 grid
    public static List<Rectangle> Layout(Rectangle area, int count)
    {
        int w = area.Width, h = area.Height;
        int halfW = w / 2, halfH = h / 2;
        int x0 = area.Left, y0 = area.Top;

        return count switch
        {
            1 => new() { area },
            2 => new()
            {
                new(x0, y0, halfW, h),
                new(x0 + halfW, y0, w - halfW, h),
            },
            3 => new()
            {
                new(x0, y0, halfW, h),
                new(x0 + halfW, y0, w - halfW, halfH),
                new(x0 + halfW, y0 + halfH, w - halfW, h - halfH),
            },
            _ => new()
            {
                new(x0, y0, halfW, halfH),
                new(x0 + halfW, y0, w - halfW, halfH),
                new(x0, y0 + halfH, halfW, h - halfH),
                new(x0 + halfW, y0 + halfH, w - halfW, h - halfH),
            },
        };
    }

    // Returns a path (or command name) that Process.Start can launch, or null if not found.
    public static string? FindBrowser(BrowserKind browser)
    {
        if (browser == BrowserKind.Edge)
            return "msedge";   // registered in Windows App Paths, so the name is enough

        string[] roots =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        };
        foreach (var root in roots)
        {
            string path = Path.Combine(root, "Google", "Chrome", "Application", "chrome.exe");
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    // Launches one browser app window per slot and positions it. Blocks while waiting for
    // windows to appear, so call it from a background thread. Returns the windows it started.
    // copyLogins seeds each new profile with the site logins from the user's normal browser profile.
    public static List<LaunchedWindow> Launch(BrowserKind browser, Rectangle workArea, IReadOnlyList<Platform> slots, bool copyLogins, Action<string> log)
    {
        string? browserPath = FindBrowser(browser);
        if (browserPath == null)
        {
            log($"Couldn't find {browser}. Is it installed?");
            return new List<LaunchedWindow>();
        }

        string profilesRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArenaOwl", browser.ToString());

        var tiles = Layout(workArea, slots.Count);
        var launched = new List<(string Label, Process Process, Rectangle Tile)>();

        // Work out each window's label and profile folder up front. Two windows of the same
        // service need separate profiles (one profile = one browser process), so the 2nd ESPN
        // gets "ESPN-2", and so on. Each keeps its own login.
        var seen = new Dictionary<string, int>();
        var plan = new List<(string Label, string ProfileDir)>();
        foreach (var platform in slots)
        {
            seen[platform.ProfileKey] = seen.GetValueOrDefault(platform.ProfileKey) + 1;
            int n = seen[platform.ProfileKey];
            string profileName = n == 1 ? platform.ProfileKey : $"{platform.ProfileKey}-{n}";
            string label = n == 1 ? platform.Name : $"{platform.Name} #{n}";
            plan.Add((label, Path.Combine(profilesRoot, profileName)));
        }

        // A brand-new profile starts logged out, so copy the user's site logins into it. Do this
        // for every window before opening any, so a failure doesn't leave a half-launched wall.
        foreach (var (label, profileDir) in plan)
        {
            Directory.CreateDirectory(profileDir);
            if (copyLogins && ProfileSeeder.NeedsSeed(profileDir))
            {
                if (!ProfileSeeder.Seed(browser, profileDir, log))
                    return new List<LaunchedWindow>();
                log($"Copied your {browser} logins for {label}");
            }
        }

        for (int i = 0; i < slots.Count; i++)
        {
            var platform = slots[i];
            var tile = tiles[i];
            var (label, profileDir) = plan[i];

            string args =
                $"--app=\"{platform.Url}\" " +
                $"--user-data-dir=\"{profileDir}\" " +
                $"--window-position={tile.X},{tile.Y} " +
                $"--window-size={tile.Width},{tile.Height} " +
                "--autoplay-policy=no-user-gesture-required " +
                "--no-first-run";

            try
            {
                var process = Process.Start(new ProcessStartInfo(browserPath, args) { UseShellExecute = true });
                if (process != null)
                    launched.Add((label, process, tile));
                log($"Launched {label}");
            }
            catch (Exception ex)
            {
                log($"Could not launch {label}: {ex.Message}");
            }
        }

        // The browser's own placement flags can be off on monitors with different scaling, so
        // place each window explicitly. Twice: it may resize itself after landing on the monitor.
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (var (label, process, tile) in launched)
            {
                IntPtr hwnd = WaitForWindow(process, TimeSpan.FromSeconds(15));
                if (hwnd == IntPtr.Zero)
                {
                    if (pass == 0)
                        log($"Couldn't position {label}. If its profile was already open, close all the windows and try again.");
                    continue;
                }
                PlaceWindow(hwnd, tile);
            }
            if (pass == 0)
                Thread.Sleep(2000);
        }

        log("Done. Enjoy the games!");
        return launched.Select(l => new LaunchedWindow(l.Label, l.Process, l.Tile)).ToList();
    }

    // Windows 10/11 windows have an invisible border (~7px on the sides and bottom) around what
    // you actually see, which leaves visible gaps between tiles. Measure how far the visible
    // frame sits inside the window rect and grow the window by that much so the visible part
    // lands exactly on the tile.
    static void PlaceWindow(IntPtr hwnd, Rectangle tile)
    {
        SetWindowPos(hwnd, IntPtr.Zero, tile.X, tile.Y, tile.Width, tile.Height, SWP_NOZORDER | SWP_NOACTIVATE);

        if (!GetWindowRect(hwnd, out RECT outer) ||
            DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT visible, Marshal.SizeOf<RECT>()) != 0)
            return;

        int left = visible.Left - outer.Left;
        int top = visible.Top - outer.Top;
        int right = outer.Right - visible.Right;
        int bottom = outer.Bottom - visible.Bottom;

        SetWindowPos(hwnd, IntPtr.Zero,
            tile.X - left, tile.Y - top,
            tile.Width + left + right, tile.Height + top + bottom,
            SWP_NOZORDER | SWP_NOACTIVATE);
    }

    // The browser creates its window a moment after the process starts; poll for it.
    static IntPtr WaitForWindow(Process process, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                process.Refresh();
                if (process.HasExited)
                    return IntPtr.Zero;   // handed off to an existing browser process
                if (process.MainWindowHandle != IntPtr.Zero)
                    return process.MainWindowHandle;
            }
            catch (InvalidOperationException)
            {
                return IntPtr.Zero;
            }
            Thread.Sleep(200);
        }
        return IntPtr.Zero;
    }
}
