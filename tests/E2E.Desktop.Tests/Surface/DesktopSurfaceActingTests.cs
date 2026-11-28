// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E.Desktop;
using E2E.Engine;

namespace E2E.Desktop.Tests.Surface;

/// <summary>Ported from <c>describe('DesktopSurface: acting')</c> in upstream's <c>packages/desktop/tests/unit/surface.test.ts</c>.</summary>
public sealed class DesktopSurfaceActingTests : SurfaceTestBase
{
    private static readonly LaunchOptions Headed = new()
    {
        Observe = false,
        Options = new DesktopOptions { Headed = true },
    };

    private static List<SemanticNode> Every(SemanticNode node) => [node, .. node.Children.SelectMany(Every)];

    private static object Box(int x, int y, int width, int height) =>
        new
        {
            x,
            y,
            width,
            height,
        };

    private static Scripted Placed() =>
        Windows(_ =>
            [
                new
                {
                    id = "w-mine",
                    is_focused = true,
                    bounds = Box(0, 0, 200, 200),
                },
            ]
        );

    private static Answer Form(params object[] children) =>
        Screen(
            "w-mine",
            new
            {
                name = "Form",
                bounds = Box(0, 0, 200, 200),
                children,
            }
        );

    private static async Task<SemanticNode> Observed(DesktopSurface surface, Func<SemanticNode, bool> match) =>
        Every((await surface.ObserveAsync(Operation(), null)).Roots[0]).First(match);

    private static string[] Actions(FakeDriver fake) =>
        [.. fake.Of("act").Select(call => ((ActRequest)call.Args[0]!).Action)];

    [Fact]
    public async Task Fills_a_field_with_the_value_as_a_call_argument_never_through_another_process()
    {
        var (surface, fake, byTestId) = await Launched();
        await surface.PerformAsync(byTestId("password"), new LocatorAction.Fill("hunter2", true), Operation());
        var fill = Last<ActRequest>(fake, "act")!;
        Assert.Equal("@s1y1hxmo5pexrl:e2", fill.Ref);
        Assert.Equal("setValue", fill.Action);
        Assert.Equal("hunter2", fill.Value);
        // The driver's own wait is what is left of the operation's budget.
        Assert.True(fill.TimeoutMs > 9_000);
        Assert.True(fill.TimeoutMs <= 10_000);
    }

    [Fact]
    public async Task Leaves_whether_a_box_needs_checking_to_the_driver_which_reads_its_live_state()
    {
        var (surface, fake, byTestId) = await Launched();
        await surface.PerformAsync(byTestId("remember"), new LocatorAction.Check(), Operation());
        await surface.PerformAsync(byTestId("remember"), new LocatorAction.Uncheck(), Operation());
        Assert.Equal(["check", "uncheck"], fake.Acted());
    }

    [Fact]
    public async Task Unchecks_a_mixed_checkbox_whose_state_is_neither_checked_nor_unchecked()
    {
        var mixed = Replaced(
            SnapshotAnswer("macos/appkit-form"),
            "\"value\":\"0\"",
            "\"value\":\"2\",\"states\":[\"indeterminate\"]",
            all: false
        );
        var (surface, fake, byTestId) = await Launched(new Scenario { ["snapshot"] = [mixed] });
        await surface.PerformAsync(byTestId("remember"), new LocatorAction.Uncheck(), Operation());
        Assert.Equal(["uncheck"], fake.Acted());
    }

    [Fact]
    public async Task Maps_each_control_action_onto_the_driver_action_that_performs_it()
    {
        var (surface, fake, byTestId) = await Launched(
            [],
            new LaunchOptions { Options = new DesktopOptions { Headed = true } }
        );
        var signIn = byTestId("sign-in");
        LocatorAction[] kinds =
        [
            new LocatorAction.Tap(),
            new LocatorAction.DoubleTap(),
            new LocatorAction.SecondaryTap(),
            new LocatorAction.Focus(),
            new LocatorAction.Clear(),
            new LocatorAction.Hover(),
        ];
        foreach (var kind in kinds)
        {
            await surface.PerformAsync(signIn, kind, Operation());
        }

        Assert.Equal(["click", "doubleClick", "rightClick", "focus", "clear", "hover"], fake.Acted());
    }

