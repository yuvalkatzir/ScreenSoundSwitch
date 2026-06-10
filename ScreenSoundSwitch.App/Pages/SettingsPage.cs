using Crescendo.Core.Layout;
using Crescendo.Core.Widgets;
using ScreenSoundSwitch.App.Data;

namespace ScreenSoundSwitch.App.Pages;

public static class SettingsPage
{
    public static Widget Build()
    {
        var statusText = new Text("").FontSize(13);

        void SetStatus(string msg) => statusText.Content(msg);

        // Auto-start
        var autoStartToggle = new Toggle(AppSettings.EnableAutoStart)
            .OnChanged(v =>
            {
                AppSettings.EnableAutoStart = v;
                SetStatus(v ? "Auto-start enabled." : "Auto-start disabled.");
            });

        // Tray icon
        var trayToggle = new Toggle(AppSettings.EnableTrayIcon)
            .OnChanged(v =>
            {
                AppSettings.EnableTrayIcon = v;
                SetStatus(v ? "Tray icon enabled." : "Tray icon disabled.");
            });

        // Auto-restore config
        var restoreToggle = new Toggle(AppSettings.EnableAutoRestoreConfig)
            .OnChanged(v =>
            {
                AppSettings.EnableAutoRestoreConfig = v;
                SetStatus(v ? "Config auto-restore enabled." : "Config auto-restore disabled.");
            });

        // Channel balance
        var balanceToggle = new Toggle(AppSettings.EnableScreenPositionChannelBalance)
            .OnChanged(v =>
            {
                AppSettings.EnableScreenPositionChannelBalance = v;
                SetStatus(v ? "Screen-position channel balance enabled." : "Screen-position channel balance disabled.");
            });

        // Channel balance strength
        var strengthLabel = new Text($"{(int)AppSettings.ChannelBalanceStrength}").FontSize(13);
        var strengthSlider = new Slider(0, 50, (float)AppSettings.ChannelBalanceStrength)
            .Step(1)
            .OnValueChanged(v =>
            {
                AppSettings.ChannelBalanceStrength = v;
                strengthLabel.Content($"{(int)v}");
            });
        strengthSlider.Style = new LayoutStyle { FlexGrow = 1 };

        // Debug page
        var debugToggle = new Toggle(AppSettings.EnableDebugPage)
            .OnChanged(v =>
            {
                AppSettings.EnableDebugPage = v;
                SetStatus(v ? "Debug page enabled." : "Debug page disabled.");
            });

        Widget SettingRow(string label, Widget control, string? hint = null)
        {
            var col = Container.Column(gap: 2, padding: 0,
                new Text(label).FontSize(14).Bold(),
                hint != null ? new Text(hint).FontSize(12) : new Spacer()
            );
            col.Style = new LayoutStyle { FlexGrow = 1, FlexDirection = FlexDirection.Column };
            var row = new Container();
            row.Style = new LayoutStyle
            {
                FlexDirection = FlexDirection.Row,
                AlignItems = AlignItems.Center,
                Gap = 16,
                Padding = new Edges(0, 4),
            };
            row.AddChild(col);
            row.AddChild(control);
            return row;
        }

        var startupCard = new Card();
        startupCard.Style = new LayoutStyle { FlexDirection = FlexDirection.Column, Padding = new Edges(16), Gap = 12 };
        startupCard.AddChild(new Text("Startup").FontSize(18).Bold());
        startupCard.AddChild(SettingRow("Launch on login", autoStartToggle, "Add to Windows startup via registry."));
        startupCard.AddChild(SettingRow("Start minimized to tray", trayToggle, "Hide window on close; reopen from tray."));
        startupCard.AddChild(SettingRow("Restore last bindings on launch", restoreToggle, "Navigate directly to Volume page if bindings exist."));

        var balanceCard = new Card();
        balanceCard.Style = new LayoutStyle { FlexDirection = FlexDirection.Column, Padding = new Edges(16), Gap = 12 };
        balanceCard.AddChild(new Text("Channel Balance").FontSize(18).Bold());
        balanceCard.AddChild(SettingRow("Enable screen-position balance", balanceToggle,
            "Shifts L/R audio as windows move between screens."));
        balanceCard.AddChild(Container.Row(gap: 8, padding: 0,
            new Text("Strength").FontSize(13),
            strengthSlider,
            strengthLabel
        ));

        var advancedCard = new Card();
        advancedCard.Style = new LayoutStyle { FlexDirection = FlexDirection.Column, Padding = new Edges(16), Gap = 12 };
        advancedCard.AddChild(new Text("Advanced").FontSize(18).Bold());
        advancedCard.AddChild(SettingRow("Show debug tab", debugToggle, "Reveals the Debug page in the tab bar."));

        var page = Container.Column(gap: 16, padding: 24,
            new Text("Settings").FontSize(22).Bold(),
            statusText,
            startupCard,
            balanceCard,
            advancedCard
        );

        return new ScrollView(page);
    }
}
