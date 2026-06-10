using Crescendo.Core.Animation;
using Crescendo.Core.Layout;
using Crescendo.Core.Widgets;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using ScreenSoundSwitch.App.Data;
using ScreenSoundSwitch.App.Interop;
using ScreenSoundSwitch.App.Widgets;
using SoundSwitch.Audio.Manager;
using SoundSwitch.Audio.Manager.Interop.Com.User;
using SoundSwitch.Audio.Manager.Interop.Enum;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenSoundSwitch.App.Pages;

public static class VolumePage
{
    private static WindowMonitor? _monitor;
    private static readonly ConcurrentQueue<Action> _uiQueue = new();

    // PID -> (session, device, lastScreen)
    private sealed class SessionEntry
    {
        public uint Pid;
        public AudioSessionControl Session;
        public MMDevice Device;
        public Screen? LastScreen;
        public Slider? VolumeSlider;

        public SessionEntry(uint pid, AudioSessionControl session, MMDevice device)
        {
            Pid = pid;
            Session = session;
            Device = device;
        }
    }

    private static readonly Dictionary<uint, SessionEntry> _sessionMap = new();
    // Maps parent process PID -> session entry, so window-process events (e.g. main
    // Brave process) resolve to the correct audio renderer subprocess session.
    private static readonly Dictionary<uint, SessionEntry> _parentMap = new();
    private static uint _foregroundPid;

    // widget refs
    private static Text _statusText = null!;
    private static Container _devicesContainer = null!;

    public static Widget Build()
    {
        _statusText = new Text("Monitoring window focus...").FontSize(13);
        _devicesContainer = Container.Column(gap: 12, padding: 0);

        BuildDevices();
        StartMonitor();
        RouteAllSessions();

        AnimationScheduler.AddUpdater(_ =>
        {
            while (_uiQueue.TryDequeue(out var action))
                action();
            return true;
        });

        var page = Container.Column(gap: 16, padding: 24,
            new Text("Volume Control").FontSize(22).Bold(),
            _statusText,
            new Divider(),
            _devicesContainer
        );

        return new ScrollView(page);
    }

    public static void Shutdown()
    {
        _monitor?.Stop();
        _monitor = null;
    }

// --- Device / session building ---

    private static void BuildDevices()
    {
        _devicesContainer.ClearChildren();
        _sessionMap.Clear();
        _parentMap.Clear();

        var manager = AudioDeviceManager.Instance;
        foreach (MMDevice device in manager.Devices)
        {
            var card = BuildDeviceCard(device);
            _devicesContainer.AddChild(card);
        }
    }

    private static Widget BuildDeviceCard(MMDevice device)
    {
        var card = new Card();
        card.Style = new LayoutStyle
        {
            FlexDirection = FlexDirection.Column,
            Padding = new Edges(16),
            Gap = 10,
        };

        card.AddChild(new Text(device.FriendlyName).FontSize(16).Bold());

        // Master volume
        var masterSlider = new Slider(0, 100, device.AudioEndpointVolume.MasterVolumeLevelScalar * 100)
            .Step(1)
            .OnValueChanged(v =>
            {
                try { device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)(v / 100); }
                catch { }
            });
        card.AddChild(Container.Row(gap: 8, padding: 0,
            new Text("Master").FontSize(12),
            masterSlider
        ));

        // L/R channel sliders if stereo
        if (device.AudioEndpointVolume.Channels.Count >= 2)
        {
            var leftSlider = new Slider(0, 100, device.AudioEndpointVolume.Channels[0].VolumeLevelScalar * 100)
                .Step(1)
                .OnValueChanged(v =>
                {
                    try { device.AudioEndpointVolume.Channels[0].VolumeLevelScalar = (float)(v / 100); }
                    catch { }
                });
            var rightSlider = new Slider(0, 100, device.AudioEndpointVolume.Channels[1].VolumeLevelScalar * 100)
                .Step(1)
                .OnValueChanged(v =>
                {
                    try { device.AudioEndpointVolume.Channels[1].VolumeLevelScalar = (float)(v / 100); }
                    catch { }
                });
            card.AddChild(Container.Row(gap: 8, padding: 0,
                new Text("L").FontSize(12),
                leftSlider
            ));
            card.AddChild(Container.Row(gap: 8, padding: 0,
                new Text("R").FontSize(12),
                rightSlider
            ));
        }

        card.AddChild(new Divider());

        // Sessions
        var sessionsCol = Container.Column(gap: 6, padding: 0);
        card.AddChild(sessionsCol);