    [Fact]
    public async Task Refuses_to_act_on_the_window_but_to_swipe_or_bring_it_into_view()
    {
        var (surface, _, _) = await Launched();
        await Rejects(surface.PerformAsync("root", new LocatorAction.Tap(), Operation()), "NOT_ACTIONABLE");
    }

    [Fact]
    public async Task Refuses_to_act_on_a_node_the_driver_gave_no_ref()
    {
        var read = Form(
            new
            {
                role = "statictext",
                value = "Hello",
                bounds = Box(10, 10, 50, 20),
            }
        );
        var (surface, fake, _) = await Launched(
            new Scenario { ["snapshot"] = [read] },
            new LaunchOptions { Observe = false }
        );
        var observed = await surface.ObserveAsync(Operation(), null);
        await Rejects(
            surface.PerformAsync(observed.Roots[0].Children[0].Ref, new LocatorAction.Tap(), Operation()),
            "NOT_ACTIONABLE"
        );
        Assert.Empty(Inputs(fake));
    }

    [Fact]
    public async Task Double_clicks_a_node_with_no_ref_only_while_a_fresh_read_still_holds_it_where_it_was_never_what_replaced_it()
    {
        static Answer FormOf(object child) =>
            Screen(
                "w-mine",
                new
                {
                    name = "Form",
                    bounds = Box(0, 0, 200, 200),
                    children = new[] { child },
                }
            );
        var label = FormOf(
            new
            {
                bounds = Box(10, 10, 50, 20),
                role = "statictext",
                value = "Hello",
            }
        );
        var replaced = FormOf(
            new
            {
                bounds = Box(10, 10, 50, 20),
                role = "button",
                name = "Delete",
                ref_id = "@s:e9",
            }
        );
        var swapped = await Launched(new Scenario { ["snapshot"] = [label, label, replaced] }, Headed);
        var hello = (await swapped.Surface.ObserveAsync(Operation(), null)).Roots[0].Children[0];
        await Rejects(
            swapped.Surface.PerformAsync(hello.Ref, new LocatorAction.DoubleTap(), Operation()),
            "NODE_STALE",
            retryable: true
        );
        Assert.Empty(Inputs(swapped.Fake));
        var still = await Launched(new Scenario { ["snapshot"] = [label], ["listWindows"] = [Placed()] }, Headed);
        var shown = (await still.Surface.ObserveAsync(Operation(), null)).Roots[0].Children[0];
        await still.Surface.PerformAsync(shown.Ref, new LocatorAction.DoubleTap(), Operation());
        Assert.Equal(["focusWindow", "pointer"], Inputs(still.Fake));
    }

