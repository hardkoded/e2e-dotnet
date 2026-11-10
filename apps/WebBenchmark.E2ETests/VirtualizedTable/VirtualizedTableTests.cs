// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.VirtualizedTable;

/// <summary>
/// Ported from upstream's <c>apps/web-benchmark/tests/virtualized-table.e2e.ts</c>: its one test is outside any <c>describe</c>.
/// The Golden Row is row index 4321 at 36 px a row, 155 556 px down a 480 px viewport. A slow fling on the table moves three quarters of its
/// height, 360 px, so about 432 strides reach it, and no row passes unseen because a stride is shorter than the viewport; measured at about
/// 160 ms a stride, 70 s in all. Twice that, so a slower CI box fails the scroll and not the attempt; the attempt gets the default budget on top
/// for opening and claiming.
/// </summary>
public sealed class VirtualizedTableTests : BenchmarkTest
{
    private const int ScrollTimeoutMs = 140_000;

    private static readonly Regex GoldenRowText = new("^Row 4322");

    /// <summary>Upstream's per-test <c>timeout</c>: the scroll budget plus the default budget for opening and claiming.</summary>
    protected override TimeSpan TestTimeout => TimeSpan.FromMilliseconds(ScrollTimeoutMs + 30_000);

    [Test]
    public async Task Scrolls_the_windowed_table_to_the_Golden_Row_and_claims_it()
    {
        await App.OpenAsync("/e/virtualized-table");
        var viewport = Screen.GetByTestId("table-viewport");
        var rows = viewport.GetByTestId("table-row");
        var claim = Screen.GetByRole("button", "Claim");
        await Expect.That(rows.First()).ToHaveTextAsync("Row 1");
        await Expect.That(claim).ToBeHiddenAsync();

        // The table is its own scroll container, so the table pages, not the page.
        await viewport.ScrollUntilVisibleAsync(claim, new ScrollUntilVisibleOptions { Timeout = TimeSpan.FromMilliseconds(ScrollTimeoutMs) });

        await Expect.That(rows.Filter(hasText: "Golden Row")).ToHaveTextAsync(GoldenRowText);
        await Expect.That(rows.Filter(hasText: "Row 1")).ToHaveCountAsync(0);
        await claim.TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Golden Row claimed");
    }
}
