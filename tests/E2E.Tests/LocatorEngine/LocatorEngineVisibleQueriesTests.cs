// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.LocatorEngine;

/// <summary>
/// A role query never matches a hidden node, so the getByRole row of "without visible keeps the hidden twin, so the pair
/// is LOCATOR_AMBIGUOUS" is not ported (COMPATIBILITY, Locators). The two tests that hand the query to an engine of its own run on a
/// <see cref="E2E.LocatorEngine"/> over a locate, as upstream's <c>makeLocatorEngineOverEngine</c> does.
/// </summary>
public sealed class LocatorEngineVisibleQueriesTests
{
    /// <summary>A visible node and its hidden twin, as an engine that ignores <c>visible</c> would report them.</summary>
    private static readonly SemanticNode[] Twins =
    [
        new() { Ref = "shown", Role = "button", Name = "Save", Text = "Save", Placeholder = "Save", Value = "Save", TestId = "save" },
        new() { Ref = "hidden", Role = "button", Name = "Save", Text = "Save", Placeholder = "Save", Value = "Save", TestId = "save", States = new NodeStates { Hidden = true } },
    ];

    private static Screen ScreenOver(IReadOnlyList<SemanticNode> nodes, List<string> tapped) => new(
        _ => Task.FromResult(new Observation { Route = "/", Roots = nodes }),
        (node, _, _) =>
        {
            tapped.Add(node.Ref);
            return Task.CompletedTask;
        },
        () => CancellationToken.None,
        () => { },
        TimeSpan.FromMilliseconds(300),
        TimeSpan.FromMilliseconds(300));

    private static Locator Query(Screen screen, string kind, bool visible)
    {
        var options = new TextMatchOptions { Visible = visible };
        return kind switch
        {
            "getByRole" => screen.GetByRole("button", new RoleOptions { Name = "Save", Visible = visible }),
            "getByText" => screen.GetByText("Save", options),
            "getByLabel" => screen.GetByLabel("Save", options),
            "getByPlaceholder" => screen.GetByPlaceholder("Save", options),
            "getByDisplayValue" => screen.GetByDisplayValue("Save", options),
            "getByTestId" => screen.GetByTestId("save", options),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    [Theory]
    [InlineData("getByRole")]
    [InlineData("getByText")]
    [InlineData("getByLabel")]
    [InlineData("getByPlaceholder")]
    [InlineData("getByDisplayValue")]
    [InlineData("getByTestId")]
    public async Task With_visible_true_resolves_the_shown_twin_and_ignores_the_hidden_one(string kind)
    {
        var tapped = new List<string>();
        var screen = ScreenOver(Twins, tapped);

        await Query(screen, kind, visible: true).TapAsync();

        Assert.Equal(["shown"], tapped);
        Assert.Equal(1, await Query(screen, kind, visible: true).CountAsync());
    }

    [Theory]
    [InlineData("getByText")]
    [InlineData("getByLabel")]
    [InlineData("getByPlaceholder")]
    [InlineData("getByDisplayValue")]
    [InlineData("getByTestId")]
    public async Task Without_visible_keeps_the_hidden_twin_so_the_pair_is_LOCATOR_AMBIGUOUS(string kind)
    {
        var screen = ScreenOver(Twins, []);

        var error = await Assert.ThrowsAsync<TestException>(() => Query(screen, kind, visible: false).TapAsync());

        Assert.Equal("LOCATOR_AMBIGUOUS", error.Code);
        Assert.Equal(2, await Query(screen, kind, visible: false).CountAsync());
    }

    [Fact]
    public async Task Names_the_predicate_in_the_ambiguity_message_when_two_visible_nodes_remain()
    {
        var screen = ScreenOver(
            [new SemanticNode { Ref = "a", Role = "button", Name = "Save", Text = "Save" }, new SemanticNode { Ref = "b", Role = "button", Name = "Save", Text = "Save" }],
            []);

        var error = await Assert.ThrowsAsync<TestException>(() => screen.GetByText("Save", new TextMatchOptions { Visible = true }).TapAsync());

        Assert.Equal("LOCATOR_AMBIGUOUS", error.Code);
        Assert.Contains("getByText(\"Save\", visible: true)", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NET_only_a_text_query_resolves_a_shown_label_over_the_zero_sized_control_inside_it()
    {
        // Upstream's web tree lists no box-less node; this one does, so the text query leaves it out of the innermost pick.
        var control = new SemanticNode { Ref = "input", Role = "textbox", Name = "Click me", Rect = new BoundingBox(68, 8, 0, 0) };
        var label = new SemanticNode { Ref = "label", Text = "Click me", Rect = new BoundingBox(8, 8, 60, 18), Children = [control] };
        var tapped = new List<string>();
        var screen = ScreenOver([label], tapped);

        await screen.GetByText("Click me").TapAsync();

        Assert.Equal(["label"], tapped);
    }

    /// <summary>A locator engine over an engine that answers every locate with <paramref name="nodes"/>, and keeps the expressions it was asked.</summary>
    private static E2E.LocatorEngine EngineAnswering(IReadOnlyList<SemanticNode> nodes, List<LocatorExpression> expressions) => new(new LocatorEngineOptions
    {
        Observe = _ => Task.FromResult(new Observation { Route = "/", Roots = [] }),
        Locate = (expression, _, _) =>
        {
            expressions.Add(expression);
            return Task.FromResult(nodes);
        },
        Perform = (_, _, _, _) => Task.CompletedTask,
        Cancellation = () => CancellationToken.None,
        ActionTimeout = TimeSpan.FromMilliseconds(1_000),
        AssertionTimeout = TimeSpan.FromMilliseconds(1_000),
    });

    [Fact]
    public async Task Keeps_nodes_whose_engine_reports_no_visibility_at_all()
    {
        var engine = EngineAnswering([new SemanticNode { Ref = "unknown", Role = "button", Name = "Save" }], []);
        var query = ScreenOver([], []).GetByRole("button", new RoleOptions { Name = "Save", Visible = true }).Query;

        var node = await engine.ResolveExactlyOneAsync(query, new Deadline(TimeSpan.FromMilliseconds(5_000), TimeProvider.System), CancellationToken.None);

        Assert.Equal("unknown", node.Ref);
    }

    [Fact]
    public async Task Hands_a_visible_query_under_an_index_to_the_engine_unchanged_only_it_can_filter_before_nth()
    {
        var expressions = new List<LocatorExpression>();
        var engine = EngineAnswering(Twins, expressions);
        var indexed = ScreenOver([], []).GetByText("Save", new TextMatchOptions { Visible = true }).First().Query;

        // The adapter cannot know which of the engine's answers came first, so it must not second-guess them; the engine applied
        // `visible` before `first`.
        var matches = await engine.ResolveNowAsync(indexed, CancellationToken.None, engine.Reads(engine.DeadlineFor()));

        Assert.Equal(2, matches.Count);
        Assert.Equal(indexed, expressions[0]);
    }
}
