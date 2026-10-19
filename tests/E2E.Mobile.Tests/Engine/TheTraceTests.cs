// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Mobile.Tests.Screens;

namespace E2E.Mobile.Tests.Engine;

public sealed class TheTraceTests
{
    [Fact]
    public async Task Hands_the_harness_the_capture_a_locate_matched_against_with_no_snapshot_of_its_own_and_the_device_it_runs_on()
    {
        var h = Harness.Create(options => options with { Device = "iPhone 16" });
        var screens = new List<Observation>();
        var facts = new List<IReadOnlyDictionary<string, string>>();
        var session = await h.Engine.StartAsync(
            new MobileStartInfo { RunId = "run-1", TargetName = "ios-simulator", ProjectRoot = Harness.ProjectRoot, Screen = screens.Add, Environment = facts.Add },
            CancellationToken.None);
        Assert.Equal([new Dictionary<string, string> { ["platform"] = "ios", ["device"] = "iPhone 16" }], facts);
        await session.ObserveAsync(CancellationToken.None);
        var snapshots = h.Fake.Calls.Count(call => call.Method == "capture.snapshot");
        await session.LocateAsync(LocatorQuery.ForRole("button", null, new RoleOptions()), TimeSpan.FromSeconds(5), CancellationToken.None);
        Assert.Equal(snapshots + 1, h.Fake.Calls.Count(call => call.Method == "capture.snapshot"));
        var screen = Assert.Single(screens);
        Assert.Equal("button", Named(screen.Roots[0], "Back").Role);
        Assert.Contains("com.apple.Preferences", screen.Route, StringComparison.Ordinal);
    }
}
