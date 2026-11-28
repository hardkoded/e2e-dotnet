// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E.Desktop;
using E2E.Engine;

namespace E2E.Desktop.Tests.Surface;

/// <summary>Ported from <c>describe('DesktopSurface: keys')</c> in upstream's <c>packages/desktop/tests/unit/surface.test.ts</c>.</summary>
public sealed class DesktopSurfaceKeysTests : SurfaceTestBase
{
    private static object Box(int x, int y, int width, int height) =>
        new
        {
            x,
            y,
            width,
            height,
        };

    private static Dictionary<string, object?> Field(
        string name,
        string reference,
        int y,
        bool focused,
        object? extra = null
    )
    {
        var node = new Dictionary<string, object?>
        {
            ["role"] = "textfield",
            ["name"] = name,
            ["ref_id"] = reference,
            ["bounds"] = Box(10, y, 100, 20),
        };
        if (focused)
        {
            node["states"] = new[] { "focused" };
        }

        if (extra is not null)
        {
            foreach (var property in extra.GetType().GetProperties())
            {
                node[property.Name] = property.GetValue(extra);
            }
        }

        return node;
    }

    private static Answer List(string id, object[] children, object? extra = null)
    {
        var tail = new Dictionary<string, object?> { ["snapshot_id"] = id };
        if (extra is not null)
        {
            foreach (var property in extra.GetType().GetProperties())
            {
                tail[property.Name] = property.GetValue(extra);
            }
        }

        return Screen(
            "w-mine",
            new
            {
                name = "List",
                bounds = Box(0, 0, 200, 200),
                children,
            },
            tail
        );
    }

    private static PressRequest LastPress(FakeDriver fake) => Last<PressRequest>(fake, "press")!;

    [Fact]
    public async Task Presses_keys_as_driver_combos_into_the_window_of_the_launched_process_without_physical_input()
    {
        var (surface, fake, _) = await Launched();
        await surface.PressKeyAsync("ControlOrMeta+a", Operation());
        var press = LastPress(fake);
        Assert.Equal((fake.Pid, "w-mine", "cmd+a", false), (press.Pid, press.WindowId, press.Combo, press.Physical));
        Assert.NotNull(press.InputId);
        Assert.True(press.TimeoutMs > 0);
        Assert.Null(press.FocusedRef);
    }

    [Fact]
    public async Task Presses_keys_through_the_real_keyboard_with_headed()
    {
        var (surface, fake, _) = await Launched(
            [],
            new LaunchOptions { Options = new DesktopOptions { Headed = true } }
        );
        await surface.PressKeyAsync("Enter", Operation());
        var press = LastPress(fake);
        Assert.Equal(("return", true), (press.Combo, press.Physical));
    }

    [Fact]
    public async Task Refuses_a_key_the_driver_could_not_send_to_the_apps_key_window_and_says_nothing_was_sent()
    {
        var unconfirmed = Failure(
            "ACTION_FAILED",
            "not_delivered",
            "the window is not the key window",
            new Dictionary<string, object?> { ["cause"] = "exact_window_focus_unconfirmed" }
        );
        var (surface, fake, byTestId) = await Launched(
            new Scenario
            {
                ["snapshot"] = [Renamed("macos/appkit-form", "w-8032", "w-mine")],
                ["press"] = [unconfirmed],
            }
        );
        await Rejects(
            surface.PerformAsync(byTestId("username"), new LocatorAction.Press("Tab"), Operation()),
            "NOT_ACTIONABLE",
            "key window.*nothing was sent"
        );
        await Rejects(surface.PressKeyAsync("Enter", Operation()), "NOT_ACTIONABLE");
        Assert.Equal(["tab", "return"], fake.Of("press").Select(call => ((PressRequest)call.Args[0]!).Combo).ToArray());
    }

    [Fact]
    public async Task Presses_a_key_into_a_control_only_while_it_holds_focus_and_into_its_own_window()
    {
        var (surface, fake, byTestId) = await Launched(
            new Scenario { ["snapshot"] = [Renamed("macos/appkit-form", "w-8032", "w-mine")] }
        );
        await surface.PerformAsync(byTestId("username"), new LocatorAction.Press("Enter"), Operation());
        Assert.Equal(["focus", "press return"], Inputs(fake));
        // The driver sends the key only while the control's own element still holds the app's focus, checked right before it goes.
        var press = LastPress(fake);
        Assert.Equal(("w-mine", Last<ActRequest>(fake, "act")!.Ref), (press.WindowId, press.FocusedRef));
        await Rejects(
            surface.PerformAsync(byTestId("sign-in"), new LocatorAction.Press("Enter"), Operation()),
            "NOT_ACTIONABLE"
        );
        Assert.Single(fake.Of("press"));
    }

