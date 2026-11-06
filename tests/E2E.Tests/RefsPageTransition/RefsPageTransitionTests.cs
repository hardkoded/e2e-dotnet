// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.RefsPageTransition;

// .NET-only: upstream's AttemptSession.invalidate() clears the refs at a page transition but has no test for it.
[Collection(BrowserCollection.Name)]
public sealed class RefsPageTransitionTests
{
    [Fact]
    public async Task A_ref_observed_before_a_restart_is_a_retryable_stale_node_not_a_closed_target()
    {
        using var site = await TinySite.StartAsync("<!DOCTYPE html><title>Refs</title><button>Go</button>");
        await using var session = await new WebEngine(headless: true).StartAsync(new EngineStartOptions { ActionTimeout = TimeSpan.FromSeconds(10) }, CancellationToken.None);
        await session.OpenAsync(site.Url, CancellationToken.None);
        var observation = await session.ObserveAsync(CancellationToken.None);
        var go = WebSemanticsTests.Flatten(observation.Roots).First(node => node.Role == "button");

        await session.RestartAsync(CancellationToken.None);

        var error = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(go, new LocatorAction.Tap(), CancellationToken.None));
        Assert.Equal(EngineErrorCodes.NodeStale, error.Code);
        Assert.True(error.Retryable);
    }
}
