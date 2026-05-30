using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using ScreenSoundSwitch.WinUI.Data;
using ScreenSoundSwitch.WinUI.ViewModels;
using System;
using System.Diagnostics;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;

namespace ScreenSoundSwitch.WinUI.Views
{
    public sealed partial class SettingPage : Page
    {
        private ApplicationDataContainer localSettings;
        private readonly SettingViewModel ViewModel;

        public SettingPage()
        {
            this.InitializeComponent();
            ViewModel = this.DataContext as SettingViewModel;
            Page_Loaded(this, null);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();

            if (localSettings.Values.ContainsKey("AudioFilePath"))
            {
                ViewModel.SetAudioFileFolder(StorageFolder.GetFolderFromPathAsync(localSettings.Values["AudioFilePath"].ToString()).AsTask().Result);
                AudioFolderPathTextBlock.Text = localSettings.Values["AudioFilePath"].ToString();
                SettingStatusInfoBar.Severity = InfoBarSeverity.Success;
                SettingStatusInfoBar.Message = $"Current folder: {AudioFolderPathTextBlock.Text}";
            }
            else
            {
                SettingStatusInfoBar.Severity = InfoBarSeverity.Informational;
                SettingStatusInfoBar.Message = "No audio file folder configured.";
            }

            EnableDebugToggleSwitch.IsOn = ViewModel.SettingModel.EnableDebugPage;

            var autoStartToggle = this.FindName("EnableAutoStartToggleSwitch") as ToggleSwitch;
            if (autoStartToggle != null)
            {
                autoStartToggle.IsOn = StartupManager.IsEnabled();
                ViewModel.SetEnableAutoStart(autoStartToggle.IsOn);
            }

            var trayToggle = this.FindName("EnableTrayIconToggleSwitch") as ToggleSwitch;
            if (trayToggle != null)
                trayToggle.IsOn = ViewModel.SettingModel.EnableTrayIcon;

            var autoRestoreToggle = this.FindName("EnableAutoRestoreConfigToggleSwitch") as ToggleSwitch;
            if (autoRestoreToggle != null)
                autoRestoreToggle.IsOn = ViewModel.SettingModel.EnableAutoRestoreConfig;

            var channelBalanceToggle = this.FindName("EnableScreenPositionChannelBalanceToggleSwitch") as ToggleSwitch;
            if (channelBalanceToggle != null)
                channelBalanceToggle.IsOn = ViewModel.SettingModel.EnableScreenPositionChannelBalance;

            var strengthSlider = this.FindName("ChannelBalanceStrengthSlider") as Slider;
            var strengthTextBlock = this.FindName("ChannelBalanceStrengthValueTextBlock") as TextBlock;
            if (strengthSlider != null)
            {
                strengthSlider.Value = ViewModel.SettingModel.ScreenPositionChannelBalanceStrength;
                if (strengthTextBlock != null)
                    strengthTextBlock.Text = ((int)strengthSlider.Value).ToString();
            }
        }

        private async void PickFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var senderButton = sender as Button;
            senderButton.IsEnabled = false;

            var openPicker = new FolderPicker();
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.m_window);
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);
            openPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            openPicker.FileTypeFilter.Add("*");

            StorageFolder folder = await openPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedAudioFolderToken", folder);
                ViewModel.SetAudioFileFolder(folder);
                AudioFolderPathTextBlock.Text = folder.Path;
            }

            senderButton.IsEnabled = true;
        }

        private void EnableDebugToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            var isEnabled = EnableDebugToggleSwitch.IsOn;
            ViewModel.SetEnableDebugPage(isEnabled);
            DebugPageState.SetEnabled(isEnabled);

            SettingStatusInfoBar.Severity = InfoBarSeverity.Informational;
            SettingStatusInfoBar.Message = isEnabled ? "Debug page enabled." : "Debug page disabled.";
        }

        private void EnableAutoStartToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = this.FindName("EnableAutoStartToggleSwitch") as ToggleSwitch;
            if (toggle == null) return;

            var isEnabled = toggle.IsOn;
            ViewModel.SetEnableAutoStart(isEnabled);
            StartupManager.SetEnabled(isEnabled);

            SettingStatusInfoBar.Severity = InfoBarSeverity.Informational;
            SettingStatusInfoBar.Message = isEnabled ? "Auto-start enabled." : "Auto-start disabled.";
        }

        private void EnableTrayIconToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = this.FindName("EnableTrayIconToggleSwitch") as ToggleSwitch;
            if (toggle == null) return;

            var isEnabled = toggle.IsOn;
            ViewModel.SetEnableTrayIcon(isEnabled);
            TrayIconState.SetEnabled(isEnabled);

            SettingStatusInfoBar.Severity = InfoBarSeverity.Informational;
            SettingStatusInfoBar.Message = isEnabled ? "Tray mode enabled." : "Tray mode disabled.";
        }

        private void EnableAutoRestoreConfigToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = this.FindName("EnableAutoRestoreConfigToggleSwitch") as ToggleSwitch;
            if (toggle == null) return;

            var isEnabled = toggle.IsOn;
            ViewModel.SetEnableAutoRestoreConfig(isEnabled);

            SettingStatusInfoBar.Severity = InfoBarSeverity.Informational;
            SettingStatusInfoBar.Message = isEnabled ? "Config auto-restore enabled." : "Config auto-restore disabled.";
        }

        private void EnableScreenPositionChannelBalanceToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            var channelBalanceToggle = this.FindName("EnableScreenPositionChannelBalanceToggleSwitch") as ToggleSwitch;
            if (channelBalanceToggle == null) return;

            var isEnabled = channelBalanceToggle.IsOn;
            ViewModel.SetEnableScreenPositionChannelBalance(isEnabled);
            ChannelBalanceState.SetEnabled(isEnabled);

            SettingStatusInfoBar.Severity = InfoBarSeverity.Informational;
            SettingStatusInfoBar.Message = isEnabled
                ? "Screen-position channel balance enabled."
                : "Screen-position channel balance disabled.";
        }

        private void ChannelBalanceStrengthSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            var rounded = Math.Round(e.NewValue);
            var strengthTextBlock = this.FindName("ChannelBalanceStrengthValueTextBlock") as TextBlock;
            if (strengthTextBlock != null)
                strengthTextBlock.Text = ((int)rounded).ToString();

            ViewModel.SetScreenPositionChannelBalanceStrength(rounded);
        }

        private void LoadSettings()
        {
            localSettings = ApplicationData.Current.LocalSettings;
            foreach (var key in localSettings.Values.Keys)
                Debug.WriteLine(key);
        }
    }
}