    [Fact]
    public async Task Brings_a_controls_window_forward_before_it_focuses_the_control_for_a_headed_press()
    {
        var (surface, fake, byTestId) = await Launched(
            new Scenario { ["snapshot"] = [Renamed("macos/appkit-form", "w-8032", "w-mine")] },
            new LaunchOptions { Options = new DesktopOptions { Headed = true } }
        );
        await surface.PerformAsync(byTestId("username"), new LocatorAction.Press("Enter"), Operation());
        // The window comes forward first, so a control in a window another held the keyboard from can take focus.
        Assert.Equal(["focusWindow", "focus", "press return"], Inputs(fake)[^3..]);
    }

    [Fact]
    public async Task Refuses_a_shortcut_the_keyboard_layout_cannot_type_before_it_moves_focus()
    {
        var (surface, fake, byTestId) = await Launched(
            new Scenario { ["snapshot"] = [Renamed("macos/appkit-form", "w-8032", "w-mine")] }
        );
        // AZERTY types é on the 2 key without Shift: a digit shortcut there is no key the driver can press.
        fake.Untypable.Add("cmd+2");
        await Rejects(
            surface.PerformAsync(byTestId("username"), new LocatorAction.Press("ControlOrMeta+2"), Operation()),
            "UNSUPPORTED_CAPABILITY"
        );
        Assert.Empty(Inputs(fake));
    }

    [Fact]
    public async Task Sends_no_key_when_focusing_the_control_fails()
    {
        var (surface, fake, byTestId) = await Launched(
            new Scenario { ["act"] = [Failure("ACTION_NOT_SUPPORTED", "not_delivered")] }
        );
        await Rejects(
            surface.PerformAsync(byTestId("username"), new LocatorAction.Press("a"), Operation()),
            "UNSUPPORTED_CAPABILITY"
        );
        Assert.Empty(fake.Of("press"));
    }

    [Fact]
    public async Task Refuses_a_key_the_engine_cannot_press_before_it_moves_focus()
    {
        var (surface, fake, byTestId) = await Launched();
        await Rejects(
            surface.PerformAsync(byTestId("username"), new LocatorAction.Press("Insert"), Operation()),
            "UNSUPPORTED_CAPABILITY"
        );
        Assert.Empty(Inputs(fake));
    }

    [Fact]
    public async Task Reports_a_read_that_went_stale_after_the_press_focused_the_control_as_possibly_committed_so_it_is_not_retried_and_focused_again()
    {
        var form = Renamed("macos/appkit-form", "w-8032", "w-mine");
        var (surface, fake, byTestId) = await Launched(
            new Scenario
            {
                ["snapshot"] =
                [
                    form,
                    form,
                    Failure(
                        "APP_UNRESPONSIVE",
                        "not_delivered",
                        "busy",
                        new Dictionary<string, object?> { ["retryable"] = true }
                    ),
                ],
            }
        );
        await Rejects(
            surface.PerformAsync(byTestId("username"), new LocatorAction.Press("Tab"), Operation()),
            "ACTION_MAY_HAVE_COMMITTED",
            retryable: false
        );
        Assert.Equal(["focus"], fake.Acted());
        Assert.Empty(fake.Of("press"));
    }

    [Fact]
    public async Task Sends_no_key_into_a_window_that_cannot_be_shown_to_be_the_apps_key_window()
    {
        var (surface, fake, byTestId) = await Launched(
            new Scenario
            {
                ["snapshot"] = [Renamed("macos/appkit-form", "w-8032", "w-mine")],
                ["listWindows"] =
                [
                    Windows(_ =>
                        [new { id = "w-mine", is_focused = false }, new { id = "w-panel", is_focused = true }]
                    ),
                ],
            }
        );
        await Rejects(
            surface.PerformAsync(byTestId("username"), new LocatorAction.Press("Enter"), Operation()),
            message: "key window"
        );
        Assert.Empty(fake.Of("press"));
    }

