// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.DivSoup;

/// <summary>Ported from <c>describe('div soup')</c> in upstream's <c>apps/web-benchmark/tests/div-soup.e2e.ts</c>.</summary>
public sealed class DivSoupTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheDivSoup() => await App.OpenAsync("/e/div-soup");

    [Test]
    public async Task A_wrong_code_is_rejected()
    {
        await TapTilesAsync(["1", "2", "3", "4", "→"]);
        await Expect.That(Screen.GetByText("Wrong code")).ToBeVisibleAsync();
    }

    [Test]
    public async Task The_right_code_grants_access_after_the_delete_tile_fixes_a_typo()
    {
        await TapTilesAsync(["7", "2", "8", "‹", "9", "1", "→"]);
        await Expect.That(Screen.GetByText("Access granted")).ToBeVisibleAsync();
    }

    /// <summary>Upstream's <c>tapTiles</c>: the pad is bare divs with no roles or ids, so a tile's glyph is its only handle.</summary>
    private async Task TapTilesAsync(IReadOnlyList<string> tiles)
    {
        foreach (var tile in tiles)
        {
            await Screen.GetByText(tile).TapAsync();
        }
    }
}
