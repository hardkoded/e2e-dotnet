// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.OverlayTrap;

/// <summary>Ported from upstream's <c>apps/web-benchmark/tests/overlay-trap.e2e.ts</c>: its one test is outside any <c>describe</c>.</summary>
public sealed class OverlayTrapTests : BenchmarkTest
{
    [Test]
    public async Task Continues_only_after_the_banner_and_the_overlay_are_gone()
    {
        await App.OpenAsync("/e/overlay-trap");
        await Screen.GetByRole("button", "Accept cookies").TapAsync();
        // The transparent overlay arrives half a second after load; its chip is the
        // only way to close it.
        await Screen.GetByRole("button", "close overlay", exact: false).TapAsync();
        await Screen.GetByRole("button", "Continue").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("You made it past the overlay");
    }
}
