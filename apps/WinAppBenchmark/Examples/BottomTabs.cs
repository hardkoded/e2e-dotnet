// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static WinAppBenchmark.App.Support;

namespace WinAppBenchmark.Examples;

/// <summary>Tab navigation and acting inside a tab. The one scenario without the app header: its tab bar is its chrome, with a Back button where a mobile stack has its swipe back.</summary>
public sealed class BottomTabs : Component
{
    private static readonly string[] Tabs = ["Home", "Feed", "Actions"];

    public override Element Render()
    {
        var nav = UseNavigation<string>();
        var (tab, setTab) = UseState("Home");
        var (done, setDone) = UseState(false);

        Element content = tab switch
        {
            "Feed" => VStack(12, Title("Feed").AutomationId("feed-tab-content")),
            "Actions" => VStack(12, done
                ? Success("Action completed")
                : Button("Complete action", () => setDone(true)).AutomationId("complete-action-button")),
            _ => VStack(12,
                Title("Home").AutomationId("home-tab-content"),
                Hint("Go to the Actions tab and press the button there.")),
        };

        return Grid(
            columns: [GridSize.Star()],
            rows: [GridSize.Auto, GridSize.Star(), GridSize.Auto],
            Button("Back", () => nav.GoBack()).AutomationId("back").Margin(12, 8),
            content.Padding(24).Grid(row: 1),
            HStack(8, [.. Tabs.Select(name => TabButton(name, tab, setTab))])
                .Padding(12, 8)
                .Grid(row: 2));
    }

    private static Element TabButton(string name, string current, Action<string> select) =>
        Button(name == "Feed" ? "Feed (3)" : name, () => select(name))
            .AutomationName(name == "Feed" ? "Feed, 3 new items" : name)
            .AutomationId($"tab-{name.ToLowerInvariant()}")
            .IsEnabled(name != current)
            .Flex(grow: 1);
}
