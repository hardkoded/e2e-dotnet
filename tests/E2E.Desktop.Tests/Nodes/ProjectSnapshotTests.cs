// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop;
using E2E.Engine;
using static E2E.Desktop.Tests.Raw;

namespace E2E.Desktop.Tests.Nodes;

/// <summary>Ported from <c>describe('projectSnapshot')</c> in upstream's <c>packages/desktop/tests/unit/nodes.test.ts</c>.</summary>
public sealed class ProjectSnapshotTests
{
    private static ProjectedScreen Project(string name) => DesktopNodes.ProjectSnapshot(Load(name), Minter());

    private static ProjectedScreen Project(
        RawNode tree,
        ProjectionOptions? options = null,
        SnapshotWindow? window = null
    ) => DesktopNodes.ProjectSnapshot(Snapshot(tree, window: window), Minter(), options);

    private static List<SemanticNode> All(SemanticNode node) => [node, .. node.Children.SelectMany(All)];

    private static SemanticNode? ByTestId(SemanticNode root, string testId) =>
        All(root).FirstOrDefault(node => node.TestId == testId);

    private static SemanticNode? ByName(SemanticNode root, string name) =>
        All(root).FirstOrDefault(node => node.Name == name);

    private static Rect R(double x, double y, double width, double height) => new(x, y, width, height);

    [Fact]
    public void Maps_an_AppKit_form_onto_the_contract_vocabulary()
    {
        var screen = Project("macos/appkit-form");
        var root = screen.Root;
        Assert.Equal("root", root.Ref);
        Assert.Equal("window", root.Role);
        Assert.Equal("Spike Form", root.Name);
        var username = ByTestId(root, "username")!;
        Assert.Equal("textbox", username.Role);
        Assert.Equal("Username", username.Name);
        Assert.Equal("bob", username.Value);
        Assert.True(username.States.Focused);
        var remember = ByTestId(root, "remember")!;
        Assert.Equal("checkbox", remember.Role);
        Assert.Equal("Remember me", remember.Name);
        Assert.True(remember.States.CheckedReported);
        Assert.False(remember.States.Checked);
        Assert.Null(remember.Value);
        var plan = ByTestId(root, "plan")!;
        Assert.Equal("combobox", plan.Role);
        Assert.Equal("Plan", plan.Name);
        Assert.Equal("Pro", plan.Value);
        Assert.False(plan.States.Expanded);
        var signIn = ByTestId(root, "sign-in")!;
        Assert.Equal("button", signIn.Role);
        Assert.Equal("Sign in", signIn.Name);
        var binding = screen.Bindings[signIn.Ref];
        Assert.Equal("@s1y1hxmo5pexrl:e5", binding.Ref);
        Assert.Equal(["Click", "SetFocus"], binding.Actions);
    }

    [Fact]
    public void Reads_a_headings_level_from_the_app_else_from_its_description()
    {
        var root = Project(
            Node(
                "window",
                "W",
                children:
                [
                    Node("heading", "Summary", level: 2),
                    Node("heading", "Notes", description: "heading level 3"),
                    Node("heading", "Plain"),
                ]
            )
        ).Root;
        Assert.Equal(
            [("Summary", 2), ("Notes", 3), ("Plain", null)],
            All(root).Where(node => node.Role == "heading").Select(node => (node.Name!, node.Level)).ToArray()
        );
        IReadOnlyList<SemanticNode> Heading(int level) =>
            ExpressionResolution.ResolveExpression(
                LocatorExpression.ForRole("heading", "Summary", new RoleOptions { Level = level }),
                [root]
            );
        Assert.Single(Heading(2));
        Assert.Empty(Heading(1));
    }

    [Fact]
    public void Reports_a_secure_field_as_a_password_field_with_no_value()
    {
        var root = Project("macos/appkit-form").Root;
        var password = ByTestId(root, "password")!;
        Assert.Equal("textbox", password.Role);
        Assert.Equal("Password", password.Name);
        Assert.Equal("password", password.InputPurpose);
        Assert.True(password.States.Secure);
        Assert.Null(password.Value);
    }

    [Fact]
    public void Drops_anonymous_wrappers_and_keeps_the_controls()
    {
        var root = Project("macos/sign-in-form").Root;
        Assert.DoesNotContain(All(root), node => node.Role == "group" && node.Name is null);
        var username = ByTestId(root, "username")!;
        Assert.Equal("textbox", username.Role);
        Assert.Equal("Username", username.Name);
        var password = ByTestId(root, "password")!;
        Assert.Equal("password", password.InputPurpose);
        Assert.True(password.States.Secure);
    }

