// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E;

namespace E2E.NUnit.Tests.Speed;

/// <summary>
/// The floor of what a deterministic step costs against a page that has nothing to wait for, ported from
/// upstream's <c>apps/testbed/tests/speed.e2e.ts</c>. A settle wait, a redundant observation, or a retry loop
/// in the action path shows up here first.
/// </summary>
public sealed class DeterministicSpeedFloorTests : E2ETest
{
    private const int Taps = 40;
    private const int Fills = 20;
    private const int Reads = 20;

    // An action may cost this many times a bare locator read, measured in the same test so a slow machine
    // slows both, and never less than the floor.
    private const double ActionRatio = 20;
    private const double ActionFloorMs = 100;

    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    public async Task Forty_taps_each_cost_no_more_than_a_few_locator_reads()
    {
        await App.OpenAsync("/speed");
        var increment = Screen.GetByRole("button", "Increment");
        var readMs = await ReadCostAsync(increment);

        var started = Environment.TickCount64;
        for (var i = 0; i < Taps; i++)
        {
            await increment.TapAsync();
        }

        var perTap = (Environment.TickCount64 - started) / (double)Taps;

        await Expect.That(Screen.GetByRole("status", "Count")).ToHaveTextAsync(Taps.ToString(CultureInfo.InvariantCulture));
        Assert.That(perTap, Is.LessThanOrEqualTo(Budget(readMs)), Verdict("tap", perTap, readMs));
    }

    [Test]
    public async Task Twenty_fills_each_cost_no_more_than_a_few_locator_reads()
    {
        await App.OpenAsync("/speed");
        var echo = Screen.GetByLabel("Echo");
        var readMs = await ReadCostAsync(echo);

        var started = Environment.TickCount64;
        for (var i = 0; i < Fills; i++)
        {
            await echo.FillAsync($"value {i}");
        }

        var perFill = (Environment.TickCount64 - started) / (double)Fills;

        await Expect.That(Screen.GetByRole("status", "Echoed")).ToHaveTextAsync($"value {Fills - 1}");
        Assert.That(perFill, Is.LessThanOrEqualTo(Budget(readMs)), Verdict("fill", perFill, readMs));
    }

    // Milliseconds one CountAsync on the locator costs right now, averaged over Reads reads.
    private static async Task<double> ReadCostAsync(Locator locator)
    {
        var started = Environment.TickCount64;
        for (var i = 0; i < Reads; i++)
        {
            await locator.CountAsync();
        }

        return (Environment.TickCount64 - started) / (double)Reads;
    }

    private static double Budget(double readMs) => Math.Max(ActionFloorMs, ActionRatio * readMs);

    private static string Verdict(string action, double perActionMs, double readMs) =>
        $"{perActionMs:F1} ms per {action} against a {Budget(readMs):F1} ms budget (a read costs {readMs:F1} ms)";
}
