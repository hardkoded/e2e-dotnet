// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E.Engine;

namespace E2E.Tests.LocatorEngine;

/// <summary>
/// Upstream's <c>LocatorEngine reads that do not wait</c>. The port's locate answers the nodes themselves, so there is no separate
/// <c>session.read</c> call: the 'readNow answers null for a node that went stale between resolve and read, without a second read' it has
/// nothing to run and is not ported, and the 'readNow re-resolves after a stale resolve' it, which counts a read, is <c>Partial_</c>: it
/// checks the resolves and the node, not a read call.
/// </summary>
public sealed class LocatorEngineReadsThatDoNotWaitTests
{
    private static readonly SemanticNode Node = ScriptedLocatorEngine.Node;

    private static readonly LocatorExpression Expression = ScriptedLocatorEngine.Expression;

    [Fact]
    public async Task ResolveNow_answers_a_missing_frame_with_zero_matches_at_once()
    {
        var scripted = ScriptedLocatorEngine.Make([ScriptedLocatorEngine.Frame, ScriptedLocatorEngine.Nodes(Node)], actionTimeout: TimeSpan.FromMilliseconds(5_000));
        var watch = Stopwatch.StartNew();

        var matches = await scripted.Engine.ResolveNowAsync(Expression, CancellationToken.None);

        Assert.Empty(matches);
        Assert.Equal(1, scripted.Resolves);
        Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(1_000), "took " + watch.Elapsed);
    }

    [Fact]
    public async Task ResolveNow_still_re_resolves_after_a_stale_resolve_a_race_not_a_wait()
    {
        var scripted = ScriptedLocatorEngine.Make([ScriptedLocatorEngine.Stale, ScriptedLocatorEngine.Nodes(Node)]);

        var matches = await scripted.Engine.ResolveNowAsync(Expression, CancellationToken.None);

        Assert.Equal([Node.Ref], matches.Select(match => match.Ref));
        Assert.Equal(2, scripted.Resolves);
    }

    [Fact]
    public async Task ResolveNow_translates_a_non_retryable_failure_without_retrying()
    {
        var scripted = ScriptedLocatorEngine.Make([ScriptedLocatorEngine.Failure]);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => scripted.Engine.ResolveNowAsync(Expression, CancellationToken.None));

        Assert.Equal("ENGINE_FAILURE", ErrorSerializer.Classify(error).Code);
        Assert.Equal(1, scripted.Resolves);
    }

    [Fact]
    public async Task ReadNow_answers_null_for_a_missing_frame_without_reading()
    {
        var scripted = ScriptedLocatorEngine.Make([ScriptedLocatorEngine.Frame], actionTimeout: TimeSpan.FromMilliseconds(5_000));

        Assert.Null(await scripted.Engine.ReadNowAsync(Expression, CancellationToken.None));
        Assert.Equal(1, scripted.Resolves);
    }

    [Fact]
    public async Task Partial_ReadNow_re_resolves_after_a_stale_resolve_as_a_poll_would_then_reads_the_node()
    {
        var scripted = ScriptedLocatorEngine.Make([ScriptedLocatorEngine.Stale, ScriptedLocatorEngine.Nodes(Node)]);

        var node = await scripted.Engine.ReadNowAsync(Expression, CancellationToken.None);

        Assert.Equal(Node.Ref, node?.Ref);
        Assert.Equal(2, scripted.Resolves);
    }

    [Fact]
    public async Task ReadNow_gives_a_resolve_that_stays_stale_the_action_timeout_then_LOCATOR_NOT_FOUND()
    {
        var scripted = ScriptedLocatorEngine.Make(Enumerable.Repeat(ScriptedLocatorEngine.Stale, 50), actionTimeout: TimeSpan.FromMilliseconds(350));
        var watch = Stopwatch.StartNew();

        var error = await Assert.ThrowsAnyAsync<Exception>(() => scripted.Engine.ReadNowAsync(Expression, CancellationToken.None));

        Assert.Equal("LOCATOR_NOT_FOUND", ErrorSerializer.Classify(error).Code);
        Assert.True(watch.Elapsed >= TimeSpan.FromMilliseconds(300), "failed after " + watch.Elapsed);
        Assert.True(scripted.Resolves > 1);
    }

    [Fact]
    public async Task ReadNow_reads_the_one_match_and_refuses_two_with_LOCATOR_AMBIGUOUS()
    {
        Assert.Equal(Node.Ref, (await ScriptedLocatorEngine.Make().Engine.ReadNowAsync(Expression, CancellationToken.None))?.Ref);
        var twins = ScriptedLocatorEngine.Make([ScriptedLocatorEngine.Nodes(Node, new SemanticNode { Ref = "node-2", Role = "button", Name = "Submit" })]);

        var error = await Assert.ThrowsAsync<TestException>(() => twins.Engine.ReadNowAsync(Expression, CancellationToken.None));

        Assert.Equal("LOCATOR_AMBIGUOUS", error.Code);
    }

    [Fact]
    public async Task ReadAllNow_answers_a_missing_frame_with_no_nodes_without_reading()
    {
        var scripted = ScriptedLocatorEngine.Make([ScriptedLocatorEngine.Frame], actionTimeout: TimeSpan.FromMilliseconds(5_000));

        Assert.Empty(await scripted.Engine.ReadAllNowAsync(Expression, CancellationToken.None));
        Assert.Equal(1, scripted.Resolves);
    }
}
