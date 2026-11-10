// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.StaleDom;

/// <summary>Ported from upstream's <c>apps/web-benchmark/tests/stale-dom.e2e.ts</c>: its one test is outside any <c>describe</c>.</summary>
public sealed class StaleDomTests : BenchmarkTest
{
    [Test]
    public async Task Taps_the_moving_target_three_times_across_rebuilds_of_the_list()
    {
        await App.OpenAsync("/e/stale-dom");
        var target = Screen.GetByRole("button", "Tap me");
        var progress = Screen.GetByTestId("progress");
        await Expect.That(progress).ToHaveTextAsync("Progress: 0 / 3");
        await Expect.That(Screen.GetByRole("button", "Decoy")).ToHaveCountAsync(5);

        await target.TapAsync();
        await Expect.That(progress).ToHaveTextAsync("Progress: 1 / 3");
        await WaitForRebuildAsync(target);
        await target.TapAsync();
        await Expect.That(progress).ToHaveTextAsync("Progress: 2 / 3");
        await WaitForRebuildAsync(target);
        await target.TapAsync();

        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Target clicked 3 times");
        await Expect.That(Screen.GetByTestId("error-message")).ToBeHiddenAsync();
        await Expect.That(target).ToBeHiddenAsync();
    }

    /// <summary>
    /// Waits for the next rebuild: every generation remounts the rows and rotates the order by one slot, so the target's box moves once a
    /// second.
    /// </summary>
    private static async Task WaitForRebuildAsync(Locator target)
    {
        var before = (await target.BoundingBoxAsync())?.Y;
        await Expect.Poll(async () => (await target.BoundingBoxAsync())?.Y, new PollOptions { Timeout = TimeSpan.FromSeconds(5) })
            .Not.ToBeAsync(before);
    }
}
