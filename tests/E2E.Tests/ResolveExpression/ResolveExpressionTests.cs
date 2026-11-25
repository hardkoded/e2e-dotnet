// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Tests.ResolveExpression;

/// <summary>
/// The reference locator semantics. The screen is an iOS Settings page as the
/// mobile engine projects one: an application node, a navigation bar with a
/// button, a cell echoing its static text, a switch, two fields, a hidden button.
/// Not ported: "refuses a role query whose value is not a string" (the port's role is a typed string), "hands a
/// selector to the platform hook, and refuses one without it" (no selector queries), and "rejects frames, since a
/// semantic tree has no nested documents" (no frame queries).
/// </summary>
public sealed class ResolveExpressionTests
{
    private static readonly SemanticNode[] Settings =
    [
        new()
        {
            Ref = "app",
            Role = "application",
            Name = "Settings",
            Text = "Settings",
            Children =
            [
                new()
                {
                    Ref = "bar",
                    Role = "navigation",
                    Name = "General",
                    Text = "General",
                    Children =
                    [
                        new()
                        {
                            Ref = "back",
                            Role = "button",
                            Name = "Back",
                            Text = "Back",
                            TestId = "BackButton",
                        },
                    ],
                },
                new()
                {
                    Ref = "about",
                    Role = "listitem",
                    Name = "About",
                    Text = "About",
                    TestId = "ABOUT",
                    Children =
                    [
                        new()
                        {
                            Ref = "about-text",
                            Role = "text",
                            Name = "About",
                            Text = "About",
                        },
                    ],
                },
                new()
                {
                    Ref = "airplane",
                    Role = "switch",
                    Name = "Airplane Mode",
                    Text = "Airplane Mode",
                    States = new NodeStates { Checked = false },
                },
                new()
                {
                    Ref = "search",
                    Role = "textbox",
                    Name = "Search",
                    Text = "Search",
                    Value = "wifi",
                },
                new()
                {
                    Ref = "password",
                    Role = "textbox",
                    Name = "Password",
                    Text = "Password",
                    InputPurpose = "password",
                    States = new NodeStates { Secure = true },
                },
                new()
                {
                    Ref = "hidden",
                    Role = "button",
                    Name = "Hidden",
                    Text = "Hidden",
                    States = new NodeStates { Hidden = true, Disabled = true },
                },
                new()
                {
                    Ref = "scroller",
                    Role = "listitem",
                    Name = "Scroller",
                    Text = "Scroller",
                },
            ],
        },
    ];

    private static readonly Screen Screen = ScreenFixture.Create(Settings);

    [Fact]
    public async Task Answers_role_queries_with_name_filters_and_skips_hidden_nodes()
    {
        Assert.Equal(["back"], await Ids(Screen.GetByRole("button")));
        Assert.Equal(["back"], await Ids(Screen.GetByRole("button", "Back")));
        Assert.Equal(["back"], await Ids(Screen.GetByRole("button", new Regex("^ba", RegexOptions.IgnoreCase))));
        Assert.Empty(await Ids(Screen.GetByRole("switch", new RoleOptions { Checked = true })));
        Assert.Equal(["airplane"], await Ids(Screen.GetByRole("switch", new RoleOptions { Checked = false })));
        Assert.Equal(["about", "scroller"], await Ids(Screen.GetByRole("listitem")));
    }

    [Fact]
    public async Task Requires_a_heading_level_exactly_so_a_tree_without_levels_answers_nothing()
    {
        var headings = ScreenFixture.Create(
            new SemanticNode
            {
                Ref = "h1",
                Role = "heading",
                Level = 1,
            },
            new SemanticNode
            {
                Ref = "h2",
                Role = "heading",
                Level = 2,
            },
            new SemanticNode { Ref = "h", Role = "heading" }
        );
        Assert.Equal(["h2"], await Ids(headings.GetByRole("heading", new RoleOptions { Level = 2 })));
        Assert.Equal(["h1", "h2", "h"], await Ids(headings.GetByRole("heading")));
        Assert.Empty(await Ids(Screen.GetByRole("listitem", new RoleOptions { Level = 1 })));
    }

    /// <summary>Partial: the port's role options cannot ask for the hidden state, so the role part is not checked.</summary>
    [Fact]
    public async Task Drops_hidden_nodes_from_the_text_label_and_index_queries_when_it_says_visible()
    {
        Assert.Equal(["hidden"], await Ids(Screen.GetByText("Hidden")));
        Assert.Empty(await Ids(Screen.GetByText("Hidden", new TextMatchOptions { Visible = true })));
        Assert.Equal(["search"], await Ids(Screen.GetByLabel("Search", new TextMatchOptions { Visible = true })));
        Assert.Equal(
            ["about-text"],
            await Ids(Screen.GetByText("About", new TextMatchOptions { Visible = true }).Last())
        );
    }

