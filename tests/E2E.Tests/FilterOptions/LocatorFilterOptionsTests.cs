// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Tests.FilterOptions;

/// <summary>
/// <c>Filter</c> takes the text or the locator as an argument, with no options object. So the upstream its that refuse a
/// key it does not implement (<c>visible</c>) and a <c>null</c> or inherited options
/// object cannot be written, and are not ported. The <c>query options</c> describe is not ported for the same reason:
/// <c>RoleOptions</c> and <c>TextMatchOptions</c> are typed.
/// </summary>
public sealed class LocatorFilterOptionsTests
{
    private static readonly SemanticNode Paid = new() { Ref = "paid", Role = "row", Text = "Invoice 41 Paid" };
    private static readonly SemanticNode Due = new() { Ref = "due", Role = "row", Text = "Invoice 42 Due" };

    [Fact]
    public async Task Keeps_hasText_as_the_only_predicate_when_it_is_the_only_key()
    {
        var screen = ScreenFixture.Create(Paid, Due);

        var rows = screen.GetByRole("row").Filter("Invoice");

        Assert.Equal(["Invoice 41 Paid", "Invoice 42 Due"], await rows.AllTextContentsAsync());
        Assert.Equal("Invoice 42 Due", await screen.GetByRole("row").Filter("Due").TextContentAsync());
    }

    [Fact]
    public async Task Excludes_rows_by_hasNotText_alone_beside_hasText_and_as_a_RegExp()
    {
        var rows = ScreenFixture.Create(Paid, Due).GetByRole("row");

        Assert.Equal(["Invoice 42 Due"], await rows.Filter(hasNotText: "paid").AllTextContentsAsync());
        Assert.Equal(["Invoice 42 Due"], await rows.Filter(hasText: "invoice", hasNotText: new Regex("paid$", RegexOptions.IgnoreCase)).AllTextContentsAsync());
        Assert.Equal(0, await rows.Filter(hasNotText: new Regex("Invoice")).CountAsync());
        Assert.Equal(2, await rows.Filter(hasNotText: "unrelated").CountAsync());
        Assert.Equal(0, await rows.Filter(hasNotText: "").CountAsync());
    }

    [Fact]
    public async Task Drops_rows_whose_subtree_resolves_hasNot_alone_and_beside_hasText()
    {
        static SemanticNode Badge(string id, string text) => new() { Ref = id, Role = "status", Name = text, Text = text };
        var screen = ScreenFixture.Create(
            new SemanticNode { Ref = "paid", Role = "row", Text = "Invoice 41 Paid", Children = [Badge("paid-badge", "Paid")] },
            new SemanticNode { Ref = "due", Role = "row", Text = "Invoice 42 Due", Children = [Badge("due-badge", "Due")] });
        var all = screen.GetByRole("row");

        Assert.Equal(["Invoice 42 Due"], await all.Filter(hasNot: screen.GetByRole("status", "Paid")).AllTextContentsAsync());
        Assert.Equal(["Invoice 41 Paid"], await all.Filter(hasText: "Invoice", hasNot: screen.GetByRole("status", "Due")).AllTextContentsAsync());
        Assert.Equal(2, await all.Filter(hasNot: screen.GetByRole("status", "Missing")).CountAsync());
    }

    [Fact]
    public void Rejects_absent_options_as_an_empty_filter()
    {
        var rows = ScreenFixture.Create(Paid, Due).GetByRole("row");

        var error = Assert.Throws<TestException>(() => rows.Filter());

        Assert.Equal("INVALID_LOCATOR", error.Code);
        Assert.Equal("filter() requires hasText, hasNotText, has, and/or hasNot", error.Message);
    }

    /// <summary>Partial: an absent argument is an <see cref="ArgumentNullException"/>, not an INVALID_LOCATOR error with the message "filter() requires hasText, hasNotText, has, and/or hasNot".</summary>
    [Fact]
    public void Rejects_an_absent_filter_with_an_argument_error()
    {
        var rows = ScreenFixture.Create(Paid, Due).GetByRole("row");

        Assert.Equal("hasText", Assert.Throws<ArgumentNullException>(() => rows.Filter((TextMatch)null!)).ParamName);
        Assert.Equal("has", Assert.Throws<ArgumentNullException>(() => rows.Filter((Locator)null!)).ParamName);
    }
}