    [Fact]
    public async Task Refuses_a_tap_on_a_control_the_read_found_off_screen_and_scrolls_one_into_view_first_when_it_can()
    {
        static Answer At(bool hidden, string[] actions, string role = "button") =>
            Form(
                hidden
                    ? new
                    {
                        role,
                        name = "Top",
                        ref_id = "@s:e1",
                        available_actions = actions,
                        states = new[] { "offscreen" },
                        bounds = Box(10, -40, 50, 20),
                    }
                    : (object)
                        new
                        {
                            role,
                            name = "Top",
                            ref_id = "@s:e1",
                            available_actions = actions,
                            bounds = Box(10, -40, 50, 20),
                        }
            );
        // No scroll action: accessibility would press it all the same, out of the user's reach.
        var stuck = await Launched(
            new Scenario { ["snapshot"] = [At(true, ["Click"])] },
            new LaunchOptions { Observe = false }
        );
        var top = await Observed(stuck.Surface, node => node.Name == "Top");
        await Rejects(
            stuck.Surface.PerformAsync(top.Ref, new LocatorAction.Tap(), Operation()),
            "NOT_ACTIONABLE",
            "not on screen"
        );
        Assert.Empty(stuck.Fake.Of("act"));
        // A scroll action: the driver scrolls it into view and answers once it shows, then it is pressed. A look-alike that the scroll moved is not
        // looked for again by its place, which changed.
        var twins = Form(
            new
            {
                role = "button",
                name = "Delete",
                ref_id = "@s:e1",
                available_actions = new[] { "Click", "ScrollTo" },
                bounds = Box(10, 10, 50, 20),
            },
            new
            {
                role = "button",
                name = "Delete",
                ref_id = "@s:e2",
                available_actions = new[] { "Click", "ScrollTo" },
                states = new[] { "offscreen" },
                bounds = Box(10, 400, 50, 20),
            }
        );
        var scrolls = await Launched(new Scenario { ["snapshot"] = [twins] }, new LaunchOptions { Observe = false });
        var second = Every((await scrolls.Surface.ObserveAsync(Operation(), null)).Roots[0])
            .Where(node => node.Name == "Delete")
            .ElementAt(1);
        await scrolls.Surface.PerformAsync(second.Ref, new LocatorAction.Tap(), Operation());
        var acts = scrolls.Fake.Of("act").Select(call => (ActRequest)call.Args[0]!).ToArray();
        Assert.Equal([("@s:e2", "scrollTo"), ("@s:e2", "click")], acts.Select(act => (act.Ref, act.Action)).ToArray());
        // The scroll changed the app: a click that then fails, even as a stale ref, is never retried as if nothing happened.
        var moved = await Launched(
            new Scenario { ["snapshot"] = [twins], ["act"] = [Ok(null), Failure("STALE_REF", "not_delivered")] },
            new LaunchOptions { Observe = false }
        );
        var stale = Every((await moved.Surface.ObserveAsync(Operation(), null)).Roots[0])
            .Where(node => node.Name == "Delete")
            .ElementAt(1);
        await Rejects(
            moved.Surface.PerformAsync(stale.Ref, new LocatorAction.Tap(), Operation()),
            "ACTION_MAY_HAVE_COMMITTED",
            retryable: false
        );
        // A secondary tap goes to the driver alone, which checks the context menu live before it scrolls: the observed actions may come from a
        // read that stalled.
        var plain = await Launched(new Scenario { ["snapshot"] = [twins] }, new LaunchOptions { Observe = false });
        var hiddenPlain = Every((await plain.Surface.ObserveAsync(Operation(), null)).Roots[0])
            .Where(node => node.Name == "Delete")
            .ElementAt(1);
        await plain.Surface.PerformAsync(hiddenPlain.Ref, new LocatorAction.SecondaryTap(), Operation());
        Assert.Equal(["rightClick"], plain.Fake.Acted());
        // A check goes to the driver, which reads the box first: one already right gets nothing, and one it must press out of view is scrolled into
        // view or refused there.
        var box = await Launched(
            new Scenario { ["snapshot"] = [At(true, ["Click", "Check"], "checkbox")] },
            new LaunchOptions { Observe = false }
        );
        var hiddenBox = await Observed(box.Surface, node => node.Name == "Top");
        await box.Surface.PerformAsync(hiddenBox.Ref, new LocatorAction.Check(), Operation());
        Assert.Equal(["check"], Actions(box.Fake));
        // A button in a row the list holds out of view is hidden by the scroll area alone: it is scrolled into view too.
        var inRow = Form(
            new
            {
                role = "group",
                name = "Row",
                states = new[] { "offscreen" },
                bounds = Box(0, 300, 200, 30),
                children = new object[]
                {
                    new
                    {
                        role = "button",
                        name = "Remove",
                        ref_id = "@s:e3",
                        available_actions = new[] { "Click", "ScrollTo" },
                        states = new[] { "offscreen" },
                        bounds = Box(10, 305, 50, 20),
                    },
                },
            }
        );
        var nested = await Launched(new Scenario { ["snapshot"] = [inRow] }, new LaunchOptions { Observe = false });
        var remove = await Observed(nested.Surface, node => node.Name == "Remove");
        await nested.Surface.PerformAsync(remove.Ref, new LocatorAction.Tap(), Operation());
        Assert.Equal(["scrollTo", "click"], Actions(nested.Fake));
        // Hidden for another reason than a scroll: no scroll shows a minimized window's control or one with no size, so each is refused at once,
        // though both offer ScrollTo.
        var minimized = await Launched(
            new Scenario
            {
                ["listWindows"] =
                [
                    Windows(_ => [new { id = "w-mine", is_focused = true }]),
                    Windows(_ =>
                        [
                            new
                            {
                                id = "w-mine",
                                visible = false,
                                minimized = true,
                            },
                        ]
                    ),
                ],
                ["snapshot"] = [At(false, ["Click", "ScrollTo"])],
            },
            new LaunchOptions { Observe = false }
        );
        var folded = await Observed(minimized.Surface, node => node.Name == "Top");
        Assert.True(folded.States.Hidden);
        await Rejects(
            minimized.Surface.PerformAsync(folded.Ref, new LocatorAction.Tap(), Operation()),
            "NOT_ACTIONABLE",
            "not on screen"
        );
        Assert.Empty(minimized.Fake.Of("act"));
        var flat = Form(
            new
            {
                role = "button",
                name = "Flat",
                ref_id = "@s:e1",
                available_actions = new[] { "Click", "ScrollTo" },
                bounds = Box(10, 10, 0, 20),
            }
        );
        var zero = await Launched(new Scenario { ["snapshot"] = [flat] }, new LaunchOptions { Observe = false });
        var none = await Observed(zero.Surface, node => node.Name == "Flat");
        await Rejects(
            zero.Surface.PerformAsync(none.Ref, new LocatorAction.Tap(), Operation()),
            "NOT_ACTIONABLE",
            "not on screen"
        );
        Assert.Empty(zero.Fake.Of("act"));
    }

