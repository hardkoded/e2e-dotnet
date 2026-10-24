// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.UI.Reactor.Navigation;

namespace WinAppBenchmark.App;

/// <summary>
/// The home list, two columns of compact rows so every scenario is on screen at the app's size: a
/// suite reaches a scenario with one tap and no scrolling. A row's AutomationId is the scenario name
/// and its name joins the name and the description, as upstream's <c>testID</c> and
/// <c>accessibilityLabel</c> do.
/// </summary>
public sealed class HomeList : Component
{
    private const double RowHeight = 44;

    public override Element Render()
    {
        var nav = UseNavigation<string>();
        var examples = Examples.All;
        var rows = (examples.Count + 1) / 2;

        return ScrollView(Grid(
            columns: [GridSize.Star(), GridSize.Star()],
            rows: [.. Enumerable.Repeat(GridSize.Px(RowHeight), rows)],
            [.. examples.Select((example, index) => Row(nav, example).Grid(row: index / 2, column: index % 2))]));
    }

    private static Element Row(NavigationHandle<string> nav, Example example) =>
        Button(TextBlock(example.Name).SemiBold().FontSize(13).TextWrapping().MaxLines(2), () => nav.Navigate(example.Name))
            .AutomationId(example.Name)
            .AutomationName($"{example.Name}. {example.Description}")
            .Height(RowHeight)
            .HAlign(HorizontalAlignment.Stretch)
            .CornerRadius(0);
}
