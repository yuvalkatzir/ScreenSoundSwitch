using Serilog;
using SoundSwitch.Audio.Manager.Interop.Com.Threading;
using SoundSwitch.Audio.Manager.Interop.Com.User;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static SoundSwitch.Audio.Manager.Interop.Com.User.User32.NativeMethods;

namespace SoundSwitch.Audio.Manager
{
    public class WindowMonitor
    {
        public class Event : EventArgs
        {
            public uint ProcessId { get; }
            public string ProcessName { get; }
            public string WindowName { get; }
            public string WindowClass { get; }
            public User32.NativeMethods.HWND Hwnd { get; }

            public Event(uint processId, string processName, string windowName, string windowClass, User32.NativeMethods.HWND hwnd)
            {
                ProcessId = processId;
                ProcessName = processName;
                WindowName = windowName;
                WindowClass = windowClass;
                Hwnd = hwnd;
            }

            public override string ToString() =>
                $"{nameof(ProcessId)}: {ProcessId}, {nameof(ProcessName)}: {ProcessName}, {nameof(WindowName)}: {WindowName}, {nameof(WindowClass)}: {WindowClass}";
        }

        public class MouseWheelEventArgs : EventArgs
        {
            public int Delta { get; }
            public MouseWheelEventArgs(int delta) { Delta = delta; }
        }

        public event EventHandler<Event> ForegroundChanged;
        public event EventHandler<Event> ForegroundWindowMoved;
        public event EventHandler<MouseWheelEventArgs> MouseWheelScrolled;

        private readonly User32.NativeMethods.WinEventDelegate _foregroundWindowChanged;
        private readonly User32.NativeMethods.WinEventDelegate _foregroundWindowMoved;
        private IntPtr _foregroundWindowMoveEndHook = IntPtr.Zero;
        private IntPtr _foregroundWindowLocationHook = IntPtr.Zero;
        private User32.NativeMethods.HWND _foregroundWindow = User32.NativeMethods.HWND.NULL;
        private User32.NativeMethods.RECT _lastForegroundWindowRect;
        private bool _hasLastForegroundWindowRect;
        private readonly User32.NativeMethods.HookProc _mouseProc;
        private IntPtr _mouseHookID = IntPtr.Zero;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public WindowMonitor()
        {
            _foregroundWindowChanged = (hook, type, hwnd, idObject, child, thread, time) =>
            {
                if (idObject != User32.NativeMethods.OBJID_WINDOW)
                    return;

                if (hwnd == IntPtr.Zero)
                    return;

                var (processId, windowText, windowClass) = ProcessWindowInformation(hwnd);

                if (processId == 0) return;

                if (processId == Environment.ProcessId)
                {
                    Log.Information("Foreground window is this app, skipping.");
                    return;
                }

                _foregroundWindow = GetRootWindow(hwnd);
                UpdateLastForegroundWindowRect(_foregroundWindow);

                Task.Factory.StartNew(() =>
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                        return;
                    try
                    {
                        var process = Process.GetProcessById((int)processId);
                        var processName = process.ProcessName;
                        ForegroundChanged?.Invoke(this, new Event(processId, processName, windowText, windowClass, hwnd));
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to raise ForegroundChanged for pid={ProcessId}", processId);
                    }
                }, _cancellationTokenSource.Token);
            };

            _foregroundWindowMoved = (hook, type, hwnd, idObject, child, thread, time) =>
            {
                if (idObject != User32.NativeMethods.OBJID_WINDOW)
                    return;

                if (child != User32.NativeMethods.CHILDID_SELF)
                    return;

                if (hwnd == IntPtr.Zero)
                    return;

                var rootWindow = GetRootWindow(hwnd);
                if (rootWindow == User32.NativeMethods.HWND.NULL)
                    return;

                if (type == User32.NativeMethods.EVENT_OBJECT_LOCATIONCHANGE)
                {
                    if (_foregroundWindow == User32.NativeMethods.HWND.NULL)
                        _foregroundWindow = GetRootWindow(User32.NativeMethods.GetForegroundWindow());

                    if (rootWindow != _foregroundWindow)
                        return;

                    if (!HasRootWindowRectChanged(rootWindow))
                        return;
                }
                else
                {
                    UpdateLastForegroundWindowRect(rootWindow);
                }

                var (processId, windowText, windowClass) = ProcessWindowInformation(rootWindow);

                if (processId == 0) return;

                if (processId == Environment.ProcessId)
                {
                    Log.Information("Moved window is this app, skipping.");
                    return;
                }

                Task.Factory.StartNew(() =>
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                        return;

                    try
                    {
                        var process = Process.GetProcessById((int)processId);
                        var processName = process.ProcessName;
                        ForegroundWindowMoved?.Invoke(this, new Event(processId, processName, windowText, windowClass, rootWindow));
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to raise ForegroundWindowMoved for pid={ProcessId}", processId);
                    }
                }, _cancellationTokenSource.Token);
            };

            ComThread.Invoke(() =>
            {
                _foregroundWindowMoveEndHook = User32.NativeMethods.SetWinEventHook(
                    User32.NativeMethods.EVENT_SYSTEM_MOVESIZEEND,
                    User32.NativeMethods.EVENT_SYSTEM_MOVESIZEEND,
                    IntPtr.Zero, _foregroundWindowMoved, 0, 0,
                    User32.NativeMethods.WINEVENT_OUTOFCONTEXT);
            });

