// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.LocatorEngine;

/// <summary>
/// Upstream's <c>describe('readViewport')</c> (<c>packages/e2e/tests/unit/locator-engine.test.ts</c>). The port's locate answers the nodes
/// themselves unless the session has a <c>Read</c>; the test gives it one.
/// </summary>
public sealed class ReadViewportTests
{
    [Fact]
    public async Task Reads_a_ref_superseded_between_the_resolve_and_the_read_as_stale_not_as_gone()
    {
        var scripted = ScriptedLocatorEngine.Make(measuresViewport: true, read: [ScriptedLocatorEngine.ReadStale]);
        var engine = scripted.Engine;
        var reads = engine.Reads(engine.DeadlineFor(TimeSpan.FromSeconds(1)));

        var stale = await engine.ReadViewportAsync(ScriptedLocatorEngine.Expression, CancellationToken.None, reads);
        Assert.Null(stale.Node);
        Assert.Equal(1, stale.Count);
        Assert.True(stale.Stale);

        var fresh = await engine.ReadViewportAsync(ScriptedLocatorEngine.Expression, CancellationToken.None, reads);
        Assert.Same(ScriptedLocatorEngine.Node, fresh.Node);
        Assert.Equal(1, fresh.Count);
        Assert.False(fresh.Stale);
        Assert.Equal(new global::E2E.Engine.ViewportIntersection(true, 1), fresh.Intersection);
    }
}
