// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;
using static E2E.Mobile.Tests.RawNodes;

namespace E2E.Mobile.Tests.Locate;

public sealed class LocatorExpressionsOverADeviceSnapshotTests
{
    private static TextMatch Exact(string value) => TextMatch.FromString(value);

    private static LocatorExpression Role(string role, TextMatch? name = null, RoleOptions? options = null) =>
        LocatorExpression.ForRole(role, name, options ?? new RoleOptions());

    private static LocatorExpression Text(string kind, string value, bool visible = false, bool exact = true) =>
        LocatorExpression.For(kind, Exact(value), new TextMatchOptions { Exact = exact, Visible = visible }, nameof(value));

    private static List<string> Names(LocatorExpression expression)
    {
        var index = Project(SettingsNodes).Index;
        return [.. DeviceLocator.ResolveExpression(expression, index, MobilePlatform.Ios).Select(entry => entry.Node.Name ?? "")];
    }

    [Fact]
    public void Answers_role_queries_with_name_filters_and_skips_hidden_nodes()
    {
        Assert.Equal(["Back"], Names(Role("button")));
        Assert.Equal(["Back"], Names(Role("button", Exact("Back"))));
        Assert.Equal(["Back"], Names(Role("button", TextMatch.FromRegex(new Regex("^ba", RegexOptions.IgnoreCase)))));
        Assert.Empty(Names(Role("switch", options: new RoleOptions { Checked = true })));
        Assert.Equal(["Airplane Mode"], Names(Role("switch", options: new RoleOptions { Checked = false })));
    }

    [Fact]
    public void Drops_hidden_nodes_from_any_query_kind_when_it_says_visible()
    {
        Assert.Equal(["Hidden"], Names(Text("text", "Hidden")));
        Assert.Empty(Names(Text("text", "Hidden", visible: true)));

        // Upstream also names `states: { hidden: true }` on this role query. A role query here has no hidden state.
        Assert.Equal(["Back"], Names(Role("button", options: new RoleOptions { Visible = true })));
        Assert.Equal(["Search"], Names(Text("label", "Search", visible: true)));
        Assert.Equal(["About"], Names(new LocatorExpression { Kind = "index", Source = Text("text", "About", visible: true), Index = LocatorExpression.LastIndex }));
    }

    [Fact]
    public void Answers_label_text_display_value_and_test_id_queries()
    {
        Assert.Equal(["Search"], Names(Text("label", "Search")));
        Assert.Equal(["About"], Names(Text("text", "About")));
        Assert.Equal(["About"], Names(Text("text", "abo", exact: false)));
        Assert.Equal(["Search"], Names(Text("displayValue", "wifi")));
        Assert.Empty(Names(Text("displayValue", "hunter2")));
        Assert.Equal(["About"], Names(Text("testid", "ABOUT")));
        Assert.Empty(Names(Text("placeholder", "anything")));
    }

    [Fact]
    public void Answers_text_and_label_queries_with_the_innermost_match_when_an_ancestor_echoes_the_text()
    {
        var index = Project(SettingsNodes).Index;
        var matches = DeviceLocator.ResolveExpression(Text("text", "About"), index, MobilePlatform.Ios);
        Assert.Equal(["text"], matches.Select(entry => entry.Node.Role));
        Assert.Equal(["text"], DeviceLocator.ResolveExpression(Text("label", "About"), index, MobilePlatform.Ios).Select(entry => entry.Node.Role));

        // The echoing cell still answers role queries, and filters by its subtree text.
        Assert.Equal(["About"], Names(Role("listitem", Exact("About"))));
        Assert.Equal(["About"], Names(new LocatorExpression { Kind = "filter", Source = Role("listitem"), HasText = Exact("About"), HasTextExact = true }));
    }

