// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Decision.Tests.Elements.ElementsHelpers;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.Elements;

/// <summary><c>describe('element table')</c> of <c>elements.test.ts</c>.</summary>
public sealed class ElementTableTests
{
    private static ExecutorNode[] Children(int count, Func<int, ExecutorNode> make) =>
        [.. Enumerable.Range(0, count).Select(make)];

    private static ActionSpace SpaceOf(
        StepExecutorContext ctx,
        IReadOnlyList<ExecutorNode> tree,
        string path,
        bool canType,
        ExecutorPixels? pixels = null,
        bool locates = false
    ) =>
        global::E2E.Decision.Elements.ActionSpace(
            ctx,
            new SpaceObservation
            {
                Path = path,
                Viewport = new TraceViewport(800, 600),
                Tree = tree,
                Pixels = pixels,
                Locates = locates,
            },
            canType
        );

    [Fact]
    public void Offers_tap_on_buttons_and_links_with_indexed_keys()
    {
        var space = SpaceFor([Node("a", "button", "Add"), Node("b", "link", "Home")]);
        Assert.Equal(
            [("1", "button", "Add"), ("2", "link", "Home")],
            space.Elements.Select(element => (element.Index, element.Role, element.Label))
        );
        Assert.Equal(["1", "2"], Keys(space, Operation.Tap));
    }

    [Fact]
    public void Keeps_checkbox_radio_and_switch_under_check_only()
    {
        var space = SpaceFor([
            Node("a", "checkbox", "Milk", states: new NodeStates { Checked = false }),
            Node("b", "radio", "R", states: new NodeStates { Checked = false }),
            Node("c", "switch", "S"),
        ]);
        Assert.False(space.Targets.ContainsKey(Operation.Tap));
        Assert.Equal(["1", "2", "3"], Keys(space, Operation.Check));
        Assert.Equal(false, space.Elements[0].Checked);
        Assert.Contains(Operation.Check, space.Elements[0].Operations);
        Assert.DoesNotContain(Operation.Tap, space.Elements[0].Operations);
    }

    [Fact]
    public void Shows_the_selected_state_of_a_tab_so_the_model_can_tell_which_one_is_active()
    {
        var space = SpaceFor([
            Node("a", "tab", "Profile", states: new NodeStates { Selected = true }),
            Node("b", "tab", "Settings", states: new NodeStates { Selected = false }),
        ]);
        Assert.Equal(
            [("Profile", (bool?)true), ("Settings", false)],
            space.Elements.Select(element => (element.Label, element.Selected))
        );
    }

    [Fact]
    public void Never_offers_check_on_a_checked_radio_which_would_uncheck_it()
    {
        var space = SpaceFor([
            Node("on", "radio", "Express", states: new NodeStates { Checked = true }),
            Node("off", "radio", "Standard", states: new NodeStates { Checked = false }),
            Node("box", "checkbox", "Gift", states: new NodeStates { Checked = true }),
        ]);
        Assert.Equal(["Standard", "Gift"], space.Elements.Select(element => element.Label));
        Assert.Equal(["1", "2"], Keys(space, Operation.Check));
        Assert.Equal("Express (checked)", space.PageText);
    }

    [Fact]
    public void Offers_a_native_select_only_under_select_with_per_option_keys()
    {
        var space = SpaceFor([
            Node("s", "combobox", "Size", children: [Node("o1", "option", "Small"), Node("o2", "option", "Large")]),
        ]);
        Assert.False(space.Targets.ContainsKey(Operation.Tap));
        Assert.False(space.Targets.ContainsKey(Operation.Type));
        Assert.Equal(["1:0", "1:1"], Keys(space, Operation.Select));
        Assert.Contains(Operation.Select, space.Elements[0].Operations);
        Assert.DoesNotContain(Operation.Tap, space.Elements[0].Operations);
        Assert.DoesNotContain(Operation.DoubleTap, space.Elements[0].Operations);
    }

    [Fact]
    public void Skips_hidden_and_disabled_options()
    {
        var space = SpaceFor([
            Node(
                "s",
                "combobox",
                "Size",
                children:
                [
                    Node("o1", "option", "Small"),
                    Node("o2", "option", "Hidden", states: new NodeStates { Hidden = true }),
                    Node("o3", "option", "Off", states: new NodeStates { Disabled = true }),
                ]
            ),
        ]);
        Assert.Equal(["1:0"], Keys(space, Operation.Select));
    }