    [Fact]
    public async Task Refuses_to_double_click_a_node_with_no_ref_whose_center_is_a_control_inside_it_and_hovers_it()
    {
        var row = Form(
            new
            {
                role = "group",
                name = "Row",
                bounds = Box(10, 10, 100, 50),
                children = new object[]
                {
                    new
                    {
                        role = "button",
                        name = "Delete",
                        ref_id = "@s:e9",
                        bounds = Box(50, 25, 20, 20),
                    },
                },
            }
        );
        var (surface, fake, _) = await Launched(
            new Scenario { ["snapshot"] = [row], ["listWindows"] = [Placed()] },
            Headed
        );
        var group = await Observed(surface, node => node.Role == "group" && node.Name == "Row");
        await Rejects(
            surface.PerformAsync(group.Ref, new LocatorAction.DoubleTap(), Operation()),
            "NOT_ACTIONABLE",
            "button is at the center of node"
        );
        Assert.Empty(Inputs(fake));
        await surface.PerformAsync(group.Ref, new LocatorAction.Hover(), Operation());
        Assert.Equal(new object[] { 60d, 35d, "hover" }, fake.Of("pointer")[^1].Args);
        // A hover presses nothing: the driver is not told to refuse a control under it.
        Assert.Null(fake.PointerOptions[^1].Target?.Content);
        // A child button that reaches past its group's box is inside it all the same.
        var wide = Form(
            new
            {
                role = "group",
                name = "Row",
                bounds = Box(20, 20, 80, 30),
                children = new object[]
                {
                    new
                    {
                        role = "button",
                        name = "Delete",
                        ref_id = "@s:e9",
                        bounds = Box(50, 20, 80, 30),
                    },
                },
            }
        );
        var past = await Launched(new Scenario { ["snapshot"] = [wide], ["listWindows"] = [Placed()] }, Headed);
        var wideRow = await Observed(past.Surface, node => node.Role == "group" && node.Name == "Row");
        await Rejects(
            past.Surface.PerformAsync(wideRow.Ref, new LocatorAction.DoubleTap(), Operation()),
            "NOT_ACTIONABLE"
        );
        Assert.Empty(Inputs(past.Fake));
        // So is a sibling control laid over the node's center: it is what a click there reaches.
        var laid = Form(
            new
            {
                role = "statictext",
                value = "Notes",
                bounds = Box(10, 10, 100, 20),
            },
            new
            {
                role = "button",
                name = "Delete",
                ref_id = "@s:e9",
                bounds = Box(50, 5, 20, 30),
            }
        );
        var over = await Launched(new Scenario { ["snapshot"] = [laid], ["listWindows"] = [Placed()] }, Headed);
        var notes = await Observed(over.Surface, node => node.Role == "text" || node.Name == "Notes");
        await Rejects(
            over.Surface.PerformAsync(notes.Ref, new LocatorAction.DoubleTap(), Operation()),
            "NOT_ACTIONABLE"
        );
        Assert.Empty(Inputs(over.Fake));
        // A button scrolled out of its list is not under the pointer, whatever box it keeps.
        var scrolled = Form(
            new
            {
                role = "statictext",
                value = "Notes",
                bounds = Box(10, 10, 100, 20),
            },
            new
            {
                role = "button",
                name = "Delete",
                ref_id = "@s:e9",
                states = new[] { "offscreen" },
                bounds = Box(50, 5, 20, 30),
            }
        );
        var away = await Launched(new Scenario { ["snapshot"] = [scrolled], ["listWindows"] = [Placed()] }, Headed);
        var visible = await Observed(away.Surface, node => node.Name == "Notes");
        await away.Surface.PerformAsync(visible.Ref, new LocatorAction.DoubleTap(), Operation());
        Assert.Equal(new object[] { 60d, 20d, "doubleTap" }, away.Fake.Of("pointer")[^1].Args);
        // A read stopped at the depth limit may have left out the control at the center.
        var deep = Form(
            new
            {
                role = "group",
                name = "Row",
                bounds = Box(10, 10, 100, 50),
                children_count = 1,
            }
        );
        var cut = await Launched(new Scenario { ["snapshot"] = [deep], ["listWindows"] = [Placed()] }, Headed);
        var shallow = await Observed(cut.Surface, node => node.Name == "Row");
        await Rejects(
            cut.Surface.PerformAsync(shallow.Ref, new LocatorAction.DoubleTap(), Operation()),
            "NODE_STALE",
            "read only in part"
        );
        Assert.Empty(Inputs(cut.Fake));
        // Text inside a button is the button's: a double-click there presses it. Text in a selectable row is not.
        static Answer Labelled(string role, string[] actions) =>
            Form(
                new
                {
                    role,
                    name = "Holder",
                    ref_id = "@s:e9",
                    available_actions = actions,
                    bounds = Box(10, 10, 100, 20),
                    children = new object[]
                    {
                        new
                        {
                            role = "statictext",
                            value = "Inner",
                            bounds = Box(10, 10, 100, 20),
                        },
                    },
                }
            );
        var button = await Launched(
            new Scenario { ["snapshot"] = [Labelled("button", ["Click"])], ["listWindows"] = [Placed()] },
            Headed
        );
        var inButton = await Observed(button.Surface, node => node.Name == "Inner");
        await Rejects(
            button.Surface.PerformAsync(inButton.Ref, new LocatorAction.DoubleTap(), Operation()),
            "NOT_ACTIONABLE"
        );
        var selectable = await Launched(
            new Scenario { ["snapshot"] = [Labelled("row", ["Click"])], ["listWindows"] = [Placed()] },
            Headed
        );
        var inRow = await Observed(selectable.Surface, node => node.Name == "Inner");
        await selectable.Surface.PerformAsync(inRow.Ref, new LocatorAction.DoubleTap(), Operation());
        Assert.True(selectable.Fake.PointerOptions[^1].Target?.Content);
    }

