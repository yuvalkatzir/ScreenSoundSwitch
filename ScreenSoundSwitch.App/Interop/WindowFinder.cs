using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenSoundSwitch.App.Interop;

public static class WindowFinder
{
    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern long GetWindowLongW(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, ref RECT rect);

    [DllImport("kernel32.dll")] private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);
    [DllImport("kernel32.dll")] private static extern bool Process32First(IntPtr snapshot, ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll")] private static extern bool Process32Next(IntPtr snapshot, ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

    private const int GWL_STYLE = -16;
    private const long WS_VISIBLE = 0x10000000L;
    private const uint TH32CS_SNAPPROCESS = 0x00000002;

    /// <summary>
    /// Finds the best visible top-level window for a process or any of its ancestors.
    /// Handles multi-process apps like Chromium-based browsers where the audio session
    /// belongs to a renderer subprocess but the visible window is in the parent process.
    /// Works without elevation.
    /// </summary>
    public static IntPtr FindVisibleWindow(uint pid)
    {
        // Walk up to 6 levels of the process tree looking for a window.
        var current = pid;
        for (int depth = 0; depth < 6; depth++)
        {
            var hwnd = FindWindowForPid(current);
            if (hwnd != IntPtr.Zero) return hwnd;

            var parent = GetParentProcessId(current);
            if (parent == 0 || parent == current) break;
            current = parent;
        }

        return IntPtr.Zero;
    }

    private static IntPtr FindWindowForPid(uint pid)
    {
        IntPtr best = IntPtr.Zero;
        IntPtr bestOnScreen = IntPtr.Zero;

        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out uint windowPid);
            if (windowPid != pid) return true;
            if (!IsWindowVisible(hwnd)) return true;
            if (GetParent(hwnd) != IntPtr.Zero) return true;

            var style = GetWindowLongW(hwnd, GWL_STYLE);
            if ((style & WS_VISIBLE) == 0) return true;

            if (best == IntPtr.Zero)
                best = hwnd;

            RECT r = default;
            if (GetWindowRect(hwnd, ref r))
            {
                var bounds = new System.Drawing.Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
                var screen = Screen.FromHandle(hwnd);
                if (screen != null && screen.Bounds.IntersectsWith(bounds) && bestOnScreen == IntPtr.Zero)
                    bestOnScreen = hwnd;
            }

            return true;
        }, IntPtr.Zero);

        return bestOnScreen != IntPtr.Zero ? bestOnScreen : best;
    }

    public static uint GetParentProcessId(uint pid)
    {
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1)) return 0;

        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (!Process32First(snapshot, ref entry)) return 0;

            do
            {
                if (entry.th32ProcessID == pid)
                    return entry.th32ParentProcessID;
            }
            while (Process32Next(snapshot, ref entry));

            return 0;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }
}
