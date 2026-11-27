// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E;
using E2E.Engine;

namespace E2E.NUnit.Tests.Clock;

/// <summary>
/// Upstream's <c>apps/testbed/tests/clock.e2e.ts</c> (<c>describe('browser.clock')</c>, tester-army/e2e#2256), against the playground's <c>/clock</c> page:
/// everything on it runs on the page's time, and <c>Browser.Clock</c> makes each of them deterministic.
/// </summary>
public sealed class BrowserClockTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null
    );

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    public async Task A_paused_clock_moves_only_when_the_test_runs_it()
    {
        await Browser.Clock.InstallAsync(
            new BrowserClockInstallOptions { Time = new DateTimeOffset(2024, 2, 2, 8, 0, 0, TimeSpan.Zero) }
        );
        await Browser.Clock.PauseAtAsync(new DateTimeOffset(2024, 2, 2, 9, 0, 0, TimeSpan.Zero));
        await App.OpenAsync("/clock");
        await Expect.That(Screen.GetByLabel("Today")).ToHaveTextAsync("February 2, 2024");
        await Expect.That(Screen.GetByLabel("Offer")).ToHaveTextAsync("Offer ends in 10s");
        await Expect.That(Screen.GetByLabel("Progress")).ToHaveTextAsync("0%");

        await Browser.Clock.RunForAsync(3000);
        await Expect.That(Screen.GetByLabel("Offer")).ToHaveTextAsync("Offer ends in 7s");
        await Expect.That(Screen.GetByLabel("Progress")).ToHaveTextAsync("100%");

        await Browser.Clock.RunForAsync("00:07");
        await Expect.That(Screen.GetByLabel("Offer")).ToHaveTextAsync("Offer expired");
        await Expect.That(Screen.GetByRole("button", "Claim offer")).ToBeDisabledAsync();
        await Expect.That(Screen.GetByLabel("Session")).ToHaveTextAsync("Signed in");

        await Browser.Clock.FastForwardAsync("30:00");
        await Expect.That(Screen.GetByLabel("Session")).ToHaveTextAsync("Session expired");
    }

    [Test]
    public async Task A_fixed_time_holds_the_date_while_the_pages_timers_keep_running()
    {
        await Browser.Clock.SetFixedTimeAsync("2030-12-31T23:59:59Z");
        await App.OpenAsync("/clock");
        await Expect.That(Screen.GetByLabel("Today")).ToHaveTextAsync("December 31, 2030");

        // The countdown runs in real time here, so any second after the first will do.
        await Expect.That(Screen.GetByLabel("Offer")).ToHaveTextAsync(new Regex("^Offer ends in [1-9]s$"));
        await App.RestartAsync();
        await App.OpenAsync("/clock");
        await Expect.That(Screen.GetByLabel("Today")).ToHaveTextAsync("December 31, 2030");
    }
}
