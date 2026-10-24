// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.UI.Reactor.Navigation;

namespace WinAppBenchmark.App;

/// <summary>
/// The app shell, the port of upstream's <c>src/App.tsx</c>: a stack of routes named by scenario, a
/// header with the route's title (AutomationId "screen-title") and a Back button (AutomationId
/// "back") where the mobile app has the navigation bar, and the home list as the first screen.
/// </summary>
public sealed class BenchmarkApp : Component
{
    public override Element Render()
    {
        var nav = UseNavigation(Examples.HomeTitle);
        var route = nav.CurrentRoute;
        var headerShown = Examples.Find(route)?.HeaderShown ?? true;

        return Grid(
            columns: [GridSize.Star()],
            rows: [GridSize.Auto, GridSize.Star()],
            When(headerShown, () => Header(nav, route)),
            NavigationHost(nav, name => name == Examples.HomeTitle
                ? Component<HomeList>()
                : Examples.Find(name)?.Component() ?? TextBlock("Not found")).Grid(row: 1));
    }

    private static Element Header(NavigationHandle<string> nav, string title) =>
        HStack(8,
            When(nav.CanGoBack, () => Button("Back", () => nav.GoBack()).AutomationId("back")),
            Subtitle(title).AutomationId("screen-title").VAlign(VerticalAlignment.Center))
        .Padding(12, 8);
}