    [Fact]
    public void Caps_select_options_per_question_and_leaves_them_out_of_omitted()
    {
        var space = SpaceFor(
            Children(260, index =>
                Node(
                    "s" + index,
                    "combobox",
                    "Size " + index,
                    children: [Node("a" + index, "option", "Small"), Node("b" + index, "option", "Large")]
                )
            )
        );
        Assert.Equal(254, space.Elements.Count);
        Assert.Equal(254, space.Targets[Operation.Select].Count);
        // Only the 6 rows past the element cap count: scrolling can reveal a
        // row, never an option the per-question cap dropped.
        Assert.Equal(6, space.Omitted);
    }

    [Fact]
    public void Keeps_password_fields_under_typeSecret_only()
    {
        var space = SpaceFor([Node("p", "textbox", "Password", inputPurpose: "password")]);
        Assert.False(space.Targets.ContainsKey(Operation.Type));
        Assert.False(space.Targets.ContainsKey(Operation.Submit));
        Assert.Equal(["1"], Keys(space, Operation.TypeSecret));
    }

    [Fact]
    public void Skips_hidden_and_disabled_nodes_and_their_text_stays_out_of_the_page()
    {
        var space = SpaceFor([
            Node("a", "button", "Gone", states: new NodeStates { Hidden = true }),
            Node("b", "button", "Off", states: new NodeStates { Disabled = true }),
            Node("c", "heading", "Todos"),
        ]);
        // The heading is no control; it stays on the page text and takes only pointer operations.
        var element = Assert.Single(space.Elements);
        Assert.Equal("Todos", element.Label);
        Assert.Equal(["hover", "secondary_tap"], Names(element.Operations));
        Assert.False(space.Targets.ContainsKey(Operation.Tap));
        Assert.Equal("Todos", space.PageText);
    }

    [Fact]
    public void Offers_pointer_operations_on_labeled_passive_nodes_and_drag_onto_any_of_them()
    {
        var space = SpaceFor([
            Node("menu", text: "Card actions"),
            Node("list", "list", "Todo column", children: [Node("card", "listitem", "Design review")]),
            Node("done", "region", "Done column"),
            Node("label", text: ""),
        ]);
        Assert.Equal(
            ["Card actions", "Todo column", "Design review", "Done column"],
            space.Elements.Select(element => element.Label)
        );
        Assert.Equal(["1", "2", "3", "4"], Keys(space, Operation.Hover));
        Assert.Equal(["1", "2", "3", "4"], Keys(space, Operation.SecondaryTap));
        Assert.Equal(["1", "2", "3", "4"], Keys(space, Operation.Drag));
        Assert.Equal(
            [("1", "Card actions"), ("2", "Todo column"), ("3", "Design review"), ("4", "Done column")],
            space.Destinations.Select(pair => (pair.Key, pair.Value.Label))
        );
        Assert.Equal("Card actions\nTodo column\nDesign review\nDone column", space.PageText);
        var noDrop = SpaceFor([Node("menu", text: "Card actions")]);
        Assert.False(noDrop.Targets.ContainsKey(Operation.Drag));
        Assert.DoesNotContain(Operation.Drag, noDrop.Elements[0].Operations);
    }

    [Fact]
    public void Offers_scroll_to_only_on_nodes_outside_the_viewport()
    {
        var space = SpaceFor([
            Node("in", "heading", "Top", rect: new BoundingBox(0, 10, 100, 20)),
            Node("out", "paragraph", "Footnote", rect: new BoundingBox(0, 5000, 100, 20)),
        ]);
        Assert.Equal(["2"], Keys(space, Operation.ScrollTo));
    }

    [Fact]
    public void Offers_upload_on_a_file_input_with_a_text_model_never_tap()
    {
        var file = new Dictionary<string, string> { ["type"] = "file" };
        var space = SpaceFor([Node("f", "button", "Attachments", attributes: file)]);
        Assert.Equal(["1"], Keys(space, Operation.Upload));
        Assert.False(space.Targets.ContainsKey(Operation.Tap));
        Assert.False(space.Targets.ContainsKey(Operation.DoubleTap));
        var noText = SpaceOf(Context().Ctx, [Node("f", "button", "Attachments", attributes: file)], "/form", false);
        Assert.False(noText.Targets.ContainsKey(Operation.Upload));
    }