    [Fact]
    public async Task Answers_label_text_display_value_placeholder_and_test_id_queries()
    {
        Assert.Equal(["search"], await Ids(Screen.GetByLabel("Search")));
        Assert.Equal(["about-text"], await Ids(Screen.GetByText("About")));
        Assert.Equal(["about-text"], await Ids(Screen.GetByText("abo", exact: false)));
        Assert.Equal(["search"], await Ids(Screen.GetByDisplayValue("wifi")));
        Assert.Empty(await Ids(Screen.GetByDisplayValue("hunter2")));
        Assert.Equal(["about"], await Ids(Screen.GetByTestId("ABOUT")));
        Assert.Empty(await Ids(Screen.GetByPlaceholder("anything")));
        // The port's node carries the placeholder as a field; upstream's carries it as an attribute.
        var field = ScreenFixture.Create(
            new SemanticNode
            {
                Ref = "email",
                Role = "textbox",
                Placeholder = "you@example.test",
            }
        );
        Assert.Equal(["email"], await Ids(field.GetByPlaceholder("you@example.test")));
    }

    [Fact]
    public async Task Answers_text_and_label_queries_with_the_innermost_match_when_an_ancestor_echoes_the_text()
    {
        Assert.Equal(
            ["text"],
            (await Screen.GetByText("About").ResolveAsync(CancellationToken.None)).Select(match => match.Role)
        );
        Assert.Equal(["about-text"], await Ids(Screen.GetByLabel("About")));

        // The echoing cell still answers role queries, and filters by its subtree text.
        Assert.Equal(["about"], await Ids(Screen.GetByRole("listitem", "About")));
        Assert.Equal(["about"], await Ids(Screen.GetByRole("listitem").Filter("About")));
    }

    [Fact]
    public async Task Reads_text_from_the_name_or_the_visible_text()
    {
        var nodes = ScreenFixture.Create(
            new SemanticNode
            {
                Ref = "named",
                Role = "button",
                Name = "Save",
            },
            new SemanticNode { Ref = "texted", Text = "Save" },
            new SemanticNode { Ref = "other", Name = "Cancel" }
        );
        Assert.Equal(["named", "texted"], await Ids(nodes.GetByText("Save")));
        Assert.Equal(["named"], await Ids(nodes.GetByLabel("Save")));
    }

    [Fact]
    public async Task Scopes_filters_and_indexes()
    {
        Assert.Equal(["back"], await Ids(Screen.GetByRole("navigation").GetByRole("button")));
        Assert.Equal(["about"], await Ids(Screen.GetByRole("listitem").Filter("About")));
        Assert.Equal(["about"], await Ids(Screen.GetByRole("listitem").Filter(Screen.GetByRole("text"))));
        Assert.Equal(["about"], await Ids(Screen.GetByRole("listitem").First()));
        Assert.Equal(["scroller"], await Ids(Screen.GetByRole("listitem").Last()));
        Assert.Empty(await Ids(Screen.GetByRole("listitem").Nth(5)));
    }

    [Fact]
    public async Task Searches_strict_descendants_of_a_scope_each_once_in_document_order()
    {
        Assert.Empty(await Ids(Screen.GetByRole("application").GetByRole("application")));
        var nested = ScreenFixture.Create(
            new SemanticNode
            {
                Ref = "outer",
                Role = "group",
                Children =
                [
                    new()
                    {
                        Ref = "inner",
                        Role = "group",
                        Children = [new() { Ref = "leaf", Role = "button" }],
                    },
                    new() { Ref = "sibling", Role = "button" },
                ],
            }
        );
        Assert.Equal(["leaf", "sibling"], await Ids(nested.GetByRole("group").GetByRole("button")));
        Assert.Empty(await Ids(nested.GetByRole("list").GetByRole("button")));
    }

    [Fact]
    public async Task Resolves_has_inside_each_candidate_never_against_the_whole_screen()
    {
        var list = ScreenFixture.Create(
            new SemanticNode
            {
                Ref = "alpha",
                Role = "listitem",
                Text = "Alpha",
                Children =
                [
                    new()
                    {
                        Ref = "remove",
                        Role = "button",
                        Name = "Remove",
                    },
                ],
            },
            new SemanticNode
            {
                Ref = "beta",
                Role = "listitem",
                Text = "Beta",
                Children =
                [
                    new()
                    {
                        Ref = "note",
                        Role = "text",
                        Text = "Read only",
                    },
                ],
            }
        );
        var items = list.GetByRole("listitem");
        Assert.Equal(["alpha"], await Ids(items.Filter(list.GetByRole("button", "Remove"))));
        Assert.Equal(["beta"], await Ids(items.Filter(list.GetByText("Read only"))));
        Assert.Empty(await Ids(items.Filter(list.GetByRole("link"))));
        Assert.Equal(["alpha"], await Ids(items.Filter("Alpha").Filter(list.GetByRole("button"))));
        Assert.Empty(await Ids(items.Filter("Beta").Filter(list.GetByRole("button"))));
    }