    [Fact]
    public async Task Presses_a_key_into_a_control_whose_window_is_on_another_Space_which_the_window_list_shows_as_not_visible()
    {
        var (surface, fake, byTestId) = await Launched(
            new Scenario
            {
                ["snapshot"] = [Renamed("macos/appkit-form", "w-8032", "w-mine")],
                ["listWindows"] =
                [
                    Windows(_ =>
                        [
                            new
                            {
                                id = "w-mine",
                                is_focused = true,
                                visible = false,
                            },
                        ]
                    ),
                ],
            },
            new LaunchOptions { Options = new DesktopOptions { Headed = true } }
        );
        await surface.PerformAsync(byTestId("username"), new LocatorAction.Press("Enter"), Operation());
        var press = LastPress(fake);
        Assert.Equal(("return", "w-mine"), (press.Combo, press.WindowId));
    }

    [Fact]
    public async Task Sends_no_key_when_another_control_in_the_same_place_holds_focus()
    {
        var form = Renamed("macos/sign-in-form", "w-8321", "w-mine");
        var swapped = Replaced(
            Replaced(form, "\"name\":\"Username\"", "\"name\":\"Search\"", all: false),
            "\"value\":\"username\"",
            "\"value\":\"search\"",
            all: false
        );
        var (surface, fake, byTestId) = await Launched(new Scenario { ["snapshot"] = [form, form, swapped] });
        await Rejects(
            surface.PerformAsync(byTestId("username"), new LocatorAction.Press("Enter"), Operation()),
            message: "did not keep keyboard focus"
        );
        Assert.Empty(fake.Of("press"));
    }

    [Fact]
    public async Task Presses_a_key_into_one_of_two_look_alike_fields_found_again_by_its_place()
    {
        static Answer Read(string id, bool focusSecond) =>
            List(id, [Field("Item", $"@{id}:e1", 10, false), Field("Item", $"@{id}:e2", 40, focusSecond)]);
        var (surface, fake, _) = await Launched(
            new Scenario { ["snapshot"] = [Read("s1", false), Read("s1", false), Read("s2", true)] },
            new LaunchOptions { Observe = false }
        );
        var observed = await surface.ObserveAsync(Operation(), null);
        await surface.PerformAsync(observed.Roots[0].Children[1].Ref, new LocatorAction.Press("a"), Operation());
        var press = LastPress(fake);
        Assert.Equal(("a", "w-mine"), (press.Combo, press.WindowId));
    }

    [Fact]
    public async Task Sends_no_key_when_a_twin_went_as_the_field_was_focused_so_the_other_could_have_taken_its_place()
    {
        var both = List("s1", [Field("Item", "@s1:e1", 10, false), Field("Item", "@s1:e2", 40, false)]);
        var one = List("s2", [Field("Item", "@s2:e1", 40, true)]);
        var (surface, fake, _) = await Launched(
            new Scenario { ["snapshot"] = [both, both, one] },
            new LaunchOptions { Observe = false }
        );
        var observed = await surface.ObserveAsync(Operation(), null);
        await Rejects(
            surface.PerformAsync(observed.Roots[0].Children[1].Ref, new LocatorAction.Press("a"), Operation()),
            "NOT_ACTIONABLE"
        );
        Assert.Empty(fake.Of("press"));
    }

    [Fact]
    public async Task Sends_no_key_when_focusing_a_look_alike_field_replaced_it_with_one_of_the_same_name_but_another_developer_id()
    {
        static object Id(string value) => new { native_id = new { kind = "ax_identifier", value } };
        static Answer Read(string snapshot, string value, bool focusSecond) =>
            List(
                snapshot,
                [
                    Field("Item", $"@{snapshot}:e1", 10, false, Id(value)),
                    Field("Item", $"@{snapshot}:e2", 40, focusSecond, Id(value)),
                ]
            );
        var (surface, fake, _) = await Launched(
            new Scenario
            {
                ["snapshot"] = [Read("s1", "Old", false), Read("s1", "Old", false), Read("s2", "New", true)],
            },
            new LaunchOptions
            {
                Observe = false,
                Options = new DesktopOptions { Headed = true },
            }
        );
        var observed = await surface.ObserveAsync(Operation(), null);
        await Rejects(
            surface.PerformAsync(observed.Roots[0].Children[1].Ref, new LocatorAction.Press("a"), Operation()),
            "NOT_ACTIONABLE"
        );
        Assert.Empty(fake.Of("press"));
    }

    [Fact]
    public async Task Sends_no_key_when_focusing_a_look_alike_field_replaced_the_form_with_other_fields_in_the_same_places()
    {
        static Answer Read(string snapshot, string name, bool focusSecond) =>
            List(
                snapshot,
                [Field(name, $"@{snapshot}:e1", 10, false), Field(name, $"@{snapshot}:e2", 40, focusSecond)]
            );
        var (surface, fake, _) = await Launched(
            new Scenario
            {
                ["snapshot"] = [Read("s1", "Old", false), Read("s1", "Old", false), Read("s2", "New", true)],
            },
            new LaunchOptions { Observe = false }
        );
        var observed = await surface.ObserveAsync(Operation(), null);
        await Rejects(
            surface.PerformAsync(observed.Roots[0].Children[1].Ref, new LocatorAction.Press("a"), Operation()),
            "NOT_ACTIONABLE"
        );
        Assert.Empty(fake.Of("press"));
    }

