// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;
// The namespace of these tests is named after the type, so the type is reached under another name.
using EngineElementList = E2E.Engine.ElementList;

namespace E2E.Tests.ElementList;

public sealed class ObservedRefsTests
{
    private static ListedTarget Target(EngineElementList list, int index) => new(list, index);

    private static Observation Snapshot() => new() { Route = "", Roots = [] };

    [Fact]
    public async Task Resolve_to_the_element_at_their_position_in_the_list_the_observation_published()
    {
        var registry = new RefRegistry();
        var scripted = new ScriptedList();
        var list = new EngineElementList(scripted.Handle);
        var capture = registry.BeginCapture();
        registry.Publish(capture, new CapturedObservation(Snapshot(), new Dictionary<string, ListedTarget>
        {
            ["n1"] = Target(list, 0),
            ["n2"] = Target(list, 1),
        }));
        var resolved = await registry.LookupAsync("n2");
        Assert.Equal([1], scripted.Made.Select(element => element.Index));
        Assert.Equal(new ElementTarget((IElementHandle)scripted.Made[0].Handle), resolved);
    }

    [Fact]
    public async Task Release_a_superseded_generation_once_however_many_nodes_it_listed_and_then_go_stale()
    {
        var registry = new RefRegistry();
        var scripted = new ScriptedList();
        var list = new EngineElementList(scripted.Handle);
        registry.Publish(registry.BeginCapture(), new CapturedObservation(Snapshot(), new Dictionary<string, ListedTarget>
        {
            ["n1"] = Target(list, 0),
            ["n2"] = Target(list, 1),
            ["n3"] = Target(list, 2),
        }));
        registry.Publish(registry.BeginCapture(), new CapturedObservation(Snapshot(), []));
        await Task.Yield();
        Assert.Equal(1, scripted.Disposed);
        var stale = await Assert.ThrowsAsync<EngineException>(() => registry.LookupAsync("n1"));
        Assert.Equal(EngineErrorCodes.NodeStale, stale.Code);
    }
}