    [Fact]
    public async Task Double_clicks_a_node_with_a_stable_id_where_a_fresh_read_finds_it_never_the_box_it_was_read_with()
    {
        static Answer At(int x) =>
            Form(
                x == 10
                    ?
                    [
                        new
                        {
                            role = "statictext",
                            value = "Hello",
                            native_id = new { kind = "ax_identifier", value = "hello" },
                            bounds = Box(x, 10, 50, 20),
                        },
                    ]
                    :
                    [
                        new
                        {
                            role = "statictext",
                            value = "Hello",
                            native_id = new { kind = "ax_identifier", value = "hello" },
                            bounds = Box(x, 10, 50, 20),
                        },
                        new
                        {
                            role = "button",
                            name = "Delete",
                            ref_id = "@s:e9",
                            bounds = Box(10, 10, 50, 20),
                        },
                    ]
            );
        var (surface, fake, _) = await Launched(
            new Scenario { ["snapshot"] = [At(10), At(10), At(100)], ["listWindows"] = [Placed()] },
            Headed
        );
        var hello = (await surface.ObserveAsync(Operation(), null)).Roots[0].Children[0];
        await surface.PerformAsync(hello.Ref, new LocatorAction.DoubleTap(), Operation());
        Assert.Equal(new object[] { 125d, 20d, "doubleTap" }, fake.Of("pointer")[^1].Args);
    }

