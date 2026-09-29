using System.Text.Json;

namespace ArenaOwl;

// Remembers the last choices so the window opens the way you left it.
class AppSettings
{
    public string Browser { get; set; } = nameof(BrowserKind.Edge);
    public string? Monitor { get; set; }
    public int Count { get; set; } = 4;
    public bool CopyLogins { get; set; } = true;
    public string[] Slots { get; set; } = { "Prime", "ESPN", "FoxOne", "Paramount" };

    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ArenaOwl", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { /* corrupt or unreadable settings: fall back to defaults */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* not worth interrupting a launch over */ }
    }
}
