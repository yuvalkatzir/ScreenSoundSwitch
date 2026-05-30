using CommunityToolkit.Mvvm.ComponentModel;
using NAudio.CoreAudioApi;
using ScreenSoundSwitch.WinUI.Data;
using ScreenSoundSwitch.WinUI.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Forms;

namespace ScreenSoundSwitch.WinUI.ViewModels
{
    public partial class ScreenViewModel : ObservableObject
    {
        private const double CanvasWidth = 650;
        private const double CanvasHeight = 300;

        [ObservableProperty]
        private ObservableCollection<ScreenControlModel> elements = new();
        [ObservableProperty]
        private MMDeviceCollection audioDevices;
        [ObservableProperty]
        private string statusMessage = "Select a display first";
        [ObservableProperty]
        private bool canSelectAudioDevice;
        [ObservableProperty]
        public partial Screen SelectedScreen { get; set; }
        [ObservableProperty]
        public partial MMDevice SelectedAudioDevice { get; set; }

        private ScreenToAudioDevice screenToAudioDevice;
        private AudioDeviceManager audioDeviceManager;

        public ScreenViewModel(AudioDeviceManager audioManager, ScreenToAudioDevice screenToDeviceMap)
        {
            audioDeviceManager = audioManager;
            audioDevices = audioDeviceManager.Devices;
            screenToAudioDevice = screenToDeviceMap;
            canSelectAudioDevice = false;
            InitializeElements();
        }

        public void InitializeElements()
        {
            Screen[] screens = Screen.AllScreens;
            if (screens.Length == 0) return;

            double minX = screens.Min(e => e.Bounds.X);
            double minY = screens.Min(e => e.Bounds.Y);
            double width = screens.Max(e => e.Bounds.X + e.Bounds.Width) - minX;
            double height = screens.Max(e => e.Bounds.Y + e.Bounds.Height) - minY;

            double scale = Math.Min(CanvasWidth / width, CanvasHeight / height);
            double centerX = width / 2 * scale;
            double centerY = height / 2 * scale;

            Elements.Clear();

            foreach (var screen in screens)
            {
                // Only set a default device if this screen has no saved binding.
                if (!screenToAudioDevice.ContainsScreen(screen))
                    screenToAudioDevice.SetDevice(screen, audioDeviceManager.GetDefaultAudioEndpoint());

                double x = (screen.Bounds.X - minX) * scale - centerX;
                double y = (screen.Bounds.Y - minY) * scale - centerY;

                var model = new ScreenControlModel(0, screen.Bounds)
                {
                    Name = screen.DeviceName,
                    DeviceNameText = screen.DeviceName,
                    Left = x + CanvasWidth / 2,
                    Top = y + CanvasHeight / 2,
                    Width = screen.Bounds.Width * scale,
                    Height = screen.Bounds.Height * scale,
                    IsSelected = screen.Primary,
                    Screen = screen,
                    Scale = scale,
                    ViewModel = this
                };

                Elements.Add(model);
            }
        }

        public void SelectScreen(Screen screen)
        {
            SelectedScreen = screen;
            CanSelectAudioDevice = true;

            foreach (var element in Elements)
                element.IsSelected = element.DeviceNameText == screen.DeviceName;

            if (screenToAudioDevice.TryGetDevice(screen, out var mappedDevice))
            {
                var deviceId = mappedDevice.ID;
                SelectedAudioDevice = audioDevices.FirstOrDefault(d => d.ID == deviceId) ?? mappedDevice;
                StatusMessage = $"Display selected: {screen.DeviceName}";
            }
            else
            {
                StatusMessage = $"Display selected: {screen.DeviceName} - choose a playback device";
            }
        }

        partial void OnSelectedAudioDeviceChanged(MMDevice value)
        {
            if (SelectedScreen == null || value == null) return;

            try
            {
                screenToAudioDevice.SetDevice(SelectedScreen, value);
                StatusMessage = $"{SelectedScreen.DeviceName} bound to: {value.FriendlyName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to bind playback device: {ex.Message}";
            }
        }

        public void AudioDeviceSelectionChanged(object sender) { }
    }
}