    [Fact]
    public void Offers_tap_at_only_with_pixels_a_step_that_locates_and_the_tapAt_verb()
    {
        var fixture = Context();
        var pixels = new ExecutorPixels([], "image/png", 800, 600, 1, 0);
        var space = SpaceOf(fixture.Ctx, [Node("a", "button", "Add")], "/", true, pixels, true);
        Assert.True(space.TapAt);
        Assert.Equal(Operation.TapAt, space.OperationNamed("tap_at"));
        Assert.False(space.Targets.ContainsKey(Operation.TapAt));
        var noPixels = SpaceFor([Node("a", "button", "Add")]);
        Assert.False(noPixels.TapAt);
        Assert.Null(noPixels.OperationNamed("tap_at"));
        var noLocate = SpaceOf(fixture.Ctx, [], "/", true, pixels);
        Assert.False(noLocate.TapAt);
        var noVerb = SpaceOf(
            Context(new ContextOptions { Verbs = VerbSet(ExecutorVerb.Tap) }).Ctx,
            [],
            "/",
            true,
            pixels,
            true
        );
        Assert.False(noVerb.TapAt);
    }

    [Fact]
    public void Types_the_operation_and_control_an_answer_names()
    {
        var space = SpaceFor([Node("a", "button", "Add")]);
        Assert.Equal(Operation.Tap, space.OperationNamed("tap"));
        Assert.Null(space.OperationNamed("upload"));
        Assert.Equal(Control.Back, space.ControlNamed("back"));
        Assert.Null(space.ControlNamed("tap"));
    }

    // Partial: the port tree lists the page's top-level nodes with no synthetic root, so the upstream root (a named
    // region holding one button) is a pointable node here and a drag is offered; only the single-node and cap checks hold.
    [Fact]
    public void Partial_Never_offers_drag_with_no_second_node_to_drop_on_nor_onto_the_root_or_past_the_cap()
    {
        var single = SpaceFor([Node("go", "button", "Go")]);
        Assert.False(single.Targets.ContainsKey(Operation.Drag));
        Assert.DoesNotContain(Operation.Drag, single.Elements[0].Operations);
        var children = Children(256, index =>
            Node("b" + index, "button", "Button " + index, rect: new BoundingBox(0, 10, 50, 20))
        );
        var capped = SpaceFor([.. children, Node("done", "region", "Done column")]);
        Assert.Equal(254, capped.Elements.Count);
        Assert.DoesNotContain("done", capped.Destinations.Values.Select(destination => destination.Id));
    }

    [Fact]
    public void Uses_the_placeholder_as_the_label_when_there_is_no_name()
    {
        var space = SpaceFor([
            Node("q", "searchbox", attributes: new Dictionary<string, string> { ["placeholder"] = "Search todos" }),
        ]);
        Assert.Equal("Search todos", space.Elements[0].Label);
        Assert.Contains(Operation.Type, space.Elements[0].Operations);
    }

    [Fact]
    public void Keeps_a_named_nodes_text_in_the_page_text_when_it_differs_from_the_name()
    {
        var space = SpaceFor([
            Node("g", "status", "Greeting", text: "Welcome back,\n  admin!"),
            Node("h", "heading", "Todos", text: "Todos"),
            Node("p", "paragraph", text: "1 remaining"),
        ]);
        Assert.Equal("Greeting text=\"Welcome back, admin!\"\nTodos\n1 remaining", space.PageText);
    }

    [Fact]
    public void Never_offers_verbs_the_target_lacks()
    {
        var space = SpaceFor([Node("a", "button", "Add"), Node("b", "textbox", "Name")], VerbSet(ExecutorVerb.Tap));
        Assert.False(space.Targets.ContainsKey(Operation.Type));
        Assert.Empty(space.Controls);
        // A field takes a tap too: it focuses the field and opens what the app attaches there.
        Assert.Equal(["1", "2"], Keys(space, Operation.Tap));
    }

