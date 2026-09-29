using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace ArenaOwl;

// Makes one browser window audible and mutes the rest, using the same per-app controls as the
// Windows volume mixer. Browsers play sound from a helper process (a child of the browser process
// we started), so each audio session is traced up its parent chain to find which window it is.
static class AudioRouter
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct PROCESSENTRY32
    {
        public int dwSize;
        public int cntUsage;
        public int th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public int th32ModuleID;
        public int cntThreads;
        public int th32ParentProcessID;
        public int pcPriClassBase;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr hObject);

    const uint TH32CS_SNAPPROCESS = 0x2;

    // Maps every running process id to its parent's id.
    static Dictionary<int, int> GetParentMap()
    {
        var map = new Dictionary<int, int>();
        IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == new IntPtr(-1))
            return map;
        try
        {
            var entry = new PROCESSENTRY32 { dwSize = Marshal.SizeOf<PROCESSENTRY32>() };
            for (bool ok = Process32First(snapshot, ref entry); ok; ok = Process32Next(snapshot, ref entry))
                map[entry.th32ProcessID] = entry.th32ParentProcessID;
        }
        finally
        {
            CloseHandle(snapshot);
        }
        return map;
    }

    // Unmutes the window at activeIndex and mutes every other window. Pass -1 to mute them all.
    // Audio sessions only exist once a page starts playing, so call this repeatedly.
    public static void Apply(IReadOnlyList<Process> windows, int activeIndex)
    {
        var windowIndexByPid = new Dictionary<int, int>();
        for (int i = 0; i < windows.Count; i++)
        {
            try { windowIndexByPid[windows[i].Id] = i; }
            catch (InvalidOperationException) { }
        }

        try
        {
            var parents = GetParentMap();
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager.Sessions;

            for (int i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                int index = FindWindow((int)session.GetProcessID, parents, windowIndexByPid);
                if (index >= 0)
                    session.SimpleAudioVolume.Mute = index != activeIndex;
            }
        }
        catch (Exception)
        {
            // No audio device, or a session vanished mid-scan. The next tick tries again.
        }
    }

    // Walks up the parent chain from an audio process until it reaches one of our browser processes.
    static int FindWindow(int pid, Dictionary<int, int> parents, Dictionary<int, int> windowIndexByPid)
    {
        for (int depth = 0; depth < 10 && pid > 0; depth++)
        {
            if (windowIndexByPid.TryGetValue(pid, out int index))
                return index;
            if (!parents.TryGetValue(pid, out pid))
                break;
        }
        return -1;
    }
}
