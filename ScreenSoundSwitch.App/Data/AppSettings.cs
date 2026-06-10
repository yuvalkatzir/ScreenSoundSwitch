using Crescendo.Core.State;

namespace ScreenSoundSwitch.App.Data;

public static class AppSettings
{
    public static bool EnableAutoStart
    {
        get => DataStore.Get<bool>("EnableAutoStart", false);
        set { DataStore.Set("EnableAutoStart", value); StartupManager.SetEnabled(value); }
    }

    public static bool EnableTrayIcon
    {
        get => DataStore.Get<bool>("EnableTrayIcon", false);
        set => DataStore.Set("EnableTrayIcon", value);
    }

    public static bool EnableAutoRestoreConfig
    {
        get => DataStore.Get<bool>("EnableAutoRestoreConfig", false);
        set => DataStore.Set("EnableAutoRestoreConfig", value);
    }

    public static bool EnableScreenPositionChannelBalance
    {
        get => DataStore.Get<bool>("EnableScreenPositionChannelBalance", false);
        set => DataStore.Set("EnableScreenPositionChannelBalance", value);
    }

    public static double ChannelBalanceStrength
    {
        get => DataStore.Get<double>("ChannelBalanceStrength", 20.0);
        set => DataStore.Set("ChannelBalanceStrength", value);
    }

    public static bool EnableDebugPage
    {
        get => DataStore.Get<bool>("EnableDebugPage", false);
        set => DataStore.Set("EnableDebugPage", value);
    }
}
