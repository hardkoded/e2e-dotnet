// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;
// The namespace of these tests is named after the type, so the type is reached under another name.
using EngineElementList = E2E.Engine.ElementList;

namespace E2E.Tests.ElementList;

// Observed nodes share one in-page array: handles are made when an action reaches for one, and all go with the list.
public sealed class ElementListTests
{
    [Fact]
    public async Task Makes_no_element_handle_until_a_node_is_reached_then_keeps_the_one_it_made()
    {
        var scripted = new ScriptedList();
        var list = new EngineElementList(scripted.Handle);
        Assert.Equal(0, scripted.Evaluated);
        var first = await list.AtAsync(3);
        Assert.Same(first, await list.AtAsync(3));
        Assert.Equal(1, scripted.Evaluated);
        await list.AtAsync(4);
        Assert.Equal(2, scripted.Evaluated);
    }

    [Fact]
    public async Task Shares_one_handle_between_concurrent_reads_of_a_node()
    {
        var scripted = new ScriptedList();
        var list = new EngineElementList(scripted.Handle);
        var both = await Task.WhenAll(list.AtAsync(1), list.AtAsync(1));
        Assert.Same(both[0], both[1]);
        Assert.Equal(1, scripted.Evaluated);
    }

    [Fact]
    public async Task Releases_the_array_and_every_handle_made_from_it_once()
    {
        var scripted = new ScriptedList();
        var list = new EngineElementList(scripted.Handle);
        await list.AtAsync(0);
        await list.AtAsync(1);
        list.Dispose();
        list.Dispose();
        await Task.Yield();
        await Task.Yield();
        Assert.Equal(1, scripted.Disposed);
        Assert.Equal([1, 1], scripted.Made.Select(element => element.Disposed));
    }

    [Fact]
    public async Task Reports_a_node_whose_element_is_gone_as_stale_and_retries_a_later_read()
    {
        var gone = new ScriptedList(missing: true);
        var list = new EngineElementList(gone.Handle);
        var first = await Assert.ThrowsAsync<EngineException>(() => list.AtAsync(0));
        Assert.Equal(EngineErrorCodes.NodeStale, first.Code);
        Assert.True(first.Retryable);
        var second = await Assert.ThrowsAsync<EngineException>(() => list.AtAsync(0));
        Assert.Equal(EngineErrorCodes.NodeStale, second.Code);
        Assert.Equal(2, gone.Evaluated);
        Assert.Equal(1, gone.Made[0].Disposed);
    }

    [Fact]
    public async Task Reports_a_read_that_lost_its_document_to_a_navigation_as_stale()
    {
        var scripted = new ScriptedList(firstFailure: new PlaywrightException("Execution context was destroyed, most likely because of a navigation"));
        var list = new EngineElementList(scripted.Handle);
        var stale = await Assert.ThrowsAsync<EngineException>(() => list.AtAsync(0));
        Assert.Equal(EngineErrorCodes.NodeStale, stale.Code);
        Assert.True(stale.Retryable);
    }

    [Fact]
    public async Task Refuses_a_read_after_disposal_and_releases_a_handle_that_arrives_late()
    {
        var open = new TaskCompletionSource();
        var scripted = new ScriptedList(gate: open.Task);
        var list = new EngineElementList(scripted.Handle);
        var late = list.AtAsync(0);
        list.Dispose();
        open.SetResult();
        var refused = await Assert.ThrowsAsync<EngineException>(() => late);
        Assert.Equal(EngineErrorCodes.NodeStale, refused.Code);
        Assert.Equal(1, scripted.Made[0].Disposed);
        var after = await Assert.ThrowsAsync<EngineException>(() => list.AtAsync(1));
        Assert.Equal(EngineErrorCodes.NodeStale, after.Code);
    }
}
