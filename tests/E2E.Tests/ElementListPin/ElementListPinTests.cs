// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.ElementList;
using EngineElementList = E2E.Engine.ElementList;

namespace E2E.Tests.ElementListPin;

// .NET-only: the port runs operations concurrently, so an observation can publish while an action holds a handle. Upstream has no pin.
public sealed class ElementListPinTests
{
    [Fact]
    public async Task A_pinned_list_releases_its_handles_after_the_last_pin_not_when_disposed()
    {
        var scripted = new ScriptedList();
        var list = new EngineElementList(scripted.Handle);
        var element = await list.AtAsync(0);
        var pin = list.Pin();

        list.Dispose();
        await Task.Yield();
        Assert.Equal(0, scripted.Disposed);
        Assert.Equal(0, scripted.Made[0].Disposed);
        Assert.NotNull(element);

        pin.Dispose();
        pin.Dispose();
        await Task.Yield();
        Assert.Equal(1, scripted.Disposed);
        Assert.Equal(1, scripted.Made[0].Disposed);
    }

    [Fact]
    public async Task A_disposed_list_refuses_a_pin_and_a_new_read()
    {
        var scripted = new ScriptedList();
        var list = new EngineElementList(scripted.Handle);
        list.Dispose();
        Assert.Equal(EngineErrorCodes.NodeStale, Assert.Throws<EngineException>(() => list.Pin()).Code);
        var refused = await Assert.ThrowsAsync<EngineException>(() => list.AtAsync(0));
        Assert.Equal(EngineErrorCodes.NodeStale, refused.Code);
    }

    [Fact]
    public async Task A_lookup_the_deadline_abandoned_releases_its_pin_when_it_settles()
    {
        var registry = new RefRegistry();
        var open = new TaskCompletionSource();
        var scripted = new ScriptedList(gate: open.Task);
        var list = new EngineElementList(scripted.Handle);
        registry.Publish(registry.BeginCapture(), new CapturedObservation(new Observation { Route = "", Roots = [] }, new Dictionary<string, ListedTarget> { ["n1"] = new(list, 0) }));
        var lookup = registry.LookupPinnedAsync("n1");
        WebSupport.DisposePinWhenSettled(lookup);

        // The caller gave up; the list is then replaced while the lookup is still pending.
        registry.Publish(registry.BeginCapture(), new CapturedObservation(new Observation { Route = "", Roots = [] }, []));
        open.SetResult();
        await lookup;
        await Task.Yield();
        await Task.Yield();

        Assert.Equal(1, scripted.Disposed);
        Assert.Equal(1, scripted.Made[0].Disposed);
    }

    [Fact]
    public async Task A_publish_during_a_pinned_lookup_leaves_the_handle_usable()
    {
        var registry = new RefRegistry();
        var scripted = new ScriptedList();
        var list = new EngineElementList(scripted.Handle);
        registry.Publish(registry.BeginCapture(), new CapturedObservation(new Observation { Route = "", Roots = [] }, new Dictionary<string, ListedTarget> { ["n1"] = new(list, 0) }));
        var (_, pin) = await registry.LookupPinnedAsync("n1");

        registry.Publish(registry.BeginCapture(), new CapturedObservation(new Observation { Route = "", Roots = [] }, []));
        await Task.Yield();
        Assert.Equal(0, scripted.Made[0].Disposed);

        pin.Dispose();
        await Task.Yield();
        Assert.Equal(1, scripted.Made[0].Disposed);
    }
}
