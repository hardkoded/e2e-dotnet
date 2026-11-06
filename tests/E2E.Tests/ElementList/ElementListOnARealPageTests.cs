// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
// The namespace of these tests is named after the type, so the type is reached under another name.
using EngineElementList = E2E.Engine.ElementList;

namespace E2E.Tests.ElementList;

// An observed node's element is made from the in-page array, so a document replaced since the walk must read as a stale node.
[Collection(BrowserCollection.Name)]
public sealed class ElementListOnARealPageTests(ChromiumPage chromium) : IClassFixture<ChromiumPage>, IAsyncLifetime
{
    public Task InitializeAsync() => chromium.Page.SetContentAsync("<button id=\"go\">Go</button>");

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>The array the reader would leave behind: every element of the walk, in document order.</summary>
    private async Task<EngineElementList> ListOfAsync() =>
        new(await chromium.Page.EvaluateHandleAsync("() => [...document.querySelectorAll('button')]"));

    [Fact]
    public async Task Makes_a_handle_for_the_element_the_walk_read()
    {
        var list = await ListOfAsync();
        var element = await list.AtAsync(0);
        Assert.Equal("go", await element.EvaluateAsync<string>("node => node.id"));
        list.Dispose();
    }

    [Fact]
    public async Task Reports_a_navigation_since_the_walk_as_a_retryable_stale_node()
    {
        var list = await ListOfAsync();
        await chromium.Page.GotoAsync("about:blank");
        var stale = await Assert.ThrowsAsync<EngineException>(() => list.AtAsync(0));
        Assert.Equal(EngineErrorCodes.NodeStale, stale.Code);
        Assert.True(stale.Retryable);
        list.Dispose();
    }

    [Fact]
    public async Task Reports_an_index_the_walk_never_listed_as_a_retryable_stale_node()
    {
        var list = await ListOfAsync();
        var stale = await Assert.ThrowsAsync<EngineException>(() => list.AtAsync(5));
        Assert.Equal(EngineErrorCodes.NodeStale, stale.Code);
        Assert.True(stale.Retryable);
        list.Dispose();
    }
}
