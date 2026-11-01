// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.LocatorBasics.Items;

namespace E2E.Tests.LocatorUnion;

/// <summary>
/// <c>locator.Or(other)</c>: core builds one union expression and the engine resolves it. The screen here resolves with
/// the reference semantics: matches of either branch in traversal order, each node once.
/// Partial: "refuses ... a non-locator" (the parameter is typed, so only the other screen's locator is refusable).
/// </summary>
public sealed class LocatorOrTests
{
    private static readonly SemanticNode[] Tabs =
    [
        new() { Ref = "discover", Role = "tab", Name = "Discover", Text = "Discover" },
        new() { Ref = "odkrywaj", Role = "tab", Name = "Odkrywaj", Text = "Odkrywaj" },
    ];

    /// <summary>A screen over the two tabs, logging every expression it is asked to locate.</summary>
    private static (Screen Screen, List<LocatorExpression> Expressions) TabScreen()
    {
        var observation = new Observation { Route = "/", Roots = Tabs };
        var expressions = new List<LocatorExpression>();
        var screen = new Screen(
            _ => Task.FromResult(observation),
            (_, _, _) => Task.CompletedTask,
            () => CancellationToken.None,
            () => { },
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(50),
            locate: (expression, _, _) =>
            {
                expressions.Add(expression);
                return Task.FromResult(LocatorResolver.Resolve(observation, expression));
            });
        return (screen, expressions);
    }

    [Fact]
    public async Task Keeps_traversal_order_over_branch_order_and_counts_an_overlapping_node_once()
    {
        var (screen, _) = TabScreen();
        var discover = screen.GetByRole("tab", "Discover");
        var odkrywaj = screen.GetByRole("tab", "Odkrywaj");
        Assert.Equal(["Discover", "Odkrywaj"], await odkrywaj.Or(discover).AllTextContentsAsync());
        Assert.Equal(1, await discover.Or(discover).CountAsync());
        Assert.Equal("Discover", await odkrywaj.Or(discover).First().TextContentAsync());
    }

    [Fact]
    public async Task Stays_strict_when_both_branches_match_naming_both_in_the_error()
    {
        var (screen, _) = TabScreen();
        var either = screen.GetByRole("tab", "Discover").Or(screen.GetByRole("tab", "Odkrywaj"));
        var error = await FailsWith("LOCATOR_AMBIGUOUS", () => either.TextContentAsync());
        Assert.Contains("getByRole(\"tab\", \"Discover\").or(getByRole(\"tab\", \"Odkrywaj\"))", error.Message);
    }

    [Fact]
    public async Task Composes_with_filter_and_matches_nothing_when_neither_branch_does()
    {
        var (screen, _) = TabScreen();
        Assert.Equal(1, await screen.GetByRole("link").Or(screen.GetByRole("tab")).Filter("Discover").CountAsync());
        Assert.Equal(0, await screen.GetByRole("link").Or(screen.GetByRole("button")).CountAsync());
    }

    /// <summary>Partial: the parameter is typed, so the port cannot pass a non-locator; <c>null</c> is an <see cref="ArgumentNullException"/>, not <c>INVALID_LOCATOR</c>.</summary>
    [Fact]
    public void Partial_Refuses_another_screens_locator_before_any_engine_call()
    {
        var left = TabScreen();
        var right = TabScreen();
        var error = Assert.Throws<TestException>(() => left.Screen.GetByRole("tab").Or(right.Screen.GetByRole("tab")));
        Assert.Equal("INVALID_LOCATOR", error.Code);
        Assert.Empty(left.Expressions);
        Assert.Empty(right.Expressions);
    }
}
