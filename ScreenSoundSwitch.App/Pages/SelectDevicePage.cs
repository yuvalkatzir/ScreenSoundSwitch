using Crescendo.Core.Layout;
using Crescendo.Core.Widgets;
using Button = Crescendo.Core.Widgets.Button;
using NAudio.CoreAudioApi;
using ScreenSoundSwitch.App.Data;
using ScreenSoundSwitch.App.Widgets;
using SkiaSharp;
using System.Linq;
using System.Windows.Forms;

namespace ScreenSoundSwitch.App.Pages;

public static class SelectDevicePage
{
    private const float CanvasWidth = 700;
    private const float CanvasHeight = 200;

    public static Widget Build()
    {
        var screenMap = ScreenToAudioDevice.Instance;
        var audioManager = AudioDeviceManager.Instance;
        var screens = Screen.AllScreens;

        Screen? selectedScreen = null;
        Container canvas = null!;
        Container bindRow = null!;
        Text statusText = new Text("Select a display to bind it to a playback device.").FontSize(13);
        Dropdown deviceDropdown = null!;

        var devices = audioManager.Devices.Cast<MMDevice>().ToList();
        var deviceNames = devices.Select(d => d.FriendlyName).ToArray();

        void RebuildCanvas()
        {
            canvas.ClearChildren();
            if (screens.Length == 0) return;

            var minX = screens.Min(s => s.Bounds.X);
            var minY = screens.Min(s => s.Bounds.Y);
            var totalW = screens.Max(s => s.Bounds.X + s.Bounds.Width) - minX;
            var totalH = screens.Max(s => s.Bounds.Y + s.Bounds.Height) - minY;
            float scale = Math.Min(CanvasWidth / totalW, CanvasHeight / totalH);

            foreach (var screen in screens)
            {
                float x = (screen.Bounds.X - minX) * scale;
                float y = (screen.Bounds.Y - minY) * scale;
                float w = screen.Bounds.Width * scale;
                float h = screen.Bounds.Height * scale;

                bool isSelected = selectedScreen?.DeviceName == screen.DeviceName;
                string shortName = screen.DeviceName.Split('\\', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? screen.DeviceName;
                string boundName = screenMap.TryGetDevice(screen, out var bound) && bound != null
                    ? bound.FriendlyName
                    : "no device";

                var cardBg = isSelected ? new SKColor(0x33, 0x99, 0xFF, 60) : new SKColor(255, 255, 255, 10);

                var inner = new Card().BackgroundColor(cardBg);
                inner.Style = new LayoutStyle
                {
                    FlexDirection = FlexDirection.Column,
                    AlignItems = AlignItems.Center,
                    JustifyContent = JustifyContent.Center,
                    Padding = new Edges(6),
                    Gap = 4,
                    FlexGrow = 1,
                };
                inner.AddChild(new Text(shortName).FontSize(11).Bold());
                inner.AddChild(new Text(boundName).FontSize(10));
                if (screen.Primary)
                    inner.AddChild(new Badge("Primary").Color(new SKColor(0x33, 0x99, 0xFF)));

                var transparent = new SKColor(0, 0, 0, 0);
                var btn = new Button("").OnClick(() =>
                {
                    selectedScreen = screen;
                    if (screenMap.TryGetDevice(screen, out var current) && current != null)
                    {
                        var idx = devices.FindIndex(d => d.ID == current.ID);
                        deviceDropdown.SelectedIndex(idx);
                        statusText.Content($"Selected: {screen.DeviceName} - bound to {current.FriendlyName}");
                    }
                    else
                    {
                        deviceDropdown.SelectedIndex(-1);
                        statusText.Content($"Selected: {screen.DeviceName} - choose a playback device below");
                    }
                    RebuildCanvas();
                });
                btn.BackgroundColor(transparent, transparent, transparent);

                btn.Style = new LayoutStyle
                {
                    PositionType = PositionType.Absolute,
                    Left = x,
                    Top = y,
                    Width = w,
                    Height = h,
                    Padding = new Edges(0),
                };
                btn.AddChild(inner);
                canvas.AddChild(btn);
            }

            bindRow.Disabled(selectedScreen == null);
        }

        deviceDropdown = new Dropdown(deviceNames)
            .OnSelected((idx, name) =>
            {
                if (selectedScreen == null) { statusText.Content("Select a display first."); return; }
                var device = devices.ElementAtOrDefault(idx);
                if (device == null) return;
                screenMap.SetDevice(selectedScreen, device);
                statusText.Content($"{selectedScreen.DeviceName} bound to: {device.FriendlyName}");
                RebuildCanvas();
            });
        deviceDropdown.Style = new LayoutStyle { FlexGrow = 1 };

        canvas = new Container();
        canvas.Style = new LayoutStyle { Width = CanvasWidth, Height = CanvasHeight };

        bindRow = Container.Row(gap: 12, padding: 0,
            new Text("Playback device:").FontSize(13),
            deviceDropdown
        );
        bindRow.Style = new LayoutStyle
        {
            FlexDirection = FlexDirection.Row,
            AlignItems = AlignItems.Center,
            Gap = 12,
        };

        RebuildCanvas();

        var canvasCard = new Card().Elevation(2);
        canvasCard.Style = new LayoutStyle { Padding = new Edges(12), AlignSelf = AlignSelf.Center };
        canvasCard.AddChild(canvas);

        var page = Container.Column(gap: 16, padding: 24,
            new Text("Bind Displays to Audio Devices").FontSize(22).Bold(),
            new Text("Click a display, then pick a playback device below.").FontSize(13),
            canvasCard,
            statusText,
            bindRow
        );

        return new ScrollView(page);
    }
}