    [Fact]
    public void Resolves_a_label_query_to_the_text_field_inside_the_host_view_iOS_wraps_a_React_Native_input_in()
    {
        var index = Project(
            N("@e1", index: 0, depth: 0, type: "Application", label: "Benchmark"),
            N("@e2", index: 1, parentIndex: 0, depth: 1, type: "Other", label: "Name field"),
            N("@e3", index: 2, parentIndex: 1, depth: 2, type: "TextField", label: "Name field", value: "Ada", identifier: "name-input"),
            N("@e4", index: 3, parentIndex: 0, depth: 1, type: "Other", label: "Passphrase field"),
            N("@e5", index: 4, parentIndex: 3, depth: 2, type: "SecureTextField", label: "Passphrase field")).Index;
        var field = DeviceLocator.ResolveExpression(Text("label", "Name field"), index, MobilePlatform.Ios);
        Assert.Equal([("textbox", "name-input")], field.Select(entry => (entry.Node.Role!, entry.Node.TestId!)));
        var pattern = LocatorExpression.For("label", TextMatch.FromRegex(new Regex("field$")), new TextMatchOptions(), "value");
        Assert.Equal(["textbox", "textbox"], DeviceLocator.ResolveExpression(pattern, index, MobilePlatform.Ios).Select(entry => entry.Node.Role));

        // A role query keeps both, since the host view answers to no vocabulary role of its own.
        Assert.Equal(2, DeviceLocator.ResolveExpression(Role("textbox"), index, MobilePlatform.Ios).Count);
    }

    [Fact]
    public void Scopes_filters_and_indexes()
    {
        var inBar = Role("button") with { Parent = Role("navigation") };
        Assert.Equal(["Back"], Names(inBar));
        var cellsWithAbout = new LocatorExpression { Kind = "filter", Source = Role("listitem"), HasText = Exact("About"), HasTextExact = true };
        Assert.Equal(["About"], Names(cellsWithAbout));
        var cellsHavingText = new LocatorExpression { Kind = "filter", Source = Role("listitem"), Has = Role("text") };
        Assert.Equal(["About"], Names(cellsHavingText));
        Assert.Equal(["Scroller"], Names(new LocatorExpression { Kind = "index", Source = Role("listitem"), Index = LocatorExpression.LastIndex }));
        Assert.Empty(Names(new LocatorExpression { Kind = "index", Source = Role("listitem"), Index = 5 }));
    }

    // .NET-only: upstream's text pattern carries its exactness, which the port's TextMatch does not, so the expression does (HasTextExact).
    [Fact]
    public void A_hasText_filter_is_a_case_insensitive_substring_unless_the_expression_says_exact()
    {
        var substring = new LocatorExpression { Kind = "filter", Source = Role("listitem"), HasText = Exact("abou") };
        Assert.Equal(["About"], Names(substring));
        Assert.Empty(Names(substring with { HasTextExact = true }));
    }

    [Fact]
    public void Unions_matches_in_traversal_order_without_duplicates()
    {
        var back = Role("button");
        var search = Text("label", "Search");
        Assert.Equal(["Back", "Search"], Names(new LocatorExpression { Kind = "union", Left = search, Right = back }));
        Assert.Equal(["Back"], Names(new LocatorExpression { Kind = "union", Left = back, Right = back }));
        Assert.Equal(["Back"], Names(new LocatorExpression { Kind = "index", Source = new LocatorExpression { Kind = "union", Left = search, Right = back }, Index = 0 }));
    }

    [Fact]
    public void Routes_agent_device_selectors_to_the_selector_matcher_and_rejects_frames()
    {
        static LocatorExpression Selector(string selector) => new() { Kind = "selector", Selector = selector };

        Assert.Equal(["About"], Names(Selector("id=ABOUT")));
        Assert.Equal(["Airplane Mode"], Names(Selector("id=NOPE || label=\"Airplane Mode\"")));
        Assert.Matches("invalid agent-device selector", Assert.Throws<EngineException>(() => Names(Selector("@e3"))).Message);
        var frame = Assert.Throws<EngineException>(() => Names(new LocatorExpression { Kind = "frame", Selector = "iframe", Source = Role("button") }));
        Assert.Equal("FRAME_NOT_FOUND", frame.Code);
    }
}