    [Fact]
    public async Task Matches_hasText_against_the_name_text_or_value_of_the_node_or_a_descendant()
    {
        var rows = ScreenFixture.Create(
            new SemanticNode
            {
                Ref = "by-value",
                Role = "row",
                Children =
                [
                    new()
                    {
                        Ref = "field",
                        Role = "textbox",
                        Value = "draft",
                    },
                ],
            },
            new SemanticNode
            {
                Ref = "by-name",
                Role = "row",
                Name = "draft",
            },
            new SemanticNode
            {
                Ref = "other",
                Role = "row",
                Text = "final",
            }
        );
        Assert.Equal(["by-value", "by-name"], await Ids(rows.GetByRole("row").Filter("draft")));
        Assert.Equal(["other"], await Ids(rows.GetByRole("row").Filter(new Regex("FIN", RegexOptions.IgnoreCase))));
    }

    [Fact]
    public async Task Keeps_every_innermost_text_match_however_deep_or_far_apart_the_echoes_are()
    {
        var echoes = ScreenFixture.Create(
            new SemanticNode
            {
                Ref = "screen",
                Role = "application",
                Name = "Save",
                Children =
                [
                    new()
                    {
                        Ref = "form",
                        Name = "Save",
                        Children =
                        [
                            new()
                            {
                                Ref = "row",
                                Name = "Save",
                                Children = [new() { Ref = "label", Text = "Save" }],
                            },
                            new() { Ref = "note", Text = "Draft" },
                        ],
                    },
                    new()
                    {
                        Ref = "footer",
                        Children =
                        [
                            new()
                            {
                                Ref = "button",
                                Name = "Save",
                                Children = [new() { Ref = "icon", Text = "Draft" }],
                            },
                        ],
                    },
                ],
            },
            new SemanticNode { Ref = "toast", Text = "Save" }
        );
        Assert.Equal(["label", "button", "toast"], await Ids(echoes.GetByText("Save")));
        // Inside a scope the scope's own echo is not a candidate, and its innermost match still wins.
        Assert.Empty(await Ids(echoes.GetByText("Draft").GetByText("Save")));
        Assert.Equal(["label", "button"], await Ids(echoes.GetByRole("application").GetByText("Save")));
    }

    [Fact]
    public async Task Resolves_a_nested_has_inside_the_subtree_of_each_enclosing_match()
    {
        var table = ScreenFixture.Create(
            new SemanticNode
            {
                Ref = "t1",
                Role = "table",
                Children =
                [
                    new()
                    {
                        Ref = "r1",
                        Role = "row",
                        Children =
                        [
                            new()
                            {
                                Ref = "c1",
                                Role = "cell",
                                Text = "Paid",
                            },
                        ],
                    },
                    new()
                    {
                        Ref = "r2",
                        Role = "row",
                        Children =
                        [
                            new()
                            {
                                Ref = "c2",
                                Role = "cell",
                                Text = "Due",
                            },
                            new()
                            {
                                Ref = "b2",
                                Role = "button",
                                Name = "Pay",
                            },
                        ],
                    },
                ],
            },
            new SemanticNode
            {
                Ref = "t2",
                Role = "table",
                Children =
                [
                    new()
                    {
                        Ref = "r3",
                        Role = "row",
                        Children =
                        [
                            new()
                            {
                                Ref = "c3",
                                Role = "cell",
                                Text = "Due",
                            },
                        ],
                    },
                ],
            }
        );
        var dueRows = table.GetByRole("row").Filter(has: table.GetByText("Due"));
        var payable = dueRows.Filter(has: table.GetByRole("button"));
        Assert.Equal(["t1"], await Ids(table.GetByRole("table").Filter(has: payable)));
        Assert.Equal(["t2"], await Ids(table.GetByRole("table").Filter(has: dueRows, hasNot: payable)));
        Assert.Equal(
            ["t1"],
            await Ids(table.GetByRole("table").Filter(has: table.GetByText("Paid").Or(table.GetByRole("button"))))
        );
        // A union inside a scope answers in document order, each node once.
        Assert.Equal(["c1", "c2", "b2", "c3"], await Ids(table.GetByRole("button").Or(table.GetByRole("cell"))));
    }

    [Fact]
    public void Hands_a_selector_inside_has_only_the_descendants_of_each_candidate()
    {
        var seen = new List<string[]>();
        var items = new SemanticNode[]
        {
            new()
            {
                Ref = "a",
                Role = "listitem",
                Children = [new() { Ref = "a1" }],
            },
            new() { Ref = "b", Role = "listitem" },
        };
        var expression = new LocatorExpression
        {
            Kind = "filter",
            Source = Screen.GetByRole("listitem").Query,
            Has = new LocatorExpression { Kind = "selector", Selector = "*" },
        };

        var matches = ExpressionResolution.ResolveExpression(
            expression,
            items,
            new ResolveExpressionOptions
            {
                Selector = (_, candidates) =>
                {
                    seen.Add([.. candidates.Select(candidate => candidate.Ref)]);
                    return candidates;
                },
            }
        );

        Assert.Equal(["a"], matches.Select(match => match.Ref));
        Assert.Equal(2, seen.Count);
        Assert.Equal(["a1"], seen[0]);
        Assert.Empty(seen[1]);
    }

    private static async Task<IEnumerable<string>> Ids(Locator locator) =>
        (await locator.ResolveAsync(CancellationToken.None)).Select(match => match.Ref);
}
