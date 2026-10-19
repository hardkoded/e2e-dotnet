// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Mobile.Tests.Screens;

namespace E2E.Mobile.Tests.Engine;

public sealed class ReferenceLifetimeAndCancellationTests
{
    [Fact]
    public async Task Bounds_located_bindings_while_preserving_current_observation_ids()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        var observation = await Observed(session, "About");
        var expression = new LocatorExpression { Kind = "selector", Selector = "id=ABOUT" };
        var oldest = (await session.LocateAsync(expression, TimeSpan.FromSeconds(5), CancellationToken.None))[0];
        var newest = oldest;
        for (var i = 0; i < 2050; i++)
        {
            newest = (await session.LocateAsync(expression, TimeSpan.FromSeconds(5), CancellationToken.None))[0];
        }

        var stale = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(oldest, new LocatorAction.Tap(), CancellationToken.None));
        Assert.Equal("NODE_STALE", stale.Code);
        await session.PerformAsync(newest, new LocatorAction.Tap(), CancellationToken.None);
        await session.PerformAsync(observation, new LocatorAction.Tap(), CancellationToken.None);
    }

    [Fact]
    public async Task Reads_the_observation_location_off_the_same_snapshot_as_the_tree()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        var before = h.Fake.Methods().Count(method => method == "capture.snapshot");
        var observation = await session.ObserveAsync(CancellationToken.None);
        Assert.Equal("com.apple.Preferences / General", observation.Route);
        Assert.Equal(before + 1, h.Fake.Methods().Count(method => method == "capture.snapshot"));
    }

    [Fact]
    public async Task Never_dispatches_a_command_or_action_when_its_signal_is_already_aborted()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        var node = await Observed(session, "About");
        var before = h.Fake.Calls.Count;
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        var home = await Assert.ThrowsAsync<EngineException>(() => session.CommandAsync("home", DeviceMethods.CommandHome, new System.Text.Json.Nodes.JsonObject(), aborted.Token));
        Assert.Equal("CANCELLED", home.Code);
        var tap = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(node, new LocatorAction.Tap(), aborted.Token));
        Assert.Equal("CANCELLED", tap.Code);
        Assert.Equal(before, h.Fake.Calls.Count);
    }
}
