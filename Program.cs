// ArenaOwl - pick streaming platforms and how many screens, then tile them in Edge or Chrome.
//
// Run with `dotnet run`, or publish a single .exe (see CLAUDE.md).

namespace ArenaOwl;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Per-monitor DPI so window coordinates are real pixels on every monitor.
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
