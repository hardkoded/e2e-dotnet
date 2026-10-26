// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.EngineLocate;

/// <summary>
/// .NET-only: the web and document engines resolve a locator themselves (<c>LocateAsync</c>), as upstream's engines all do. Upstream's
/// web engine resolves with Playwright selectors, which the port's page script replaces, so no upstream <c>it</c> covers the port's
/// resolve; these check that the engine answers what core's own resolve over its observation answered.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class WebAndDocumentEngineLocateTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    private const string Page = """
        <!DOCTYPE html>
        <html><body>
        <button type="button">Save</button>
        <button type="button">Cancel</button>
        <div role="status" hidden>Saved</div>
        </body></html>
        """;

    [Fact]
    public async Task The_web_engine_locates_the_nodes_a_query_matches_and_keeps_their_refs()
    {
        using var site = await TinySite.StartAsync(Page);
        await using var session = await WebSemanticsTests.OpenAsync(site.Url, new WebEngineOptions { Headless = true });
        Assert.True(session.CanLocate);

        var located = await session.LocateAsync(LocatorExpression.ForRole("button", null, new RoleOptions()), Budget, CancellationToken.None);
        Assert.Equal(["Save", "Cancel"], located.Select(node => node.Name));

        // The node is the engine's own: acting on it works without another look.
        await session.PerformAsync(located[0], new LocatorAction.Tap(), CancellationToken.None);
        var one = await session.LocateAsync(LocatorExpression.ForRole("button", "Cancel", new RoleOptions()), Budget, CancellationToken.None);
        Assert.Single(one);
    }

    [Fact]
    public async Task The_web_engine_skips_a_hidden_role_match_unless_the_expression_asks_for_it()
    {
        using var site = await TinySite.StartAsync(Page);
        await using var session = await WebSemanticsTests.OpenAsync(site.Url, new WebEngineOptions { Headless = true });
        var status = LocatorExpression.ForRole("status", null, new RoleOptions());

        Assert.Empty(await session.LocateAsync(status, Budget, CancellationToken.None));
        var hidden = Assert.Single(await session.LocateAsync(status with { IncludeHidden = true }, Budget, CancellationToken.None));
        Assert.True(hidden.States.Hidden);
    }

    [Fact]
    public async Task The_document_engine_locates_the_nodes_a_query_matches_in_document_order()
    {
        var world = new DocumentWorld().Map("/", page =>
        {
            page.Button("Save", () => { });
            page.Button("Cancel", () => { });
        });
        await using var session = await new DocumentEngine(world).StartAsync(new EngineStartOptions(), CancellationToken.None);
        await session.OpenAsync("/", CancellationToken.None);
        Assert.True(session.CanLocate);

        var located = await session.LocateAsync(LocatorExpression.ForRole("button", null, new RoleOptions()), Budget, CancellationToken.None);
        Assert.Equal(["Save", "Cancel"], located.Select(node => node.Name));

        // The refs of a locate name the elements the engine acts on.
        await session.PerformAsync(located[1], new LocatorAction.Tap(), CancellationToken.None);
        Assert.Empty(await session.LocateAsync(LocatorExpression.ForRole("button", "Missing", new RoleOptions()), Budget, CancellationToken.None));
    }
}
