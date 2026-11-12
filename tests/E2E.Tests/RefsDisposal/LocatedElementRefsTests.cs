// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AttemptSession;
using Microsoft.Playwright;

namespace E2E.Tests.RefsDisposal;

// Element-backed located refs release their handles when pruned or cleared, like observation refs.
public sealed class LocatedElementRefsTests
{
    private static (IElementHandle Element, Func<int> Disposed) Handle()
    {
        var disposed = 0;
        var (element, fake) = Fake<IElementHandle>.New();
        fake.Members["DisposeAsync"] = _ =>
        {
            Interlocked.Increment(ref disposed);
            return ValueTask.CompletedTask;
        };
        return (element, () => Volatile.Read(ref disposed));
    }

    [Fact]
    public async Task Are_disposed_when_the_attempt_clears_its_refs()
    {
        var refs = new RefRegistry();
        var pinned = Handle();
        var id = refs.StoreLocated(new ElementTarget(pinned.Element));
        Assert.Equal(new ElementTarget(pinned.Element), await refs.LookupAsync(id));
        refs.Clear();
        await Task.Yield();
        Assert.Equal(1, pinned.Disposed());
        var stale = await Assert.ThrowsAsync<EngineException>(() => refs.LookupAsync(id));
        Assert.Matches("stale", stale.Message);
    }

    [Fact]
    public async Task Are_disposed_when_pruned_past_the_registry_bound()
    {
        var refs = new RefRegistry();
        var first = Handle();
        refs.StoreLocated(new ElementTarget(first.Element));
        for (var i = 0; i < 2048; i++)
        {
            refs.StoreLocated(new ElementTarget(Handle().Element));
        }

        await Task.Yield();
        Assert.Equal(1, first.Disposed());
    }
}