    [Fact]
    public void Keeps_every_Close_button_the_driver_reports_it_leaves_the_windows_own_out_by_their_subrole()
    {
        var root = Project("macos/sign-in-form").Root;
        Assert.Equal(["Sign in"], All(root).Where(node => node.Role == "button").Select(node => node.Name!).ToArray());
        // An app's own small Close button, even in the title bar beside another, is the app's.
        var panel = Project(
            Node(
                "window",
                "Panel",
                bounds: R(100, 200, 400, 300),
                children:
                [
                    Node("button", "Close", refId: "@s:e1", bounds: R(108, 208, 16, 16)),
                    Node("button", "Minimize", refId: "@s:e3", bounds: R(131, 208, 16, 16)),
                ]
            )
        );
        Assert.Equal(
            ["Close", "Minimize"],
            All(panel.Root).Where(node => node.Role == "button").Select(node => node.Name!).ToArray()
        );
    }

    [Fact]
    public void Marks_no_id_stable_that_sits_inside_a_look_alike_whose_own_id_is_new_every_snapshot()
    {
        var row = Node("row", null, children: [Node("textfield", "Name", refId: "@s:e1")]);
        var screen = Project(Node("window", "W", children: [row, row, Node("textfield", "Search", refId: "@s:e2")]));
        bool[] Stable(string name) =>
            [
                .. screen
                    .BindingOrder.Select(id => screen.Bindings[id])
                    .Where(binding => binding.Name == name)
                    .Select(binding => binding.Stable),
            ];
        Assert.Equal([false, false], Stable("Name"));
        Assert.Equal([true], Stable("Search"));
    }

    [Fact]
    public void Answers_role_and_test_id_queries_through_the_shared_resolver()
    {
        var root = Project("macos/sign-in-form").Root;
        var field = ExpressionResolution
            .ResolveExpression(LocatorExpression.ForRole("textbox", "Username", new RoleOptions()), [root])
            .FirstOrDefault();
        Assert.Equal("username", field?.TestId);
        Assert.Single(
            ExpressionResolution.ResolveExpression(
                LocatorExpression.For("testid", "password", new TextMatchOptions(), "value"),
                [root]
            )
        );
    }

    [Fact]
    public void Reads_an_empty_text_fields_value_as_the_empty_string_so_getByDisplayValue_finds_it_and_a_password_fields_never()
    {
        var root = Project("macos/sign-in-form").Root;
        var empty = ExpressionResolution.ResolveExpression(
            new LocatorExpression { Kind = "displayValue", Value = TextMatch.FromString("") },
            [root]
        );
        Assert.Equal(["username"], empty.Select(node => node.TestId!).ToArray());
        Assert.Null(ByTestId(root, "password")?.Value);
    }