        try
        {
            device.AudioSessionManager.RefreshSessions();
            var sessions = device.AudioSessionManager.Sessions;
            int count = sessions?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    var session = sessions![i];
                    if (session.IsSystemSoundsSession) continue;
                    var row = BuildSessionRow(session, device);
                    if (row != null) sessionsCol.AddChild(row);
                }
                catch { }
            }
        }
        catch { }

        // Subscribe to new session events
        try
        {
            device.AudioSessionManager.OnSessionCreated += (_, _) =>
                _uiQueue.Enqueue(() => { BuildDevices(); RouteAllSessions(); });
        }
        catch { }

        return card;
    }

    private static Widget? BuildSessionRow(AudioSessionControl session, MMDevice device)
    {
        uint pid;
        string processName;
        try
        {
            pid = session.GetProcessID;
            var proc = Process.GetProcessById((int)pid);
            processName = proc.ProcessName;
        }
        catch { return null; }

        var volSlider = new Slider(0, 100, session.SimpleAudioVolume.Volume * 100)
            .Step(1)
            .OnValueChanged(v =>
            {
                try { session.SimpleAudioVolume.Volume = (float)(v / 100); }
                catch { }
            });

        var entry = new SessionEntry(pid, session, device) { VolumeSlider = volSlider };
        _sessionMap[pid] = entry;

        // Also index by parent PID so window-process events (e.g. main browser)
        // resolve to this renderer subprocess session.
        var parentPid = WindowFinder.GetParentProcessId(pid);
        if (parentPid != 0 && parentPid != pid)
            _parentMap[parentPid] = entry;

        var row = Container.Row(gap: 8, padding: 0,
            new Text(processName).FontSize(13),
            new Spacer(),
            volSlider
        );
        row.Style = new LayoutStyle
        {
            FlexDirection = FlexDirection.Row,
            AlignItems = AlignItems.Center,
            Gap = 8,
        };
        return row;
    }

    // --- Session lookup ---

    private static SessionEntry? FindSession(uint pid) =>
        _sessionMap.TryGetValue(pid, out var e) ? e :
        _parentMap.TryGetValue(pid, out e) ? e : null;

    // --- Initial routing ---

    private static void RouteAllSessions()
    {
        var screenMap = ScreenToAudioDevice.Instance;

        foreach (var entry in _sessionMap.Values)
        {
            try
            {
                Process.GetProcessById((int)entry.Pid); // verify process still alive
                var hwnd = WindowFinder.FindVisibleWindow(entry.Pid);
                if (hwnd == IntPtr.Zero) continue;

                var screen = Screen.FromHandle(hwnd);
                if (!screenMap.TryGetDevice(screen, out var targetDevice) || targetDevice == null) continue;

                if (targetDevice.ID == entry.Device.ID) continue;

                entry.LastScreen = screen;
                SwitchProcessAudio(entry, targetDevice, entry.Pid);
                DebugLogStore.Add($"Initial route: pid={entry.Pid} -> {targetDevice.FriendlyName} (screen={screen.DeviceName})");
            }
            catch { }
        }
    }

    // --- Window monitor ---

    private static void StartMonitor()
    {
        if (_monitor != null) return;
        _monitor = new WindowMonitor();
        _monitor.ForegroundChanged += (_, e) =>
            _uiQueue.Enqueue(() => OnForegroundChanged(e.ProcessId));
        _monitor.MouseWheelScrolled += (_, e) =>
            _uiQueue.Enqueue(() => OnVolumeScroll(e.Delta));
        _monitor.ForegroundWindowMoved += (_, e) =>
            _uiQueue.Enqueue(() => OnWindowMoved(e.Hwnd, e.ProcessId));

        // Always track location changes so Win+Arrow snaps trigger routing,
        // not just drag-move-end events.
        _monitor.SetLocationChangeTracking(true);
    }

    private static void OnForegroundChanged(uint pid)
    {
        _foregroundPid = pid;

        var entry = FindSession(pid);
        if (entry == null)
        {
            _statusText.Content($"Foreground: PID {pid} (no audio session)");
            DebugLogStore.Add($"ForegroundChanged: pid={pid} (no session)");
            return;
        }

        // Route on focus: catches sessions that were already on the wrong device
        // at startup or that the window-move event missed.
        var hwnd = WindowFinder.FindVisibleWindow(pid);
        if (hwnd != IntPtr.Zero)
        {
            var screen = Screen.FromHandle(hwnd);
            var screenMap = ScreenToAudioDevice.Instance;
            if (screenMap.TryGetDevice(screen, out var targetDevice) && targetDevice != null
                && targetDevice.ID != entry.Device.ID)
            {
                SwitchProcessAudio(entry, targetDevice, pid);
                _statusText.Content($"Foreground: {entry.Device.FriendlyName} routed to {targetDevice.FriendlyName}");
                DebugLogStore.Add($"ForegroundChanged route: pid={pid}, screen={screen.DeviceName}, device={targetDevice.FriendlyName}");
                return;
            }

            _statusText.Content($"Foreground: PID {pid} on {screen.DeviceName}");
        }
        else
        {
            _statusText.Content($"Foreground: PID {pid}");
        }

        DebugLogStore.Add($"ForegroundChanged: pid={pid}");
    }

    private static void OnVolumeScroll(int delta)
    {
        var entry = FindSession(_foregroundPid);
        if (entry == null) return;

        try
        {
            var vol = entry.Session.SimpleAudioVolume.Volume;
            var newVol = Math.Clamp(vol + (delta > 0 ? 0.05f : -0.05f), 0f, 1f);
            entry.Session.SimpleAudioVolume.Volume = newVol;
            if (entry.VolumeSlider != null)
                entry.VolumeSlider.Value(newVol * 100);
            _statusText.Content($"PID {_foregroundPid} volume: {(int)(newVol * 100)}%");
        }
        catch { }
    }

    private static void OnWindowMoved(User32.NativeMethods.HWND hwnd, uint pid)
    {
        var entry = FindSession(pid);
        if (entry == null) return;

        var screen = Screen.FromHandle(hwnd);
        if (screen == null) return;

        bool screenChanged = entry.LastScreen == null || entry.LastScreen.DeviceName != screen.DeviceName;

        var screenMap = ScreenToAudioDevice.Instance;
        if (!screenMap.TryGetDevice(screen, out var targetDevice) || targetDevice == null)
        {
            DebugLogStore.Add($"No device mapping for screen {screen.DeviceName}.");
            return;
        }

        if (screenChanged)
        {
            entry.LastScreen = screen;
            SwitchProcessAudio(entry, targetDevice, pid);
        }

        if (AppSettings.EnableScreenPositionChannelBalance)
            ApplyChannelBalance(screen, targetDevice, hwnd, pid);
    }

    private static void SwitchProcessAudio(SessionEntry entry, MMDevice device, uint pid)
    {
        try
        {
            AudioSwitcher.Instance.SwitchProcessTo(device.ID, ERole.ERole_enum_count, EDataFlow.eRender, pid);
            _statusText.Content($"PID {pid} switched to {device.FriendlyName}");
            DebugLogStore.Add($"SwitchProcessTo: pid={pid}, device={device.FriendlyName}");

            // Brief mute/unmute forces stubborn apps to rebuild their audio stream.
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(150);
                    var vol = entry.Session.SimpleAudioVolume.Volume;
                    entry.Session.SimpleAudioVolume.Volume = 0f;
                    await Task.Delay(50);
                    entry.Session.SimpleAudioVolume.Volume = vol;
                }
                catch { }
            });
        }
        catch (Exception ex)
        {
            DebugLogStore.Add($"SwitchProcessTo failed for pid={pid}: {ex.Message}");
        }
    }

    private static void ApplyChannelBalance(Screen screen, MMDevice device, User32.NativeMethods.HWND hwnd, uint pid)
    {
        if (device.AudioEndpointVolume?.Channels.Count < 2) return;
        if (Screen.AllScreens.Length <= 1) return;

        if (!User32.NativeMethods.GetWindowRect(hwnd, out var rect)) return;

        var virtual_ = SystemInformation.VirtualScreen;
        if (virtual_.Width <= 0) return;

        var centerX = rect.Left + (rect.Right - rect.Left) / 2.0;
        var normalized = Math.Clamp((centerX - virtual_.Left) / virtual_.Width, 0.0, 1.0);
        var strength = AppSettings.ChannelBalanceStrength;
        var bias = (normalized - 0.5) * 2.0;
        var left = Math.Clamp(50.0 - bias * strength, 0, 100);
        var right = Math.Clamp(50.0 + bias * strength, 0, 100);

        try
        {
            device.AudioEndpointVolume.Channels[0].VolumeLevelScalar = (float)(left / 100.0);
            device.AudioEndpointVolume.Channels[1].VolumeLevelScalar = (float)(right / 100.0);
            device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)(Math.Max(left, right) / 100.0);
            DebugLogStore.Add($"Channel balance: pid={pid}, L={left:F0}, R={right:F0}");
        }
        catch { }
    }
}