            ComThread.Invoke(() =>
            {
                User32.NativeMethods.SetWinEventHook(
                    User32.NativeMethods.EVENT_SYSTEM_MINIMIZEEND,
                    User32.NativeMethods.EVENT_SYSTEM_MINIMIZEEND,
                    IntPtr.Zero, _foregroundWindowChanged, 0, 0,
                    User32.NativeMethods.WINEVENT_OUTOFCONTEXT);

                User32.NativeMethods.SetWinEventHook(
                    User32.NativeMethods.EVENT_SYSTEM_FOREGROUND,
                    User32.NativeMethods.EVENT_SYSTEM_FOREGROUND,
                    IntPtr.Zero, _foregroundWindowChanged, 0, 0,
                    User32.NativeMethods.WINEVENT_OUTOFCONTEXT);
            });

            _mouseProc = HookCallbackMouse;
            ComThread.Invoke(() =>
            {
                _mouseHookID = SetHook(_mouseProc, WH_MOUSE_LL);
            });
        }

        private IntPtr SetHook(HookProc proc, int evenType)
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            return SetWindowsHookEx(evenType, proc, GetModuleHandle(curModule.ModuleName), 0);
        }

        public void SetLocationChangeTracking(bool enabled)
        {
            ComThread.Invoke(() =>
            {
                if (enabled)
                {
                    if (_foregroundWindowLocationHook != IntPtr.Zero)
                        return;

                    _foregroundWindow = GetRootWindow(User32.NativeMethods.GetForegroundWindow());
                    UpdateLastForegroundWindowRect(_foregroundWindow);
                    _foregroundWindowLocationHook = User32.NativeMethods.SetWinEventHook(
                        User32.NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
                        User32.NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
                        IntPtr.Zero, _foregroundWindowMoved, 0, 0,
                        User32.NativeMethods.WINEVENT_OUTOFCONTEXT);
                }
                else if (_foregroundWindowLocationHook != IntPtr.Zero)
                {
                    User32.NativeMethods.UnhookWinEvent(_foregroundWindowLocationHook);
                    _foregroundWindowLocationHook = IntPtr.Zero;
                }
            });
        }

        private static User32.NativeMethods.HWND GetRootWindow(User32.NativeMethods.HWND hwnd)
        {
            if (hwnd == User32.NativeMethods.HWND.NULL)
                return User32.NativeMethods.HWND.NULL;

            var rootWindow = User32.NativeMethods.GetAncestor(hwnd, User32.NativeMethods.GA_ROOT);
            return rootWindow == User32.NativeMethods.HWND.NULL ? hwnd : rootWindow;
        }

        private bool HasRootWindowRectChanged(User32.NativeMethods.HWND hwnd)
        {
            if (!User32.NativeMethods.GetWindowRect(hwnd, out var currentRect))
            {
                // Some windows cannot reliably return a rect; return true to avoid missing position-change events.
                return true;
            }

            if (_hasLastForegroundWindowRect && currentRect.Equals(_lastForegroundWindowRect))
                return false;

            _lastForegroundWindowRect = currentRect;
            _hasLastForegroundWindowRect = true;
            return true;
        }

        private void UpdateLastForegroundWindowRect(User32.NativeMethods.HWND hwnd)
        {
            if (hwnd == User32.NativeMethods.HWND.NULL)
            {
                _hasLastForegroundWindowRect = false;
                return;
            }

            if (User32.NativeMethods.GetWindowRect(hwnd, out var currentRect))
            {
                _lastForegroundWindowRect = currentRect;
                _hasLastForegroundWindowRect = true;
            }
            else
            {
                _hasLastForegroundWindowRect = false;
            }
        }

        private IntPtr HookCallbackMouse(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_MOUSEWHEEL)
            {
                MSLLHOOKSTRUCT mouseHookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                int delta = (short)(mouseHookStruct.mouseData >> 16);
                if ((GetKeyState(VK_CONTROL) & 0x8000) != 0 && (GetKeyState(VK_MENU) & 0x8000) != 0)
                {
                    Task.Factory.StartNew(() =>
                    {
                        if (_cancellationTokenSource.Token.IsCancellationRequested)
                            return;
                        try
                        {
                            MouseWheelScrolled?.Invoke(this, new MouseWheelEventArgs(delta));
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "MouseWheelScrolled handler threw an exception");
                        }
                    }, _cancellationTokenSource.Token);
                }
            }
            return CallNextHookEx(_mouseHookID, nCode, wParam, lParam);
        }

        public static (uint ProcessId, string WindowText, string WindowClass) ProcessWindowInformation(User32.NativeMethods.HWND hwnd)
        {
            return ComThread.Invoke(() =>
            {
                uint processId = 0;
                var wndText = "";
                var wndClass = "";
                try { wndText = User32.GetWindowText(hwnd); } catch (Exception ex) { Log.Warning(ex, "Failed to get window text"); }
                try { wndClass = User32.GetWindowClass(hwnd); } catch (Exception ex) { Log.Warning(ex, "Failed to get window class"); }
                try { User32.NativeMethods.GetWindowThreadProcessId(hwnd, out processId); } catch (Exception ex) { Log.Warning(ex, "Failed to get window thread process id"); }
                return (processId, wndText, wndClass);
            });
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();

            ComThread.Invoke(() =>
            {
                if (_foregroundWindowMoveEndHook != IntPtr.Zero)
                {
                    User32.NativeMethods.UnhookWinEvent(_foregroundWindowMoveEndHook);
                    _foregroundWindowMoveEndHook = IntPtr.Zero;
                }

                if (_foregroundWindowLocationHook != IntPtr.Zero)
                {
                    User32.NativeMethods.UnhookWinEvent(_foregroundWindowLocationHook);
                    _foregroundWindowLocationHook = IntPtr.Zero;
                }
            });
        }

        public void Dispose() => Stop();
    }
}