    [Fact]
    public void Gives_a_control_the_same_identity_in_every_snapshot_its_value_aside()
    {
        var mint = Minter();
        var before = DesktopNodes.ProjectSnapshot(Load("macos/appkit-form"), mint);
        var after = DesktopNodes.ProjectSnapshot(Load("macos/sign-in-form"), mint);
        Assert.Equal(ByTestId(before.Root, "username")?.Ref, ByTestId(after.Root, "username")?.Ref);
        Assert.Equal(ByTestId(before.Root, "sign-in")?.Ref, ByTestId(after.Root, "sign-in")?.Ref);
        var ids = after.Bindings.Keys.ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Gives_look_alike_siblings_new_ids_every_snapshot_so_one_never_takes_over_another()
    {
        var children = new[]
        {
            Node("button", "Delete", refId: "@s1:e1"),
            Node("button", "Delete", refId: "@s1:e2"),
            Node("button", "Done", refId: "@s1:e3"),
        };
        var mint = Minter();
        var before = DesktopNodes.ProjectSnapshot(Snapshot(Node("window", "List", children: children), "s1"), mint);
        var after = DesktopNodes.ProjectSnapshot(Snapshot(Node("window", "List", children: children[1..]), "s1"), mint);
        string[] Ids(ProjectedScreen screen, string name) =>
            [.. screen.Root.Children.Where(node => node.Name == name).Select(node => node.Ref)];
        Assert.DoesNotContain(Ids(after, "Delete"), id => Ids(before, "Delete").Contains(id));
        Assert.Equal(Ids(before, "Done"), Ids(after, "Done"));
    }

    [Fact]
    public void Keeps_an_unnamed_field_its_id_while_it_is_typed_into()
    {
        var mint = Minter();
        RawSnapshot Field(string value) =>
            Snapshot(Node("window", "Form", children: [Node("textfield", refId: "@s:e1", value: value)]));
        var ids = new[] { "", "a", "ab" }
            .Select(value => DesktopNodes.ProjectSnapshot(Field(value), mint).Root.Children[0].Ref)
            .ToArray();
        Assert.Single(ids.Distinct());
    }

    [Fact]
    public void Reports_a_value_whole_and_an_image_by_the_contract_role()
    {
        var longValue = new string('x', 600);
        var root = Project(
            Node(
                "window",
                "Form",
                children: [Node("textfield", "Notes", refId: "@s:e1", value: longValue), Node("image", "Logo")]
            )
        ).Root;
        Assert.Equal(longValue, root.Children[0].Value);
        Assert.Equal("image", root.Children[1].Role);
    }

    [Fact]
    public void Reports_the_read_only_state_toBeEditable_reads_of_a_text_field_always_of_a_combo_box_only_when_it_takes_text()
    {
        var form = Project("macos/sign-in-form").Root;
        Assert.False(ByTestId(form, "username")?.States.Readonly);
        Assert.False(ByTestId(form, "password")?.States.Readonly);
        // A pop-up menu takes no text, which is not read-only: no state, as on a device.
        Assert.Null(ByTestId(form, "plan")?.States.Readonly);
        var root = Project(
            Node(
                "window",
                "Form",
                children:
                [
                    Node("textfield", "Order id", refId: "@s:e1", states: ["readonly"], actions: ["Click"]),
                    Node("combobox", "City", refId: "@s:e2", actions: ["Click", "SetValue"]),
                    Node("textfield", "Unknown", refId: "@s:e3", actions: ["Click"]),
                ]
            )
        ).Root;
        Assert.Equal([true, false, null], root.Children.Select(node => node.States.Readonly).ToArray());
    }

    [Fact]
    public void Reads_a_control_collapsed_to_no_width_or_height_as_hidden_but_not_a_container_with_children_listed_or_cut_off()
    {
        var root = Project(
            Node(
                "window",
                "Form",
                bounds: R(0, 0, 200, 200),
                children:
                [
                    Node("button", "Collapsed", refId: "@s:e1", bounds: R(10, 10, 0, 20)),
                    Node("button", "Shown", refId: "@s:e2", bounds: R(10, 40, 80, 20)),
                    Node(
                        "group",
                        "Wrapper",
                        bounds: R(0, 0, 0, 0),
                        children: [Node("button", "Inside", refId: "@s:e3", bounds: R(10, 70, 80, 20))]
                    ),
                    Node("group", "Deep", bounds: R(0, 0, 0, 0), childrenCount: 3),
                ]
            )
        ).Root;
        Assert.True(ByName(root, "Collapsed")?.States.Hidden);
        Assert.False(ByName(root, "Shown")?.States.Hidden);
        Assert.False(ByName(root, "Wrapper")?.States.Hidden);
        Assert.False(ByName(root, "Deep")?.States.Hidden);
        Assert.False(ByName(root, "Inside")?.States.Hidden);
    }

    [Fact]
    public void Reads_everything_under_a_hidden_node_as_hidden_under_a_wrapper_it_leaves_out_too_and_swipes_none_of_it()
    {
        static RawNode Pane(string name, string reference) =>
            Node(
                "scrollarea",
                name,
                refId: reference,
                actions: ["Scroll"],
                children: [Node("button", $"{name} item", refId: $"{reference}b")]
            );
        var screen = Project(
            Node(
                "window",
                "Panes",
                children:
                [
                    Node("group", null, states: ["hidden"], children: [Pane("Drawer", "@s:e1")]),
                    Node(
                        "group",
                        "Panel",
                        states: ["hidden"],
                        children: [Node("button", "Panel item", refId: "@s:e3")]
                    ),
                    Pane("Main", "@s:e2"),
                ]
            )
        );
        foreach (var name in new[] { "Drawer", "Drawer item", "Panel", "Panel item" })
        {
            Assert.True(ByName(screen.Root, name)?.States.Hidden, name);
        }

        Assert.False(ByName(screen.Root, "Main")?.States.Hidden);
        Assert.False(ByName(screen.Root, "Main item")?.States.Hidden);
        Assert.Equal(ByName(screen.Root, "Main")?.Ref, screen.ScrollTargets?.Vertical);
    }

    [Fact]
    public void Reads_every_node_of_a_minimized_window_as_hidden_the_window_itself_too()
    {
        var root = DesktopNodes
            .ProjectSnapshot(Load("macos/sign-in-form"), Minter(), new ProjectionOptions { Minimized = true })
            .Root;
        Assert.Empty(
            ExpressionResolution.ResolveExpression(
                LocatorExpression.ForRole("window", null, new RoleOptions { Visible = true }),
                [root]
            )
        );
        Assert.All(All(root), node => Assert.True(node.States.Hidden));
    }

    [Fact]
    public void Takes_the_windows_title_from_its_accessibility_root_not_the_window_lists_app_name()
    {
        var screen = Project(Node("window", "Sign In"), window: new SnapshotWindow("w-1", "SignInForm"));
        Assert.Equal("Sign In", screen.Title);
        Assert.Equal("Untitled", Project(Node("window"), window: new SnapshotWindow("w-1", "Untitled")).Title);
    }

    [Fact]
    public void Swipes_the_first_visible_enabled_node_the_driver_offers_Scroll_past_a_list_or_table_that_cannot_scroll()
    {
        var screen = Project(
            Node(
                "window",
                "Feed",
                children:
                [
                    Node("table", "Filters", refId: "@s:e1", actions: ["Select"]),
                    Node("list", "Sidebar", refId: "@s:e2", actions: ["Select"]),
                    Node("scrollarea", "Drawer", refId: "@s:e3", states: ["offscreen"], actions: ["Scroll"]),
                    Node("scrollarea", "Archive", refId: "@s:e4", states: ["disabled"], actions: ["Scroll"]),
                    Node("group", "Posts", refId: "@s:e5", actions: ["Scroll"]),
                ]
            )
        );
        var posts = screen.Root.Children[4].Ref;
        Assert.Equal(new ScrollTargets(posts, posts), screen.ScrollTargets);
    }

    [Fact]
    public void Swipes_each_way_the_first_node_that_scrolls_that_way_past_a_carousel_for_a_swipe_down()
    {
        var screen = Project(
            Node(
                "window",
                "Store",
                children:
                [
                    Node("scrollarea", "Carousel", refId: "@s:e1", actions: ["Scroll"], scrollAxes: ["horizontal"]),
                    Node("scrollarea", "List", refId: "@s:e2", actions: ["Scroll"], scrollAxes: ["vertical"]),
                ]
            )
        );
        var carousel = screen.Root.Children[0];
        var list = screen.Root.Children[1];
        Assert.Equal(new ScrollTargets(list.Ref, carousel.Ref), screen.ScrollTargets);
    }

    [Fact]
    public void Counts_a_node_with_children_it_did_not_list_as_a_cut_short_tree()
    {
        var screen = DesktopNodes.ProjectSnapshot(
            Snapshot(Node("window", "W", children: [Node("group", "Deep", childrenCount: 3)]), complete: true),
            Minter()
        );
        Assert.True(screen.Truncated);
    }

    [Fact]
    public void Reads_a_placeholder_from_its_own_field_not_from_a_hint()
    {
        var root = Project(
            Node(
                "window",
                "W",
                children:
                [
                    Node("textfield", "Email", placeholder: "Email address", hint: "column 1 of 2", refId: "@s:e1"),
                ]
            )
        ).Root;
        var attributes = root.Children[0].Attributes;
        Assert.Equal("Email address", attributes["placeholder"]);
        Assert.Equal("column 1 of 2", attributes["hint"]);
    }

    [Fact]
    public void Reads_a_nodes_name_and_developer_id_as_its_label_and_identifier_attributes()
    {
        var root = Project(
            Node("window", "W", children: [Node("button", "Delete", nativeId: "delete", refId: "@s:e1")])
        ).Root;
        var attributes = root.Children[0].Attributes;
        Assert.Equal("Delete", attributes["label"]);
        Assert.Equal("delete", attributes["identifier"]);
    }

    [Fact]
    public void Gives_a_text_node_and_a_name_longer_than_any_control_has_no_label_so_no_paragraph_rides_along_whole_in_an_observation()
    {
        var paragraph = string.Concat(Enumerable.Repeat("word ", 20_000));
        var root = Project(
            Node(
                "window",
                "W",
                children:
                [
                    Node("statictext", value: paragraph),
                    Node("button", paragraph, refId: "@s:e1"),
                    Node("statictext", value: "Signed in"),
                ]
            )
        ).Root;
        // A short text gets none either: its name is its text, which textContent reads.
        Assert.All(root.Children, child => Assert.False(child.Attributes.ContainsKey("label")));
    }

    [Fact]
    public void Keeps_ids_for_siblings_that_share_a_developer_id_but_not_a_name()
    {
        var mint = Minter();
        static RawNode Item(string name, int index) =>
            Node("menuitem", name, nativeId: "menuItemSelected:", refId: $"@m:e{index}", actions: ["Click"]);
        var menu = Snapshot(Node("menu", children: [Item("Free", 1), Item("Pro", 2), Item("Team", 3)]), "m");
        string[] Ids(ProjectedScreen screen) =>
            [.. All(screen.Root).Where(node => node.Role == "menuitem").Select(node => node.Ref)];
        var first = Ids(DesktopNodes.ProjectSnapshot(menu, mint));
        var second = Ids(DesktopNodes.ProjectSnapshot(menu, mint));
        Assert.Equal(3, first.Distinct().Count());
        Assert.Equal(first, second);
    }

    [Fact]
    public void Keeps_a_status_its_id_while_its_text_changes()
    {
        var mint = Minter();
        RawSnapshot Status(string text) =>
            Snapshot(Node("window", "W", children: [Node("statictext", value: text, nativeId: "status")]));
        Assert.Equal(
            DesktopNodes.ProjectSnapshot(Status("Not signed in"), mint).Root.Children[0].Ref,
            DesktopNodes.ProjectSnapshot(Status("Signed in"), mint).Root.Children[0].Ref
        );
    }

    [Fact]
    public void Never_lets_a_developer_id_and_a_name_spell_another_controls_identity()
    {
        var raw = Snapshot(
            Node(
                "window",
                "W",
                children:
                [
                    Node("button", "Edit:", nativeId: "shared", refId: "@s:e1"),
                    Node("button", "Copy", nativeId: "shared", refId: "@s:e2"),
                    Node("button", "Archived", nativeId: "shared:Edit", refId: "@s:e3"),
                ]
            )
        );
        var ids = DesktopNodes.ProjectSnapshot(raw, Minter()).Root.Children.Select(node => node.Ref).ToArray();
        Assert.Equal(3, ids.Distinct().Count());
    }

    [Fact]
    public void Keeps_a_secure_field_a_wrapper_would_hide_so_its_bounds_are_masked()
    {
        var screen = Project(
            Node(
                "window",
                "W",
                bounds: R(0, 0, 100, 100),
                children: [Node("group", states: ["secure"], bounds: R(10, 10, 50, 20))]
            )
        );
        Assert.Equal([R(10, 10, 50, 20)], screen.SecureRects);
    }

    [Fact]
    public void Withholds_masking_for_a_secure_field_whose_box_has_no_area()
    {
        var screen = Project(
            Node(
                "window",
                "W",
                bounds: R(0, 0, 100, 100),
                children: [Node("textfield", states: ["secure"], refId: "@s:e1", bounds: R(10, 10, 0, 0))]
            )
        );
        Assert.Null(screen.SecureRects);
    }

    [Fact]
    public void Gives_a_control_its_title_as_its_text_and_an_input_none()
    {
        var root = Project("macos/sign-in-form").Root;
        var signIn = ByTestId(root, "sign-in")!;
        Assert.Equal("Sign in", signIn.Name);
        Assert.Equal("Sign in", signIn.Text);
        Assert.Null(ByTestId(root, "username")?.Text);
    }

    [Fact]
    public void Withholds_masking_when_a_secure_field_has_no_bounds_and_measures_rects_from_the_window()
    {
        Assert.Null(Project("macos/appkit-form").SecureRects);
        var raw = Load("macos/appkit-form");
        var bounded = raw with
        {
            Tree = raw.Tree with
            {
                Bounds = R(100, 50, 400, 300),
                Children = [.. raw.Tree.Children!.Select(child => child with { Bounds = R(120, 90, 260, 22) })],
            },
        };
        var screen = DesktopNodes.ProjectSnapshot(bounded, Minter());
        Assert.Equal(400, screen.ViewportWidth);
        Assert.Equal(300, screen.ViewportHeight);
        Assert.Equal([R(20, 40, 260, 22)], screen.SecureRects);
    }

    [Fact]
    public void Measures_every_rect_from_the_window_on_a_real_capture()
    {
        var screen = Project("macos/sign-in-form");
        Assert.Equal(420, screen.ViewportWidth);
        Assert.Equal(352, screen.ViewportHeight);
        Assert.Equal([R(19, 110, 262, 26)], screen.SecureRects);
        Assert.NotNull(screen.WindowId);
        var status = ByTestId(screen.Root, "status")!;
        Assert.Equal("text", status.Role);
        Assert.Equal("Not signed in", status.Name);
        Assert.Equal("Not signed in", status.Text);
        Assert.Equal(19, status.Rect!.X);
        Assert.Equal(230, status.Rect.Y);
    }
}
