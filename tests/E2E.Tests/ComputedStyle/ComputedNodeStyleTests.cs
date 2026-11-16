// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.ComputedStyle;

/// <summary>
/// Upstream's <c>describe('computed node style')</c> (<c>packages/web/tests/integration/computed-style.test.ts</c>, tester-army/e2e#1633):
/// computed node properties come from real elements, including frames and shadow roots. The last two tests wrap the ref lookup
/// (<c>RefRegistry.OnceAfterObservedLookup</c>) as upstream spies on <c>RefRegistry.prototype.lookup</c>.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class ComputedNodeStyleTests
{
    private static readonly OperationContext Agent = OperationContext.Agent with { Timeout = TimeSpan.FromSeconds(10) };

    // Finds a listed node by its accessible name.
    private static SemanticNode? Named(IEnumerable<SemanticNode> nodes, string name) =>
        nodes
            .SelectMany(node => (IEnumerable<SemanticNode>)[node, .. Flatten(node.Children)])
            .FirstOrDefault(node => node.Name == name);

    private static IEnumerable<SemanticNode> Flatten(IEnumerable<SemanticNode> nodes) =>
        nodes.SelectMany(node => (IEnumerable<SemanticNode>)[node, .. Flatten(node.Children)]);

    private static async Task<SemanticNode> ObserveNamedAsync(IEngineSession session, string name) =>
        Named((await session.ObserveAsync(Agent, CancellationToken.None)).Roots, name)!;

    private static async Task<(IEngineSession Session, IPage Page)> StartAsync()
    {
        var session = await new WebEngine(headless: true).StartAsync(new EngineStartOptions(), CancellationToken.None);
        await session.OpenAsync("about:blank", CancellationToken.None);
        return (session, WebEngine.SurfaceOf(session)!.Page());
    }

    [Fact]
    public async Task Reads_current_computed_values_of_an_observed_node_without_changing_the_page()
    {
        var (session, page) = await StartAsync();
        await using var _ = session;
        await page.SetContentAsync(
            "<style>button { color: rgb(255, 0, 0); font-weight: 700; opacity: .5 }</style><button>Error</button>"
        );
        var node = await ObserveNamedAsync(session, "Error");
        string[] properties = ["color", "font-weight", "opacity", "visibility"];

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["color"] = "rgb(255, 0, 0)",
                ["font-weight"] = "700",
                ["opacity"] = "0.5",
                ["visibility"] = "visible",
            },
            await session.ReadStyleAsync(node, properties, Agent, CancellationToken.None)
        );
        await page.Locator("button").EvaluateAsync("element => { element.style.color = 'blue'; }");
        Assert.Equal(
            new Dictionary<string, string> { ["color"] = "rgb(0, 0, 255)" },
            await session.ReadStyleAsync(node, ["color"], Agent, CancellationToken.None)
        );
        Assert.Equal("Error", await page.Locator("button").TextContentAsync());
        await page.Locator("button").EvaluateAsync("element => element.remove()");
        var stale = await Assert.ThrowsAsync<EngineException>(() =>
            session.ReadStyleAsync(node, properties, Agent, CancellationToken.None)
        );
        Assert.Equal(EngineErrorCodes.NodeStale, stale.Code);
    }

    [Fact]
    public async Task Keeps_the_element_binding_when_a_similar_element_replaces_it()
    {
        var (session, page) = await StartAsync();
        await using var _ = session;
        await page.SetContentAsync("<button>Original</button>");
        var old = await ObserveNamedAsync(session, "Original");
        await page.Locator("button").EvaluateAsync("element => { element.outerHTML = '<button>Original</button>'; }");
        Assert.Equal(
            EngineErrorCodes.NodeStale,
            (
                await Assert.ThrowsAsync<EngineException>(() =>
                    session.ReadStyleAsync(old, ["color"], Agent, CancellationToken.None)
                )
            ).Code
        );
        var current = await ObserveNamedAsync(session, "Original");
        Assert.Equal(
            EngineErrorCodes.NodeStale,
            (
                await Assert.ThrowsAsync<EngineException>(() =>
                    session.ReadStyleAsync(old, ["color"], Agent, CancellationToken.None)
                )
            ).Code
        );
        Assert.Equal(
            new Dictionary<string, string> { ["display"] = "inline-block" },
            await session.ReadStyleAsync(current, ["display"], Agent, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Reads_styles_in_a_closed_shadow_root_and_rejects_unsafe_properties_before_resolving_a_ref()
    {
        var (session, page) = await StartAsync();
        await using var _ = session;
        await page.SetContentAsync("<x-message></x-message>");
        await page.EvaluateAsync(
            "() => { document.querySelector('x-message').attachShadow({ mode: 'closed' }).innerHTML = '<button style=\"color: rgb(1, 2, 3)\">Shadow error</button>'; }"
        );
        var node = await ObserveNamedAsync(session, "Shadow error");
        Assert.Equal(
            new Dictionary<string, string> { ["color"] = "rgb(1, 2, 3)" },
            await session.ReadStyleAsync(node, ["color"], Agent, CancellationToken.None)
        );
        foreach (var style in new[] { "content", "background-image", "--token" })
        {
            var refused = await Assert.ThrowsAsync<TestException>(() =>
                session.ReadStyleAsync(new SemanticNode { Ref = "absent" }, [style], Agent, CancellationToken.None)
            );
            Assert.Equal("INVALID_ARGUMENT", refused.Code);
        }

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var cancel = await Assert.ThrowsAsync<EngineException>(() =>
            session.ReadStyleAsync(node, ["color"], Agent, cancelled.Token)
        );
        Assert.Equal(EngineErrorCodes.Cancelled, cancel.Code);
    }

    [Fact]
    public async Task Uses_the_owning_document_for_a_node_in_a_same_site_frame()
    {
        var (session, page) = await StartAsync();
        await using var _ = session;
        await page.RouteAsync("http://example.test/**", route =>
            route.FulfillAsync(
                new RouteFulfillOptions
                {
                    ContentType = "text/html",
                    Body =
                        "<iframe title=\"Error frame\" srcdoc=\"<button style='color: rgb(4, 5, 6)'>Framed error</button>\"></iframe>",
                }
            )
        );
        await page.GotoAsync("http://example.test/");
        var node = await ObserveNamedAsync(session, "Framed error");

        Assert.Equal(
            new Dictionary<string, string> { ["color"] = "rgb(4, 5, 6)" },
            await session.ReadStyleAsync(node, ["color"], Agent, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Refuses_a_locator_ref_since_style_targets_are_observation_refs()
    {
        var (session, page) = await StartAsync();
        await using var _ = session;
        await page.SetContentAsync("<button>Located</button>");
        var located = Assert.Single(
            await session.LocateAsync(
                LocatorExpression.ForRole("button", null, new RoleOptions { Exact = true }),
                TimeSpan.FromSeconds(5),
                Agent,
                CancellationToken.None
            )
        );

        var error = await Assert.ThrowsAsync<EngineException>(() =>
            session.ReadStyleAsync(located, ["color"], Agent, CancellationToken.None)
        );

        Assert.Equal(EngineErrorCodes.NodeStale, error.Code);
        Assert.False(error.Retryable);
    }

    [Fact]
    public async Task Does_not_evaluate_a_node_if_cancellation_arrives_during_reference_lookup()
    {
        var (session, page) = await StartAsync();
        await using var _ = session;
        await page.SetContentAsync("<button>Cancelled read</button>");
        var node = await ObserveNamedAsync(session, "Cancelled read");
        await page.EvaluateAsync(
            "() => { const original = window.getComputedStyle; window.evaluated = false; window.getComputedStyle = (...args) => { window.evaluated = true; return original.apply(window, args); }; }"
        );
        using var controller = new CancellationTokenSource();
        WebEngine
            .RefsOf(session)
            .OnceAfterObservedLookup(async target =>
            {
                Assert.IsType<ElementTarget>(target);
                await controller.CancelAsync();
            });

        var error = await Assert.ThrowsAsync<EngineException>(() =>
            session.ReadStyleAsync(node, ["color"], Agent, controller.Token)
        );

        Assert.Equal(EngineErrorCodes.Cancelled, error.Code);
        Assert.False(await page.EvaluateAsync<bool>("() => window.evaluated"));
    }

    [Fact]
    public async Task Reports_a_navigation_that_replaces_a_resolved_node_as_retryable_NODE_STALE()
    {
        var (session, page) = await StartAsync();
        await using var _ = session;
        await page.SetContentAsync("<button>Before navigation</button>");
        var node = await ObserveNamedAsync(session, "Before navigation");
        WebEngine.RefsOf(session).OnceAfterObservedLookup(_ => page.ReloadAsync());

        var error = await Assert.ThrowsAsync<EngineException>(() =>
            session.ReadStyleAsync(node, ["color"], Agent, CancellationToken.None)
        );

        Assert.Equal(EngineErrorCodes.NodeStale, error.Code);
        Assert.True(error.Retryable);
    }
}
