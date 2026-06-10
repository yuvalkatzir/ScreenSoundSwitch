using Crescendo.Core.Theming;
using Crescendo.Core.Widgets;
using Crescendo.Platform.SDL;
using NAudio.CoreAudioApi;
using ScreenSoundSwitch;
using ScreenSoundSwitch.App.Data;
using ScreenSoundSwitch.App.Interop;
using ScreenSoundSwitch.App.Pages;
using ScreenSoundSwitch.App.Tray;
using Serilog;
using SkiaSharp;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

if (args.Contains("--detect"))
{
    RunDetect();
    return;
}

var logDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "ScreenSoundSwitch", "logs");
Directory.CreateDirectory(logDir);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(
        Path.Combine(logDir, "app-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    ScreenToAudioDevice.Instance.LoadConfig(
        deviceId => AudioDeviceManager.Instance.GetDeviceById(deviceId));
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to load audio config");
}

Crescendo.Core.State.DataStore.Initialize("ScreenSoundSwitch");
DebugLogStore.Initialize();

var themes = Theme.FromSeed(new SKColor(0x33, 0x99, 0xFF));
ThemeProvider.SetTheme(themes.Dark);

var tabView = new TabView()
    .AddTab("Displays", SelectDevicePage.Build)
    .AddTab("Volume", VolumePage.Build)
    .AddTab("Settings", SettingsPage.Build)
    .AddTab("Debug", DebugPage.Build);

var tray = new TrayIcon();

AppDomain.CurrentDomain.ProcessExit += (_, _) =>
{
    VolumePage.Shutdown();
    tray.Dispose();
    Log.CloseAndFlush();
};

var app = CrescendoApp.Create()
    .WithTitle("ScreenSoundSwitch")
    .WithSize(1100, 750)
    .WithRootWidget(() => tabView)
    .OnWindowReady(window => tray.Attach(window));

if (AppSettings.EnableTrayIcon)
    app.StartHidden();

app
    .Run();

static void RunDetect()
{
    var lines = new List<string>();
    void L(string s = "") => lines.Add(s);

    L("=== Audio session / monitor detection ===");
    L();

    var manager = AudioDeviceManager.Instance;

    foreach (MMDevice device in manager.Devices)
    {
        L($"Device: {device.FriendlyName}");
        try
        {
            device.AudioSessionManager.RefreshSessions();
            var sessions = device.AudioSessionManager.Sessions;
            int count = sessions?.Count ?? 0;

            for (int i = 0; i < count; i++)
            {
                AudioSessionControl session;
                try { session = sessions![i]; } catch { continue; }
                if (session.IsSystemSoundsSession) continue;

                uint pid = 0;
                try { pid = session.GetProcessID; } catch { continue; }

                Process? proc = null;
                try { proc = Process.GetProcessById((int)pid); } catch { }

                string procName = proc?.ProcessName ?? $"PID {pid}";
                IntPtr hwnd = WindowFinder.FindVisibleWindow(pid);
                string screenName = "(no window handle)";

                if (hwnd != IntPtr.Zero)
                {
                    var screen = Screen.FromHandle(hwnd);
                    screenName = screen.DeviceName;
                    if (screen.Primary) screenName += " [primary]";
                }

                L($"  {procName,-30} pid={pid,-6} hwnd=0x{hwnd:X}  screen={screenName}");
            }
        }
        catch (Exception ex)
        {
            L($"  (failed to enumerate sessions: {ex.Message})");
        }
        L();
    }

    L("=== Screens ===");
    foreach (var s in Screen.AllScreens)
        L($"  {s.DeviceName}  bounds={s.Bounds}  primary={s.Primary}");

    var path = Path.Combine(Path.GetTempPath(), "sss-detect.txt");
    File.WriteAllLines(path, lines, System.Text.Encoding.UTF8);
    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
}
