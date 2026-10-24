// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.UI.Xaml.Controls;
using static WinAppBenchmark.App.Support;

namespace WinAppBenchmark.Examples;

/// <summary>
/// Paginated scrolling to a target deep in a list. Thirty rows per page, a page loads 600 ms after
/// the scroll nears the end, and the rows are virtualized: a row off the screen is not in the UI
/// Automation tree.
/// </summary>
public sealed class InfiniteScrollList : Component
{
    private const int PageSize = 30;
    private const int TotalItems = 150;
    private const int TargetItem = 137;
    private const int LoadMilliseconds = 600;

    public override Element Render()
    {
        var (count, setCount) = UseState(PageSize);
        var (loading, setLoading) = UseState(false);
        var (foundTarget, setFoundTarget) = UseState(false);
        var loadingNow = UseRef(false);
        var scroll = this.UseElementRef<ScrollView>();
        var items = UseMemo(() => (IReadOnlyList<int>)[.. Enumerable.Range(1, count)], count);

        // Loads the next page once the viewport is within half a screen of the end, as the mobile
        // list's onEndReached does.
        void ViewChanged()
        {
            var view = scroll.Current;
            if (view is null || loadingNow.Current || count >= TotalItems)
            {
                return;
            }

            if (view.ExtentHeight - (view.VerticalOffset + view.ViewportHeight) > view.ViewportHeight * 0.5)
            {
                return;
            }

            loadingNow.Current = true;
            setLoading(true);
            After(LoadMilliseconds, CancellationToken.None, () =>
            {
                setCount(Math.Min(TotalItems, count + PageSize));
                setLoading(false);
                loadingNow.Current = false;
            });
        }

        if (foundTarget)
        {
            return VStack(Success($"Found item {TargetItem}")).Padding(24);
        }

        return Grid(
            columns: [GridSize.Star()],
            rows: [GridSize.Auto, GridSize.Star(), GridSize.Auto],
            Hint($"Scroll down and tap Item {TargetItem} to complete").Padding(16, 12),
            ScrollView(ItemsRepeater(items, item => item.ToString(), (item, _) =>
                    Button(TextBlock($"Item {item}"), () =>
                    {
                        if (item == TargetItem)
                        {
                            setFoundTarget(true);
                        }
                    })
                    .AutomationId($"item-{item}")
                    .Height(44)
                    .HAlign(HorizontalAlignment.Stretch)
                    .CornerRadius(0)))
                .Ref(scroll)
                .ViewChanged(ViewChanged)
                .AutomationId("benchmark-list")
                .Grid(row: 1),
            When(loading, () => ProgressRing().AutomationId("list-loading").Padding(12)).Grid(row: 2));
    }
}