    [Fact]
    public async Task Reports_a_click_the_driver_may_have_delivered_as_possibly_committed_and_one_it_refused_by_its_code()
    {
        var (surface, _, byTestId) = await Launched(
            new Scenario
            {
                ["act"] =
                [
                    Failure("ACTION_FAILED", "delivered_unverified"),
                    Failure("ACTION_NOT_SUPPORTED", "not_delivered"),
                    Failure("STALE_REF", "not_delivered"),
                ],
            }
        );
        await Rejects(
            surface.PerformAsync(byTestId("sign-in"), new LocatorAction.Tap(), Operation()),
            "ACTION_MAY_HAVE_COMMITTED"
        );
        await Rejects(
            surface.PerformAsync(byTestId("sign-in"), new LocatorAction.Tap(), Operation()),
            "UNSUPPORTED_CAPABILITY"
        );
        await Rejects(
            surface.PerformAsync(byTestId("sign-in"), new LocatorAction.Tap(), Operation()),
            "NODE_STALE",
            retryable: true
        );
    }

    [Fact]
    public async Task Reports_an_action_still_running_when_its_budget_ran_out_as_possibly_committed_and_a_cancelled_one_as_cancelled()
    {
        var (surface, _, byTestId) = await Launched(new Scenario { ["act"] = [Ok(null) with { SleepMs = 3_000 }] });
        await Rejects(
            surface.PerformAsync(byTestId("sign-in"), new LocatorAction.Tap(), Operation(timeoutMs: 600)),
            "ACTION_MAY_HAVE_COMMITTED"
        );
        using var controller = new CancellationTokenSource(200);
        await Rejects(
            surface.PerformAsync(byTestId("sign-in"), new LocatorAction.Tap(), Operation(signal: controller.Token)),
            "CANCELLED"
        );
    }

    [Fact]
    public async Task Sends_nothing_once_the_operation_budget_is_spent()
    {
        var (surface, fake, byTestId) = await Launched();
        await Rejects(
            surface.PerformAsync(byTestId("sign-in"), new LocatorAction.Tap(), Operation(timeoutMs: 0)),
            "OPERATION_TIMEOUT"
        );
        Assert.Empty(Inputs(fake));
    }

    [Fact]
    public async Task Takes_a_visible_window_as_in_view_and_refuses_a_minimized_one()
    {
        var shown = await Launched([], new LaunchOptions { Observe = false });
        await shown.Surface.PerformAsync("root", new LocatorAction.ScrollIntoView(), Operation());
        var minimized = await Launched(
            new Scenario
            {
                ["listWindows"] =
                [
                    Windows(_ => [new { id = "w-mine", is_focused = true }]),
                    Windows(_ =>
                        [
                            new
                            {
                                id = "w-mine",
                                minimized = true,
                                visible = false,
                            },
                        ]
                    ),
                ],
            },
            new LaunchOptions { Observe = false }
        );
        await Rejects(
            minimized.Surface.PerformAsync("root", new LocatorAction.ScrollIntoView(), Operation()),
            "NOT_ACTIONABLE",
            "not on screen"
        );
    }

    [Fact]
    public async Task Scrolls_nothing_to_bring_visible_static_text_into_view_and_refuses_hidden_text_it_cannot_scroll_to()
    {
        static Answer Read(string[] states) =>
            Form(
                new
                {
                    role = "statictext",
                    value = "Sign in to the testbed",
                    states,
                    bounds = Box(10, 10, 150, 20),
                }
            );
        var text = LocatorExpression.For("text", "Sign in to the testbed", new TextMatchOptions(), "value");
        var shown = await Launched(new Scenario { ["snapshot"] = [Read([])] }, new LaunchOptions { Observe = false });
        var label = (await shown.Surface.LocateAsync(text, Operation()))[0];
        await shown.Surface.PerformAsync(label.Ref, new LocatorAction.ScrollIntoView(), Operation());
        Assert.Empty(Inputs(shown.Fake));
        var away = await Launched(
            new Scenario { ["snapshot"] = [Read(["offscreen"])] },
            new LaunchOptions { Observe = false }
        );
        var hidden = (await away.Surface.LocateAsync(text, Operation()))[0];
        await Rejects(
            away.Surface.PerformAsync(hidden.Ref, new LocatorAction.ScrollIntoView(), Operation()),
            "NOT_ACTIONABLE"
        );
    }

