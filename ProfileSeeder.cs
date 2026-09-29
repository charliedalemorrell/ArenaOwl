using System.Text.Json;

namespace ArenaOwl;

// Gives a fresh ArenaOwl browser profile the site logins from the user's normal browser profile,
// so the streaming sites are already signed in the first time each window opens.
//
// Only a few things are copied: the browser's "Local State" (holds the key that decrypts cookies),
// the profile's cookies, and its local storage. Nothing else (history, passwords, extensions, or
// the browser's own account sign-in) comes along.
static class ProfileSeeder
{
    static string? UserDataDir(BrowserKind browser)
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string dir = browser == BrowserKind.Edge
            ? Path.Combine(local, "Microsoft", "Edge", "User Data")
            : Path.Combine(local, "Google", "Chrome", "User Data");
        return Directory.Exists(dir) ? dir : null;
    }

    // The profile folder the browser used last, e.g. "Default" or "Profile 2".
    static string LastUsedProfile(string userDataDir)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(userDataDir, "Local State")));
            if (doc.RootElement.TryGetProperty("profile", out var profile) &&
                profile.TryGetProperty("last_used", out var lastUsed) &&
                lastUsed.GetString() is { Length: > 0 } name)
                return name;
        }
        catch { /* fall through to the default */ }
        return "Default";
    }

    // A profile is "fresh" until it has been seeded (or the browser has run in it and made its own).
    public static bool NeedsSeed(string targetDir) => !File.Exists(Path.Combine(targetDir, "Local State"));

    // Returns false only when the copy failed and the launch should stop (usually because the
    // browser is still running and has its cookie file locked). Never throws; problems go to log.
    public static bool Seed(BrowserKind browser, string targetDir, Action<string> log)
    {
        string? source = UserDataDir(browser);
        if (source == null || !File.Exists(Path.Combine(source, "Local State")))
        {
            log($"Couldn't find your {browser} profile to copy logins from, so the windows will start logged out.");
            return true;
        }

        string profile = LastUsedProfile(source);
        string sourceProfile = Path.Combine(source, profile);
        if (!Directory.Exists(sourceProfile))
        {
            log($"Couldn't find the {browser} profile folder '{profile}', so the windows will start logged out.");
            return true;
        }

        try
        {
            // Keep the same profile folder name: "Local State" refers to it as the last-used profile.
            string targetProfile = Path.Combine(targetDir, profile);
            Directory.CreateDirectory(Path.Combine(targetProfile, "Network"));

            // Files are usually open in the running browser, so read them with sharing allowed.
            CopyShared(Path.Combine(sourceProfile, "Network", "Cookies"), Path.Combine(targetProfile, "Network", "Cookies"));
            CopyShared(Path.Combine(sourceProfile, "Network", "Cookies-journal"), Path.Combine(targetProfile, "Network", "Cookies-journal"));
            CopyDirectoryShared(Path.Combine(sourceProfile, "Local Storage"), Path.Combine(targetProfile, "Local Storage"));

            // Copied last: its presence marks the profile as seeded.
            CopyShared(Path.Combine(source, "Local State"), Path.Combine(targetDir, "Local State"));
            return true;
        }
        catch (IOException)
        {
            log($"Couldn't copy your logins because {browser} is still running. Close every {browser} window " +
                $"(also check the system tray), then click Launch again. Or untick \"Copy my browser logins\".");
            return false;
        }
        catch (Exception ex)
        {
            log($"Couldn't copy logins: {ex.Message}");
            return false;
        }
    }

    static void CopyShared(string from, string to)
    {
        if (!File.Exists(from))
            return;
        using var input = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var output = new FileStream(to, FileMode.Create, FileAccess.Write);
        input.CopyTo(output);
    }

    static void CopyDirectoryShared(string from, string to)
    {
        if (!Directory.Exists(from))
            return;
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
            CopyShared(file, Path.Combine(to, Path.GetFileName(file)));
        foreach (var dir in Directory.GetDirectories(from))
            CopyDirectoryShared(dir, Path.Combine(to, Path.GetFileName(dir)));
    }
}
