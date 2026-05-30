using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;
using NAudio.CoreAudioApi;
using ScreenSoundSwitch.WinUI.Data;
using SoundSwitch.Audio.Manager;
using System;
using System.Diagnostics;
using System.Linq;
using Windows.Storage;
using System.Windows.Forms;

namespace ScreenSoundSwitch.WinUI.Views
{
    public sealed partial class VolumePage : Page
    {
        private MMDeviceCollection previousDevices;
        private WindowMonitor windowMonitor;
        private ProcessControl foregroundProcessControl;
        private ScreenToAudioDevice screenToAudioDevice;
        private DateTime _lastChannelBalanceApplyUtc = DateTime.MinValue;
        private const int ChannelBalanceThrottleMs = 80;
        private bool _enableLocationChangeTracking;
        private bool _isDisposed;

        public VolumePage()
        {
            this.InitializeComponent();
            screenToAudioDevice = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<ScreenToAudioDevice>(App.Current.Services);
            windowMonitor = new WindowMonitor();
            windowMonitor.ForegroundChanged += WindowMonitor_ForegroundChanged;
            windowMonitor.MouseWheelScrolled += WindowMonitor_KeyIsDown;
            windowMonitor.ForegroundWindowMoved += WindowMonitor_ForegroundMoved;

            var localSettings = ApplicationData.Current.LocalSettings;
            _enableLocationChangeTracking = localSettings.Values["EnableScreenPositionChannelBalance"] is bool enabled && enabled;
            windowMonitor.SetLocationChangeTracking(_enableLocationChangeTracking);
            ChannelBalanceState.SetEnabled(_enableLocationChangeTracking);
            ChannelBalanceState.EnabledChanged += ChannelBalanceState_EnabledChanged;

            if (App.m_window != null)
                App.m_window.Closed += MainWindow_Closed;

            DebugLogStore.Add("VolumePage initialized and window monitor started.");
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            if (_isDisposed) return;
            _isDisposed = true;

            ChannelBalanceState.EnabledChanged -= ChannelBalanceState_EnabledChanged;

            if (windowMonitor != null)
            {
                windowMonitor.ForegroundChanged -= WindowMonitor_ForegroundChanged;
                windowMonitor.MouseWheelScrolled -= WindowMonitor_KeyIsDown;
                windowMonitor.ForegroundWindowMoved -= WindowMonitor_ForegroundMoved;
                windowMonitor.Stop();
            }

            if (App.m_window != null)
                App.m_window.Closed -= MainWindow_Closed;
        }

        private void ChannelBalanceState_EnabledChanged(bool enabled)
        {
            _enableLocationChangeTracking = enabled;
            windowMonitor?.SetLocationChangeTracking(enabled);
            DebugLogStore.Add($"Channel-balance location tracking {(enabled ? "enabled" : "disabled")}.");
        }

        private void WindowMonitor_ForegroundMoved(object sender, WindowMonitor.Event e)
        {
            if (_isDisposed || DispatcherQueue == null) return;

            DebugLogStore.Add($"ForegroundWindowMoved: pid={e.ProcessId}, hwnd={e.Hwnd}");
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_isDisposed) return;

                var now = DateTime.UtcNow;
                if ((now - _lastChannelBalanceApplyUtc).TotalMilliseconds < ChannelBalanceThrottleMs)
                    return;

