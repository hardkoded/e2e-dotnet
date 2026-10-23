// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Mcp;
using E2E.Cli.Tests.Helpers;
using E2E.Engine;

namespace E2E.Cli.Tests.McpTestLines;

/// <summary>
/// How a test line resolves a locator on the live screen: through the engine's <c>LocateAsync</c> when the engine has one, so the
/// observation's ids stay valid, else over a fresh observation. .NET-only: <c>CanLocate</c> is a port transition, and upstream's
/// engines all locate.
/// </summary>
public sealed class LineLocatorTests
{
    private static readonly LocatorExpression Save = SessionCatalog.LocateQuery(new LocateArgs { Role = "button", Name = "Save" }).Query;

    [Fact]
    public async Task Asks_an_engine_that_locates_for_the_matches_with_two_seconds_and_never_observes()
    {
        var asked = new List<(LocatorExpression Expression, TimeSpan Timeout)>();
        var engine = new FakeEngine { Locate = (expression, timeout) => { asked.Add((expression, timeout)); return [new SemanticNode { Ref = "bound-1", Role = "button", Name = "Save" }]; } };
        await using var context = await SessionContext.StartAsync(engine);

        var matches = await SessionCatalog.LineResolver(context.Session.Engine)(Save, CancellationToken.None);

        Assert.Equal(["bound-1"], matches.Select(node => node.Ref));
        Assert.Equal([(Save, TimeSpan.FromSeconds(2))], asked);
        Assert.Equal(0, engine.Observations);
    }

    [Fact]
    public async Task Passes_on_the_failure_of_a_locate()
    {
        var engine = new FakeEngine { Locate = (_, _) => throw new EngineException("TIMEOUT", "the screen did not settle in 2s") };
        await using var context = await SessionContext.StartAsync(engine);

        var failure = await Assert.ThrowsAsync<EngineException>(() => SessionCatalog.LineResolver(context.Session.Engine)(Save, CancellationToken.None));

        Assert.Equal("TIMEOUT", failure.Code);
    }

    [Fact]
    public async Task Resolves_every_locator_over_one_look_when_the_engine_does_not_locate()
    {
        var engine = new FakeEngine();
        await using var context = await SessionContext.StartAsync(engine);
        var resolve = SessionCatalog.LineResolver(context.Session.Engine);

        var submit = await resolve(SessionCatalog.LocateQuery(new LocateArgs { Role = "button", Name = "Submit" }).Query, CancellationToken.None);
        var table = await resolve(SessionCatalog.LocateQuery(new LocateArgs { Role = "textbox", Name = "Table" }).Query, CancellationToken.None);

        Assert.Equal(["k-1"], submit.Select(node => node.Ref));
        Assert.Equal(["k-2"], table.Select(node => node.Ref));
        Assert.Equal(1, engine.Observations);
    }
}