    [Fact]
    public void Caps_the_table_past_254_viewport_first_and_counts_the_omitted()
    {
        // Out-of-viewport nodes come first in tree order, so only a real
        // viewport-first sort puts the ten visible rows at the head.
        var space = SpaceFor(
            Children(300, index =>
                Node(
                    "n" + index,
                    "button",
                    "Button " + index,
                    rect: new BoundingBox(0, index < 290 ? 5000 : 10, 50, 20)
                )
            )
        );
        Assert.Equal(254, space.Elements.Count);
        Assert.Equal(46, space.Omitted);
        Assert.Equal(
            Enumerable.Range(0, 10).Select(position => "Button " + (290 + position)),
            space.Elements.Take(10).Select(element => element.Label)
        );
        Assert.Equal(254, Keys(space, Operation.Tap).Count);
    }

    [Fact]
    public void Keeps_every_target_question_on_a_dense_page_within_255_choices_none_included()
    {
        var fixture = Context();
        var buttons = Children(300, index => Node("b" + index, "button", "Button " + index));
        var selects = Children(300, index =>
            Node("s" + index, "combobox", "Size " + index, children: [Node("o" + index, "option", "Small")])
        );
        foreach (
            var (operation, children) in new[]
            {
                (Operation.Tap, buttons),
                (Operation.Drag, buttons),
                (Operation.Select, selects),
            }
        )
        {
            var space = SpaceOf(fixture.Ctx, children, "/runs", true);
            var request = Questions.TargetRequest(fixture.Ctx, space, operation, [], "/runs");
            foreach (var question in request.Questions.Values)
            {
                var criteria = question.Choices?.Select(choice => choice.Value).ToList() ?? [];
                Assert.Equal(255, criteria.Count);
                Assert.Contains("none", criteria);
            }
        }
    }

    [Fact]
    public void Offers_scroll_and_back_controls_when_the_engine_declares_them()
    {
        var full = SpaceFor([Node("a", "button", "Add")]);
        Assert.Equal(
            ["back", "scroll_down", "scroll_up"],
            full.Controls.Select(WireNames.Of).Order(StringComparer.Ordinal)
        );
        var none = SpaceFor([Node("a", "button", "Add")], VerbSet(ExecutorVerb.Tap));
        Assert.Empty(none.Controls);
    }

    [Fact]
    public void Offers_dismiss_keyboard_only_while_the_tree_lists_a_keyboard()
    {
        // An iOS number pad lists its keys with no keyboard node above them.
        ExecutorNode[] numberPad =
        [
            Node("reps", "textbox", "Reps", value: "8"),
            Node("pad", "other", children: [Node("k1", "key", "1"), Node("k2", "key", "Delete")]),
        ];
        Assert.Contains(Control.DismissKeyboard, SpaceFor(numberPad).Controls);
        Assert.Contains(Control.DismissKeyboard, SpaceFor([Node("kb", "keyboard")]).Controls);
        Assert.DoesNotContain(Control.DismissKeyboard, SpaceFor([Node("a", "button", "Add")]).Controls);
        Assert.DoesNotContain(
            Control.DismissKeyboard,
            SpaceFor([Node("kb", "keyboard", states: new NodeStates { Hidden = true })]).Controls
        );
        var hiddenPad = Node(
            "pad",
            "other",
            states: new NodeStates { Hidden = true },
            children: [Node("k1", "key", "1")]
        );
        Assert.DoesNotContain(Control.DismissKeyboard, SpaceFor([hiddenPad]).Controls);
        Assert.DoesNotContain(
            Control.DismissKeyboard,
            SpaceFor(numberPad, VerbSet(ExecutorVerb.Tap, ExecutorVerb.Type, ExecutorVerb.Scroll)).Controls
        );
    }

    [Fact]
    public void Takes_the_keyboard_state_the_engine_measured_over_the_tree_and_falls_back_to_the_tree_without_one()
    {
        var field = Node("reps", "textbox", "Reps", value: "8");
        // A keyboard the tree does not list, measured by the engine.
        Assert.Contains(Control.DismissKeyboard, SpaceFor([field], null, true).Controls);
        // Key nodes left in the tree after the engine measured the keyboard gone.
        Assert.DoesNotContain(Control.DismissKeyboard, SpaceFor([field, Node("k1", "key", "1")], null, false).Controls);
        Assert.Contains(Control.DismissKeyboard, SpaceFor([field, Node("k1", "key", "1")]).Controls);
        Assert.DoesNotContain(
            Control.DismissKeyboard,
            SpaceFor([field], VerbSet(ExecutorVerb.Tap, ExecutorVerb.Type, ExecutorVerb.Scroll), true).Controls
        );
    }
}