                _lastChannelBalanceApplyUtc = now;
                ForegroundMovedHandle(e.Hwnd, e.ProcessId);
            });
        }

        private void WindowMonitor_ForegroundChanged(object sender, WindowMonitor.Event e)
        {
            if (_isDisposed || DispatcherQueue == null) return;

            DebugLogStore.Add($"ForegroundChanged: pid={e.ProcessId}");
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_isDisposed) return;
                UpdateForegroundProcess(e.ProcessId);
            });
        }

        private void WindowMonitor_KeyIsDown(object sender, WindowMonitor.MouseWheelEventArgs e)
        {
            if (_isDisposed || DispatcherQueue == null) return;

            DispatcherQueue.TryEnqueue(() =>
            {
                if (_isDisposed) return;
                UpdateForegroundVolume(e.Delta);
            });
        }

        public void UpdateForegroundProcess(uint processId)
        {
            foreach (var audioDeviceControl in DevicesStackPanel.Children.OfType<AudioDeviceControl>())
            {
                foreach (var processControl in audioDeviceControl._ProcessStackPanel.Children.OfType<ProcessControl>())
                {
                    if (processId == processControl.ProcessId)
                        foregroundProcessControl = processControl;
                }
            }
        }

        public void UpdateForegroundVolume(int delta)
        {
            if (foregroundProcessControl == null)
            {
                VolumeStatusInfoBar.Severity = InfoBarSeverity.Informational;
                VolumeStatusInfoBar.Message = "No audio session found for the foreground process.";
                DebugLogStore.Add("Foreground process session not found for volume adjustment.");
                return;
            }

            if (delta > 0)
            {
                foregroundProcessControl.ChangeSimpleVolumeLevel(0.05f);
                VolumeStatusInfoBar.Severity = InfoBarSeverity.Success;
                VolumeStatusInfoBar.Message = $"Volume increased for process {foregroundProcessControl.ProcessId}.";
                DebugLogStore.Add($"Process {foregroundProcessControl.ProcessId} volume increased.");
            }
            else
            {
                foregroundProcessControl.ChangeSimpleVolumeLevel(-0.05f);
                VolumeStatusInfoBar.Severity = InfoBarSeverity.Success;
                VolumeStatusInfoBar.Message = $"Volume decreased for process {foregroundProcessControl.ProcessId}.";
                DebugLogStore.Add($"Process {foregroundProcessControl.ProcessId} volume decreased.");
            }
        }

        public void ForegroundMovedHandle(IntPtr hwnd, uint processId)
        {
            Debug.WriteLine($"ForegroundMovedHandle: hwnd={hwnd}, pid={processId}");
            if (hwnd == IntPtr.Zero) return;

            ProcessControl targetProcessControl = null;
            foreach (var audioDeviceControl in DevicesStackPanel.Children.OfType<AudioDeviceControl>())
            {
                foreach (var processControl in audioDeviceControl._ProcessStackPanel.Children.OfType<ProcessControl>())
                {
                    if (processId == processControl.ProcessId)
                    {
                        targetProcessControl = processControl;
                        break;
                    }
                }
                if (targetProcessControl != null) break;
            }

            if (targetProcessControl == null)
            {
                Debug.WriteLine($"No ProcessControl found for pid={processId}");
                VolumeStatusInfoBar.Severity = InfoBarSeverity.Warning;
                VolumeStatusInfoBar.Message = $"Process {processId} has no switchable audio session.";
                DebugLogStore.Add($"Process {processId} has no switchable audio session.");
                return;
            }

            DebugLogStore.Add($"Matched ProcessControl for pid={processId}.");

            Screen screen = Screen.FromHandle(hwnd);
            if (screen == null)
            {
                DebugLogStore.Add($"Failed to resolve screen from hwnd={hwnd} for pid={processId}.");
                return;
            }

            DebugLogStore.Add($"Resolved screen for pid={processId}: {screen.DeviceName}");

            var localSettings = ApplicationData.Current.LocalSettings;
            var channelBalanceEnabled = localSettings.Values["EnableScreenPositionChannelBalance"] is bool enabled && enabled;
            var screenChanged = targetProcessControl.IsScreenChange(screen);

            if (!screenChanged && !channelBalanceEnabled)
            {
                DebugLogStore.Add($"Screen unchanged for pid={processId}, skip switching.");
                return;
            }

            if (screenToAudioDevice.TryGetDevice(screen, out var targetDevice))
            {
                if (screenChanged)
                {
                    targetProcessControl.ChangeAudioDevice(targetDevice);
                    DebugLogStore.Add($"SwitchProcessTo requested: pid={processId}, device={targetDevice.FriendlyName}, screen={screen.DeviceName}");
                }

                if (channelBalanceEnabled)
                {
                    targetProcessControl.ApplyScreenPositionChannelBalance(screen, targetDevice, hwnd);
                    UpdateDeviceChannelSliders(targetDevice);
                }
                else
                {
                    DebugLogStore.Add($"Channel balance disabled for pid={processId}.");
                }

                VolumeStatusInfoBar.Severity = InfoBarSeverity.Success;
                VolumeStatusInfoBar.Message = screenChanged
                    ? $"Process {processId} switched to device for screen {screen.DeviceName}."
                    : $"Process {processId} channel balance updated for screen {screen.DeviceName}.";
                DebugLogStore.Add(screenChanged
                    ? $"Process {processId} switched to device mapped for {screen.DeviceName}."
                    : $"Process {processId} channel balance updated by window position on {screen.DeviceName}.");
            }
            else
            {
                VolumeStatusInfoBar.Severity = InfoBarSeverity.Warning;
                VolumeStatusInfoBar.Message = $"Screen {screen.DeviceName} has no audio device binding.";
                DebugLogStore.Add($"No playback device mapping found for screen {screen.DeviceName}.");
            }
        }

        private void UpdateDeviceChannelSliders(MMDevice targetDevice)
        {
            foreach (var audioDeviceControl in DevicesStackPanel.Children.OfType<AudioDeviceControl>())
            {
                if (audioDeviceControl.DeviceId == targetDevice.ID)
                {
                    audioDeviceControl.UpdateChannelSlidersFromDevice();
                    break;
                }
            }
        }

        private void UpdateDevices()
        {
            var audioDeviceManager = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<AudioDeviceManager>(App.Current.Services);
            var currentDevices = audioDeviceManager.Devices;

            if (previousDevices != null && previousDevices.Count == currentDevices.Count &&
                !previousDevices.Where((t, i) => !t.ID.Equals(currentDevices[i].ID)).Any())
            {
                return;
            }

            previousDevices = currentDevices;
            DevicesStackPanel.Children.Clear();

            foreach (var device in currentDevices)
                DevicesStackPanel.Children.Add(new AudioDeviceControl(device));

            VolumeStatusInfoBar.Severity = InfoBarSeverity.Informational;
            VolumeStatusInfoBar.Message = $"Loaded {currentDevices.Count} audio device(s).";
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            UpdateDevices();
        }
    }
}
