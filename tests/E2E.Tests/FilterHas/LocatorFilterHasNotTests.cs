// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.FilterHas;

/// <summary>
/// The <c>locator.filter({ hasNot })</c> describe of upstream's filter-has test. The port resolves a locator over the observed
/// tree and has no engine <c>locate</c> to hand an expression to. "refuses a hasNot that is not an e2e locator before any engine
/// call" is not ported: <c>Filter</c> takes a typed <see cref="Locator"/>, and a null is an <see cref="ArgumentNullException"/>.
/// </summary>
public sealed class LocatorFilterHasNotTests
{
    private static readonly SemanticNode Remove = new() { Ref = "remove", Role = "button", Name = "Remove" };
    private static readonly SemanticNode Note = new() { Ref = "note", Role = "text", Text = "Read only" };
    private static readonly SemanticNode Alpha = new() { Ref = "alpha", Role = "listitem", Text = "Alpha", Children = [Remove] };
    private static readonly SemanticNode Beta = new() { Ref = "beta", Role = "listitem", Text = "Beta", Children = [Note] };

    /// <summary>Partial: there is no engine call to hand the expression to, so the test reads the query the locator builds and the count the screen resolves from it.</summary>
    [Fact]
    public async Task Partial_Hands_the_engine_one_filter_expression_with_the_inner_locator_under_hasNot_unchanged()
    {
        var screen = ScreenFixture.Create(Alpha, Beta);
        var source = screen.GetByRole("listitem");
        var inner = screen.GetByRole("button", "Remove");

        var filtered = source.Filter(hasNot: inner);

        Assert.Equal("filter", filtered.Query.Kind);
        Assert.Same(source.Query, filtered.Query.Source);
        Assert.Same(inner.Query, filtered.Query.HasNot);
        Assert.Equal(1, await filtered.CountAsync());
    }

    [Fact]
    public async Task Keeps_the_items_whose_subtree_does_not_resolve_the_inner_locator()
    {
        var screen = ScreenFixture.Create(Alpha, Beta);
        var items = screen.GetByRole("listitem");

        Assert.Equal(["Beta"], await items.Filter(hasNot: screen.GetByRole("button", "Remove")).AllTextContentsAsync());
        Assert.Equal(1, await items.Filter(has: screen.GetByRole("button", "Remove"), hasNot: screen.GetByText("Read only")).CountAsync());
    }

    [Fact]
    public async Task Names_the_whole_chain_when_a_filtered_read_finds_nothing()
    {
        var screen = ScreenFixture.Create(Alpha, Beta);

        var error = await Assert.ThrowsAsync<TestException>(
            () => screen.GetByRole("listitem").Filter(has: screen.GetByRole("link"), hasNot: screen.GetByRole("button")).TextContentAsync());

        Assert.Equal("NOT_FOUND", error.Code);
        Assert.Contains("getByRole(\"listitem\").filter(has: getByRole(\"link\"), hasNot: getByRole(\"button\"))", error.Message, StringComparison.Ordinal);
    }
}
