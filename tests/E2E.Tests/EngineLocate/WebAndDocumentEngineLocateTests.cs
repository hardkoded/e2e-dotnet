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
        <button type="button" onclick="document.getElementById('log').textContent = 'saved'">Save</button>
        <button type="button">Cancel</button>
        <div role="status" hidden>Saved</div>
        <p id="log">idle</p>
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
        var log = Assert.Single(await session.LocateAsync(LocatorExpression.For("text", "saved", new TextMatchOptions(), "text"), Budget, CancellationToken.None));
        Assert.Equal("saved", log.Text);
    }

    private const string Rows = """
        <!DOCTYPE html>
        <html><body>
        <button type="button" id="add">Add</button>
        <ul id="rows"></ul>
        <input id="draft" value="draft">
        <p id="log">idle</p>
        <script>
          function row() {
            const item = document.createElement('li');
            const button = document.createElement('button');
            button.textContent = 'Delete';
            button.addEventListener('click', () => { document.getElementById('log').textContent = 'deleted'; });
            item.append(button);
            document.getElementById('rows').append(item);
          }
          row();
          document.getElementById('add').addEventListener('click', row);
        </script>
        </body></html>
        """;

    [Fact]
    public async Task The_web_engine_acts_on_a_ref_of_several_matches_after_more_twins_appear()
    {
        using var site = await TinySite.StartAsync(Rows);
        await using var session = await WebSemanticsTests.OpenAsync(site.Url, new WebEngineOptions { Headless = true });
        var add = Assert.Single(await session.LocateAsync(LocatorExpression.ForRole("button", "Add", new RoleOptions()), Budget, CancellationToken.None));
        await session.PerformAsync(add, new LocatorAction.Tap(), CancellationToken.None);
        var deletes = await session.LocateAsync(LocatorExpression.ForRole("button", "Delete", new RoleOptions()), Budget, CancellationToken.None);
        Assert.Equal(2, deletes.Count);
        await session.PerformAsync(add, new LocatorAction.Tap(), CancellationToken.None);

        await session.PerformAsync(deletes[0], new LocatorAction.Tap(), CancellationToken.None);
        var log = Assert.Single(await session.LocateAsync(LocatorExpression.For("text", "deleted", new TextMatchOptions(), "text"), Budget, CancellationToken.None));
        Assert.Equal("deleted", log.Text);
    }

    [Fact]
    public async Task The_web_engine_acts_on_an_observed_node_after_a_twin_appears_even_when_a_locate_matched_it_alone()
    {
        using var site = await TinySite.StartAsync(Rows);
        await using var session = await WebSemanticsTests.OpenAsync(site.Url, new WebEngineOptions { Headless = true });
        var observed = WebSemanticsTests.Flatten((await session.ObserveAsync(CancellationToken.None)).Roots).Single(node => node.Role == "button" && node.Name == "Delete");
        var located = Assert.Single(await session.LocateAsync(LocatorExpression.ForRole("button", "Delete", new RoleOptions()), Budget, CancellationToken.None));
        Assert.Equal(observed.Ref, located.Ref);
        var add = Assert.Single(await session.LocateAsync(LocatorExpression.ForRole("button", "Add", new RoleOptions()), Budget, CancellationToken.None));
        await session.PerformAsync(add, new LocatorAction.Tap(), CancellationToken.None);

        // The observation names its own element; only the located node was the locator's alone.
        await session.PerformAsync(observed, new LocatorAction.Tap(), CancellationToken.None);
        var log = Assert.Single(await session.LocateAsync(LocatorExpression.For("text", "deleted", new TextMatchOptions(), "text"), Budget, CancellationToken.None));
        Assert.Equal("deleted", log.Text);
    }

    [Fact]
    public async Task The_web_engine_acts_on_a_display_value_match_after_another_input_takes_the_value()
    {
        using var site = await TinySite.StartAsync(Rows.Replace("</body>", "<button type=\"button\" id=\"copy\" onclick=\"document.body.insertAdjacentHTML('beforeend', '<input value=draft>')\">Copy</button></body>", StringComparison.Ordinal));
        await using var session = await WebSemanticsTests.OpenAsync(site.Url, new WebEngineOptions { Headless = true });
        var draft = Assert.Single(await session.LocateAsync(LocatorExpression.For("displayValue", "draft", new TextMatchOptions(), "displayValue"), Budget, CancellationToken.None));
        var copy = Assert.Single(await session.LocateAsync(LocatorExpression.ForRole("button", "Copy", new RoleOptions()), Budget, CancellationToken.None));
        await session.PerformAsync(copy, new LocatorAction.Tap(), CancellationToken.None);

        // Upstream pins a display-value match to the element it read, even when it was the only one.
        await session.PerformAsync(draft, new LocatorAction.Fill("final"), CancellationToken.None);
        Assert.Equal("final", Assert.Single(await session.LocateAsync(LocatorExpression.For("displayValue", "final", new TextMatchOptions(), "displayValue"), Budget, CancellationToken.None)).Value);
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
        var tapped = new List<string>();
        var shown = 0;
        var world = new DocumentWorld().Map("/", page =>
        {
            page.Button("Save", () => tapped.Add("Save"));
            page.Button("Cancel", () => tapped.Add("Cancel"));
        });
        await using var session = await new DocumentEngine(world).StartAsync(new EngineStartOptions { Screen = _ => shown++ }, CancellationToken.None);
        await session.OpenAsync("/", CancellationToken.None);
        Assert.True(session.CanLocate);

        var located = await session.LocateAsync(LocatorExpression.ForRole("button", null, new RoleOptions()), Budget, CancellationToken.None);
        Assert.Equal(["Save", "Cancel"], located.Select(node => node.Name));

        // The refs of a locate name the elements the engine acts on.
        await session.PerformAsync(located[1], new LocatorAction.Tap(), CancellationToken.None);
        Assert.Equal(["Cancel"], tapped);
        Assert.Empty(await session.LocateAsync(LocatorExpression.ForRole("button", "Missing", new RoleOptions()), Budget, CancellationToken.None));

        // Each locate shows the trace the observation it read.
        Assert.Equal(2, shown);
    }

    [Fact]
    public async Task A_locator_waits_for_a_hidden_role_node_to_be_attached_through_the_engine()
    {
        using var site = await TinySite.StartAsync(Page);
        var session = await WebEngineTests.StartAsync(site, new ScriptedModel(_ => ModelResponses.Done("passed", "unused")), cache: null);
        await WebEngineTests.RunAsync(session, async () =>
        {
            await session.App.OpenAsync("/");
            Assert.True(session.Screen.LocatesInEngine);
            Assert.Equal(0, await session.Screen.GetByRole("status").CountAsync());
            await Expect.That(session.Screen.GetByRole("status")).ToBeAttachedAsync();
            await session.Screen.GetByRole("status").WaitForAsync(new LocatorWaitForOptions { State = WaitForState.Hidden });
            await Expect.That(session.Screen.GetByRole("status")).ToBeHiddenAsync();
        });
    }
}
