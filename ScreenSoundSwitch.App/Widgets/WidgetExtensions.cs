using Crescendo.Core.Widgets;

namespace ScreenSoundSwitch.App.Widgets;

public static class WidgetExtensions
{
    public static void ClearChildren(this Widget widget)
    {
        while (widget.Children.Count > 0)
            widget.RemoveChild(widget.Children[0]);
    }
}