    [Fact]
    public async Task Takes_a_visible_control_that_has_no_scroll_action_as_in_view_so_an_element_screenshot_of_it_does_not_fail()
    {
        static Answer Read(string[] actions) =>
            Form(
                new
                {
                    role = "button",
                    name = "Sign in",
                    ref_id = "@s:e1",
                    available_actions = actions,
                    bounds = Box(10, 10, 50, 20),
                }
            );
        var plain = await Launched(
            new Scenario { ["snapshot"] = [Read(["Click"])] },
            new LaunchOptions { Observe = false }
        );
        await plain.Surface.PerformAsync(
            (await plain.Surface.ObserveAsync(Operation(), null)).Roots[0].Children[0].Ref,
            new LocatorAction.ScrollIntoView(),
            Operation()
        );
        Assert.Empty(plain.Fake.Acted());
        var scrolls = await Launched(
            new Scenario { ["snapshot"] = [Read(["Click", "ScrollTo"])] },
            new LaunchOptions { Observe = false }
        );
        await scrolls.Surface.PerformAsync(
            (await scrolls.Surface.ObserveAsync(Operation(), null)).Roots[0].Children[0].Ref,
            new LocatorAction.ScrollIntoView(),
            Operation()
        );
        Assert.Equal(["scrollTo"], scrolls.Fake.Acted());
    }

    [Fact]
    public async Task Scrolls_a_control_into_view_through_the_driver_when_it_is_hidden_and_has_a_ref()
    {
        var read = Form(
            new
            {
                role = "button",
                name = "Far",
                ref_id = "@s:e1",
                states = new[] { "offscreen" },
                bounds = Box(10, 900, 50, 20),
            }
        );
        var (surface, fake, _) = await Launched(
            new Scenario { ["snapshot"] = [read] },
            new LaunchOptions { Observe = false }
        );
        var observed = await surface.ObserveAsync(Operation(), null);
        await surface.PerformAsync(observed.Roots[0].Children[0].Ref, new LocatorAction.ScrollIntoView(), Operation());
        var act = Last<ActRequest>(fake, "act")!;
        Assert.Equal("@s:e1", act.Ref);
        Assert.Equal("scrollTo", act.Action);
    }

    [Fact]
    public async Task Scrolls_the_window_on_a_root_swipe_right_after_a_restart_by_its_momentum()
    {
        var scrollable = Screen(
            "w-mine",
            new
            {
                name = "List",
                bounds = Box(0, 0, 100, 100),
                children = new object[]
                {
                    new
                    {
                        role = "scrollarea",
                        ref_id = "@s9:e1",
                        available_actions = new[] { "Scroll" },
                    },
                },
            }
        );
        var (surface, fake, _) = await Launched(
            new Scenario { ["snapshot"] = [scrollable] },
            new LaunchOptions { Observe = false }
        );
        await surface.RestartAsync(Operation());
        await surface.PerformAsync("root", new LocatorAction.Swipe(ScrollDirection.Down), Operation());
        var down = Last<ActRequest>(fake, "act")!;
        Assert.Equal(("@s9:e1", "scroll", "down", 0.5), (down.Ref, down.Action, down.Direction, down.Pages));
        await surface.PerformAsync("root", new LocatorAction.Swipe(ScrollDirection.Up, Momentum.Fast), Operation());
        var up = Last<ActRequest>(fake, "act")!;
        Assert.Equal(("up", 1.5), (up.Direction, up.Pages));
        await surface.PerformAsync("root", new LocatorAction.Swipe(ScrollDirection.Left, Momentum.Slow), Operation());
        var left = Last<ActRequest>(fake, "act")!;
        Assert.Equal(("left", 0.75), (left.Direction, left.Pages));
    }

