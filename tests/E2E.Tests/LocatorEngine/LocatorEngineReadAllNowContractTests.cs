// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.LocatorEngine;

/// <summary>
/// Upstream's <c>LocatorEngine readAllNow contract</c>. The port's locate answers the nodes themselves, so there is no <c>session.read</c> to
/// go stale between a resolve and a read: the 're-resolves the whole set when one member went stale between resolve and read', 'gives up on
/// a set that stays stale at the deadline as LOCATOR_NOT_FOUND' and 'translates non-stale read failures' its have nothing to run and are not
/// ported, and the first it, which counts reads, is <c>Partial_</c>: it checks the resolve and the order, not a read call.
/// </summary>
public sealed class LocatorEngineReadAllNowContractTests
{
    private static readonly SemanticNode Other = new() { Ref = "node-2", Role = "button", Name = "Cancel" };

    [Fact]
    public async Task Partial_Reads_every_current_match_in_the_order_the_engine_resolved_them()
    {
        var scripted = ScriptedLocatorEngine.Make([ScriptedLocatorEngine.Nodes(ScriptedLocatorEngine.Node, Other)]);
        var engine = scripted.Engine;

        var nodes = await engine.ReadAllNowAsync(ScriptedLocatorEngine.Expression, CancellationToken.None, engine.Reads(engine.DeadlineFor()));

        Assert.Equal([ScriptedLocatorEngine.Node.Ref, Other.Ref], nodes.Select(node => node.Ref));
        Assert.Equal(1, scripted.Resolves);
    }

    [Fact]
    public async Task Answers_zero_matches_with_an_empty_list_without_waiting()
    {
        var scripted = ScriptedLocatorEngine.Make([ScriptedLocatorEngine.Nodes()]);
        var engine = scripted.Engine;

        Assert.Empty(await engine.ReadAllNowAsync(ScriptedLocatorEngine.Expression, CancellationToken.None, engine.Reads(engine.DeadlineFor())));
        Assert.Equal(1, scripted.Resolves);
    }
}
