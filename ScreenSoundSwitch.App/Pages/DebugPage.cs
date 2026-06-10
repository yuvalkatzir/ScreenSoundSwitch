using Crescendo.Core.Layout;
using Crescendo.Core.Widgets;
using Button = Crescendo.Core.Widgets.Button;
using ScreenSoundSwitch.App.Data;
using System.Collections.Generic;

namespace ScreenSoundSwitch.App.Pages;

public static class DebugPage
{
    public static Widget Build()
    {
        var logs = new List<string>(DebugLogStore.Logs);
        VirtualizedListView<string>? listRef = null;

        var countLabel = new Text($"{logs.Count} entries").FontSize(12);

        var list = new VirtualizedListView<string>(logs, (entry, _) =>
        {
            var row = new Container();
            row.Style = new LayoutStyle
            {
                Padding = new Edges(8, 4),
                FlexDirection = FlexDirection.Row,
            };
            row.AddChild(new Text(entry).FontSize(11));
            return row;
        })
        .ItemHeightFunc(_ => 24f);

        listRef = list;
        list.Style = new LayoutStyle { FlexGrow = 1 };

        DebugLogStore.LogAdded += msg =>
        {
            logs.Add(msg);
            countLabel.Content($"{logs.Count} entries");
            listRef?.Refresh();
        };

        DebugLogStore.LogsCleared += () =>
        {
            logs.Clear();
            countLabel.Content("0 entries");
            listRef?.Refresh();
        };

        var clearBtn = new Button("Clear").OnClick(() => DebugLogStore.Clear());

        var header = Container.Row(gap: 12, padding: 0,
            new Text("Debug Log").FontSize(22).Bold(),
            new Spacer(),
            countLabel,
            clearBtn
        );
        header.Style = new LayoutStyle
        {
            FlexDirection = FlexDirection.Row,
            AlignItems = AlignItems.Center,
            Gap = 12,
        };

        var logDirText = new Text($"Log directory: {DebugLogStore.LogDirectory}").FontSize(11);

        var page = new Container();
        page.Style = new LayoutStyle
        {
            FlexDirection = FlexDirection.Column,
            Padding = new Edges(24),
            Gap = 12,
            FlexGrow = 1,
        };
        page.AddChild(header);
        page.AddChild(logDirText);
        page.AddChild(new Divider());
        page.AddChild(list);

        return page;
    }
}