    [Fact]
    public async Task Scrolls_the_node_that_scrolls_sideways_on_a_root_swipe_left_or_right_and_the_one_that_scrolls_up_and_down_otherwise()
    {
        var store = Screen(
            "w-mine",
            new
            {
                name = "Store",
                bounds = Box(0, 0, 100, 100),
                children = new object[]
                {
                    new
                    {
                        role = "scrollarea",
                        name = "Carousel",
                        ref_id = "@s:e1",
                        available_actions = new[] { "Scroll" },
                        scroll_axes = new[] { "horizontal" },
                    },
                    new
                    {
                        role = "scrollarea",
                        name = "List",
                        ref_id = "@s:e2",
                        available_actions = new[] { "Scroll" },
                        scroll_axes = new[] { "vertical" },
                    },
                },
            }
        );
        var (surface, fake, _) = await Launched(
            new Scenario { ["snapshot"] = [store] },
            new LaunchOptions { Observe = false }
        );
        foreach (
            var (direction, reference, name) in new[]
            {
                (ScrollDirection.Left, "@s:e1", "left"),
                (ScrollDirection.Right, "@s:e1", "right"),
                (ScrollDirection.Up, "@s:e2", "up"),
                (ScrollDirection.Down, "@s:e2", "down"),
            }
        )
        {
            await surface.PerformAsync("root", new LocatorAction.Swipe(direction), Operation());
            var act = Last<ActRequest>(fake, "act")!;
            Assert.Equal((reference, "scroll", name), (act.Ref, act.Action, act.Direction));
        }
    }

    [Fact]
    public async Task Scrolls_the_window_as_it_is_now_on_a_root_swipe_not_the_container_of_an_older_observation()
    {
        static Answer Read(string id) =>
            Screen(
                "w-mine",
                new
                {
                    name = "List",
                    bounds = Box(0, 0, 100, 100),
                    children = new object[]
                    {
                        new
                        {
                            role = "scrollarea",
                            name = id,
                            ref_id = $"@{id}:e1",
                            available_actions = new[] { "Scroll" },
                        },
                    },
                }
            );
        var (surface, fake, _) = await Launched(new Scenario { ["snapshot"] = [Read("s1"), Read("s1"), Read("s2")] });
        await surface.PerformAsync("root", new LocatorAction.Swipe(ScrollDirection.Down), Operation());
        var act = Last<ActRequest>(fake, "act")!;
        Assert.Equal(("@s2:e1", "scroll"), (act.Ref, act.Action));
    }

    [Fact]
    public async Task Keeps_every_node_of_a_screen_larger_than_the_binding_cap()
    {
        var buttons = Enumerable
            .Range(0, 5_000)
            .Select(index =>
                (object)
                    new
                    {
                        role = "button",
                        name = $"b{index}",
                        ref_id = $"@s1:e{index}",
                    }
            )
            .ToArray();
        var (surface, fake, _) = await Launched(
            new Scenario { ["snapshot"] = [Screen("w-mine", new { name = "Big", children = buttons })] },
            new LaunchOptions { Observe = false }
        );
        var observed = await surface.ObserveAsync(Operation(), null);
        await surface.PerformAsync(observed.Roots[0].Children[0].Ref, new LocatorAction.Tap(), Operation());
        var act = Last<ActRequest>(fake, "act")!;
        Assert.Equal(("@s1:e0", "click"), (act.Ref, act.Action));
    }

    [Fact]
    public async Task Waits_out_settle_after_an_agents_action_and_never_after_a_tests_own()
    {
        var (surface, _, byTestId) = await Launched(
            [],
            new LaunchOptions { Options = new DesktopOptions { SettleMilliseconds = 600 } }
        );
        var started = Stopwatch.StartNew();
        await surface.PerformAsync(byTestId("sign-in"), new LocatorAction.Tap(), Operation(OperationOrigin.Agent));
        Assert.True(started.ElapsedMilliseconds >= 550);
        started.Restart();
        await surface.PerformAsync(byTestId("sign-in"), new LocatorAction.Tap(), Operation());
        Assert.True(started.ElapsedMilliseconds < 500);
    }
}