    [Fact]
    public async Task Says_the_read_was_cut_short_not_that_focus_moved_when_the_focus_check_runs_out_of_time()
    {
        var whole = List("s1", [Field("User", "@s1:e1", 10, false), Field("Note", "@s1:e2", 40, false)]);
        var cut = Screen(
            "w-mine",
            new
            {
                name = "List",
                bounds = Box(0, 0, 200, 200),
                subtree_truncated = true,
                children = new object[]
                {
                    Field("User", "@s2:e1", 10, true),
                    Field("Note", "@s2:e2", 40, false, new { subtree_truncated = true }),
                },
            },
            new
            {
                snapshot_id = "s2",
                complete = false,
                truncated = true,
            }
        );
        var (surface, fake, _) = await Launched(
            new Scenario { ["snapshot"] = [whole, whole, cut] },
            new LaunchOptions { Observe = false }
        );
        var observed = await surface.ObserveAsync(Operation(), null);
        await Rejects(
            surface.PerformAsync(observed.Roots[0].Children[0].Ref, new LocatorAction.Press("a"), Operation()),
            message: "read only in part"
        );
        Assert.Empty(fake.Of("press"));
    }

    [Fact]
    public async Task Presses_a_key_into_a_located_field_among_look_alike_cards_with_interactive_snapshots_and_keeps_the_observation_ids()
    {
        var window = new { name = "Notes", bounds = Box(0, 0, 300, 300) };
        static object Note(bool focused) =>
            focused
                ? new
                {
                    role = "textfield",
                    name = "Note",
                    ref_id = "@s:e1",
                    native_id = new { kind = "ax_identifier", value = "note" },
                    bounds = Box(10, 10, 100, 20),
                    states = new[] { "focused" },
                }
                : new
                {
                    role = "textfield",
                    name = "Note",
                    ref_id = "@s:e1",
                    native_id = new { kind = "ax_identifier", value = "note" },
                    bounds = Box(10, 10, 100, 20),
                    states = (string[])[],
                };
        static object Card(params object[] inside) =>
            new
            {
                role = "group",
                name = "Card",
                children = inside,
            };
        Answer Read(bool focused) =>
            Screen(
                "w-mine",
                new
                {
                    window.name,
                    window.bounds,
                    children = new[]
                    {
                        Card(Note(focused)),
                        Card(
                            new
                            {
                                role = "statictext",
                                value = "Draft",
                                bounds = Box(10, 60, 80, 20),
                            }
                        ),
                    },
                }
            ) with
            {
                Interactive = Screen(
                    "w-mine",
                    new
                    {
                        window.name,
                        window.bounds,
                        children = new[] { Card(Note(focused)) },
                    }
                ),
            };
        var (surface, fake, _) = await Launched(
            new Scenario { ["snapshot"] = [Read(false), Read(false), Read(false), Read(false), Read(true)] },
            new LaunchOptions
            {
                Observe = false,
                Options = new DesktopOptions { Snapshot = DesktopSnapshotMode.Interactive, Headed = true },
            }
        );
        var before = await surface.ObserveAsync(Operation(), null);
        var located = (await surface.LocateAsync(TestId("note"), Operation()))[0];
        var after = await surface.ObserveAsync(Operation(), null);
        Assert.Equal(before.Roots[0].Children[0].Children[0].Ref, after.Roots[0].Children[0].Children[0].Ref);
        await surface.PerformAsync(located.Ref, new LocatorAction.Press("a"), Operation());
        var press = LastPress(fake);
        Assert.Equal(("a", "w-mine"), (press.Combo, press.WindowId));
    }

    [Fact]
    public async Task Waits_out_settle_after_an_agents_key_press_as_after_its_other_actions()
    {
        var (surface, _, _) = await Launched(
            [],
            new LaunchOptions { Options = new DesktopOptions { SettleMilliseconds = 600 } }
        );
        var started = Stopwatch.StartNew();
        await surface.PressKeyAsync("Tab", Operation(OperationOrigin.Agent));
        Assert.True(started.ElapsedMilliseconds >= 550);
    }
}
