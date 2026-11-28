// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E.Desktop;
using E2E.Engine;

namespace E2E.Desktop.Tests.Surface;

/// <summary>Ported from <c>describe('DesktopSurface: reading')</c> in upstream's <c>packages/desktop/tests/unit/surface.test.ts</c>.</summary>
public sealed class DesktopSurfaceReadingTests : SurfaceTestBase
{
    private static object[] Focused(string id = "w-mine") => [new { id, is_focused = true }];

    private static Dictionary<string, object?> Kind(string kind) => new() { ["kind"] = kind };

    private static SemanticNode? ByTestIdIn(Observation observed, string testId) =>
        observed.Roots[0].Children.FirstOrDefault(node => node.TestId == testId);

    private static List<SemanticNode> All(SemanticNode node) => [node, .. node.Children.SelectMany(All)];

    private static string WindowOf(FakeCall call) => ((SnapshotRequest)call.Args[0]!).WindowId;

    [Fact]
    public async Task Reads_again_when_the_window_list_does_not_find_the_app_while_its_process_still_runs_never_an_empty_screen()
    {
        var first = Windows(_ => Focused());
        var lost = Failure("APP_NOT_FOUND", "not_delivered");
        var (surface, _, _) = await Launched(
            new Scenario { ["listWindows"] = [first, lost] },
            new LaunchOptions { Observe = false }
        );
        await Rejects(surface.LocateAsync(TestId("username"), Operation()), "NODE_STALE", retryable: true);
    }

    [Fact]
    public async Task Reads_and_drives_nothing_before_the_app_is_opened_or_once_it_quit()
    {
        var (surface, fake, _) = await Launched([], new LaunchOptions { Observe = false });
        await surface.CloseAppAsync(Operation(timeoutMs: 5_000));
        int Reads() => fake.Calls.Count(call => call.Method is "snapshot" or "listWindows");
        var before = Reads();
        var observed = await surface.ObserveAsync(Operation(), null);
        Assert.Empty(observed.Roots[0].Children);
        Assert.Empty(await surface.LocateAsync(TestId("username"), Operation()));
        await Rejects(surface.PressKeyAsync("Enter", Operation()), "INVALID_STATE");
        Assert.Equal(before, Reads());
        Assert.Empty(Inputs(fake));
    }

    [Fact]
    public async Task Keeps_a_control_id_across_observations_of_a_window_and_acts_on_its_newest_ref()
    {
        var mine = Renamed("macos/appkit-form", "w-8032", "w-mine");
        var newer = Replaced(mine, "s1y1hxmo5pexrl", "snewer");
        var (surface, fake, byTestId) = await Launched(new Scenario { ["snapshot"] = [mine, mine, newer] });
        var signIn = byTestId("sign-in");
        var again = await surface.ObserveAsync(Operation(), null);
        Assert.Equal(signIn, ByTestIdIn(again, "sign-in")?.Ref);
        await surface.PerformAsync(signIn, new LocatorAction.Tap(), Operation());
        var act = Last<ActRequest>(fake, "act")!;
        Assert.Equal("@snewer:e5", act.Ref);
        Assert.Equal("click", act.Action);
        await Rejects(
            surface.PerformAsync("n9999", new LocatorAction.Tap(), Operation()),
            "NODE_STALE",
            retryable: true
        );
    }

    [Fact]
    public async Task Gives_a_control_in_another_window_an_id_of_its_own()
    {
        var (surface, _, byTestId) = await Launched(
            new Scenario
            {
                ["snapshot"] =
                [
                    SnapshotAnswer("macos/appkit-form"),
                    SnapshotAnswer("macos/appkit-form"),
                    Renamed("macos/appkit-form", "w-8032", "w-other"),
                ],
            }
        );
        var signIn = byTestId("sign-in");
        var other = await surface.ObserveAsync(Operation(), null);
        Assert.NotEqual(signIn, ByTestIdIn(other, "sign-in")?.Ref);
    }

    [Fact]
    public async Task Keeps_the_ids_of_one_window_while_another_window_is_read()
    {
        var mine = Renamed("macos/appkit-form", "w-8032", "w-mine");
        var (surface, _, byTestId) = await Launched(
            new Scenario
            {
                ["listWindows"] = [Windows(_ => [new { id = "w-mine", is_focused = true }, new { id = "w-other" }])],
                ["snapshot"] = [mine, mine, Renamed("macos/appkit-form", "w-8032", "w-other"), mine],
            }
        );
        var signIn = byTestId("sign-in");
        await surface.ObserveAsync(Operation(), null);
        var back = await surface.ObserveAsync(Operation(), null);
        Assert.Equal(signIn, ByTestIdIn(back, "sign-in")?.Ref);
    }

    [Fact]
    public async Task Retires_an_id_once_its_control_has_a_look_alike_so_a_surviving_twin_never_inherits_it()
    {
        static object Button(string reference) =>
            new
            {
                role = "button",
                name = "Delete",
                ref_id = reference,
            };
        static Answer Read(params object[] buttons) => Screen("w-1", new { name = "List", children = buttons });
        var (surface, _, _) = await Launched(
            new Scenario
            {
                ["snapshot"] =
                [
                    Read(Button("@s1:e1")),
                    Read(Button("@s1:e1")),
                    Read(Button("@s2:e1"), Button("@s2:e2")),
                    Read(Button("@s3:e2")),
                ],
            },
            new LaunchOptions { Observe = false }
        );
        var first = await surface.ObserveAsync(Operation(), null);
        var original = first.Roots[0].Children[0].Ref;
        await surface.ObserveAsync(Operation(), null);
        var final = await surface.ObserveAsync(Operation(), null);
        Assert.NotEqual(original, final.Roots[0].Children[0].Ref);
    }

    [Fact]
    public async Task Cuts_an_observations_names_at_256_and_text_at_512_and_still_locates_by_the_whole_text()
    {
        var longName = new string('L', 1_200);
        var labelled = Replaced(
            SnapshotAnswer("macos/appkit-form"),
            "\"name\":\"Username\"",
            $"\"name\":\"{longName}\"",
            all: false
        );
        var (surface, _, _) = await Launched(
            new Scenario { ["snapshot"] = [labelled] },
            new LaunchOptions { Observe = false }
        );
        var observed = await surface.ObserveAsync(Operation(), null);
        Assert.Equal(longName[..256], ByTestIdIn(observed, "username")?.Name);
        foreach (var node in All(observed.Roots[0]))
        {
            Assert.True((node.Name?.Length ?? 0) <= 256);
            Assert.True((node.Text?.Length ?? 0) <= 512);
        }

        Assert.Single(
            await surface.LocateAsync(LocatorExpression.ForRole("textbox", longName, new RoleOptions()), Operation())
        );
    }

    [Fact]
    public async Task Reads_again_when_the_window_closed_while_it_was_read_so_a_locator_keeps_polling()
    {
        var (surface, _, _) = await Launched(
            new Scenario
            {
                ["snapshot"] =
                [
                    SnapshotAnswer("macos/appkit-form"),
                    SnapshotAnswer("macos/appkit-form"),
                    Failure("WINDOW_NOT_FOUND", "not_delivered"),
                ],
            }
        );
        await Rejects(surface.LocateAsync(TestId("username"), Operation()), "NODE_STALE", retryable: true);
    }

    [Fact]
    public async Task Reads_the_controls_of_a_minimized_window_as_hidden_still_found_by_test_id()
    {
        var (surface, _, _) = await Launched(
            new Scenario
            {
                ["listWindows"] =
                [
                    Windows(_ => Focused()),
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
            },
            new LaunchOptions { Observe = false }
        );
        var username = (await surface.LocateAsync(TestId("username"), Operation())).FirstOrDefault();
        Assert.True(username?.States.Hidden);
    }

    [Fact]
    public async Task Reads_an_app_with_no_window_as_an_empty_screen()
    {
        var (surface, _, _) = await Launched(
            new Scenario { ["listWindows"] = [Windows(_ => Focused()), Ok(Array.Empty<object>())] },
            new LaunchOptions { Observe = false }
        );
        Assert.Empty(await surface.LocateAsync(TestId("username"), Operation()));
    }

    [Fact]
    public async Task Reads_again_never_an_empty_screen_or_a_closed_window_once_the_screen_locks_after_the_open()
    {
        var both = Windows(_ => [new { id = "w-mine", is_focused = true }, new { id = "w-panel" }]);
        var fake = NewFake(new Scenario { ["listWindows"] = [both, both, Ok(Array.Empty<object>())] });
        var (surface, _, _) = await Launched(fake, new LaunchOptions { Observe = false });
        await surface.UseWindowAsync("w-panel", Operation(timeoutMs: 5_000));
        fake.Locked = true;
        await Rejects(surface.LocateAsync(TestId("username"), Operation()), "NODE_STALE", "screen is locked", true);
        await surface.UseWindowAsync(null, Operation(timeoutMs: 5_000));
        await Rejects(surface.LocateAsync(TestId("username"), Operation()), "NODE_STALE", "screen is locked", true);
    }

    [Fact]
    public async Task Gives_the_driver_a_read_budget_that_ends_before_the_call_is_cut_off_so_a_read_cut_short_still_answers()
    {
        var (surface, fake, _) = await Launched([], new LaunchOptions { Observe = false });
        await surface.ObserveAsync(Operation(timeoutMs: 5_000), null);
        var timeoutMs = Last<SnapshotRequest>(fake, "snapshot")!.TimeoutMs;
        Assert.True(timeoutMs < 5_000);
        Assert.True(timeoutMs >= 4_000);
    }

    [Fact]
    public async Task Says_the_screen_is_locked_when_it_lists_the_windows_as_ones_accessibility_cannot_read()
    {
        var unreadable = Windows(_ =>
            [
                new
                {
                    id = "w-mine",
                    is_focused = true,
                    accessible = false,
                },
            ]
        );
        var fake = NewFake(new Scenario { ["listWindows"] = [Windows(_ => Focused()), unreadable] });
        var (surface, _, _) = await Launched(fake, new LaunchOptions { Observe = false });
        fake.Locked = true;
        await Rejects(surface.ObserveAsync(Operation(), null), "NODE_STALE", "screen is locked", true);
    }

    [Fact]
    public async Task Reads_again_never_a_closed_window_when_the_screen_locks_between_the_window_list_and_the_read()
    {
        var fake = NewFake(
            new Scenario
            {
                ["listWindows"] = [Windows(_ => [new { id = "w-mine", is_focused = true }, new { id = "w-panel" }])],
                ["snapshot"] = [SnapshotAnswer("macos/appkit-form"), Failure("WINDOW_NOT_FOUND", "not_delivered")],
            }
        );
        var (surface, _, _) = await Launched(fake, new LaunchOptions { Observe = false });
        await surface.UseWindowAsync("w-panel", Operation(timeoutMs: 5_000));
        fake.Locked = true;
        await Rejects(surface.LocateAsync(TestId("username"), Operation()), "NODE_STALE", "screen is locked", true);
    }

    [Fact]
    public async Task Never_reads_the_Siri_dialog_in_place_of_a_window_of_the_app_it_cannot_read()
    {
        var list = Windows(_ =>
            [
                new
                {
                    id = "w-doc",
                    visible = true,
                    accessible = false,
                },
                new
                {
                    id = "w-siri",
                    is_focused = true,
                    system = true,
                },
            ]
        );
        var (surface, fake, _) = await Launched(
            new Scenario { ["listWindows"] = [Windows(_ => Focused()), list] },
            new LaunchOptions { Observe = false }
        );
        await Rejects(surface.ObserveAsync(Operation(), null), "NODE_STALE", retryable: true);
        Assert.DoesNotContain("w-siri", fake.Of("snapshot").Select(WindowOf));
        var alone = await Launched(
            new Scenario
            {
                ["listWindows"] =
                [
                    Windows(_ => Focused()),
                    Windows(_ =>
                        [
                            new
                            {
                                id = "w-siri",
                                is_focused = true,
                                system = true,
                            },
                        ]
                    ),
                ],
            },
            new LaunchOptions { Observe = false }
        );
        await Rejects(alone.Surface.ObserveAsync(Operation(), null), "NODE_STALE", "system dialog");
    }

    [Fact]
    public async Task Reads_the_main_window_before_the_first_one_listed_when_the_app_in_the_background_has_none_focused()
    {
        var list = Windows(_ =>
            [
                new { id = "w-dialog", visible = true },
                new
                {
                    id = "w-doc",
                    visible = true,
                    is_main = true,
                },
            ]
        );
        var (surface, fake, _) = await Launched(
            new Scenario { ["listWindows"] = [list] },
            new LaunchOptions { Observe = false }
        );
        await surface.ObserveAsync(Operation(), null);
        Assert.Equal("w-doc", Last<SnapshotRequest>(fake, "snapshot")!.WindowId);
    }

    [Fact]
    public async Task Reads_the_apps_own_window_not_the_Siri_dialog_macOS_put_in_focus_over_it_unless_the_test_picks_the_dialog()
    {
        var list = Windows(_ =>
            [
                new
                {
                    id = "w-siri",
                    is_focused = true,
                    system = true,
                },
                new { id = "w-doc", visible = true },
            ]
        );
        var (surface, fake, _) = await Launched(
            new Scenario { ["listWindows"] = [list] },
            new LaunchOptions { Observe = false }
        );
        await surface.ObserveAsync(Operation(), null);
        Assert.Equal("w-doc", Last<SnapshotRequest>(fake, "snapshot")!.WindowId);
        await surface.UseWindowAsync("w-siri", Operation(timeoutMs: 5_000));
        await surface.ObserveAsync(Operation(), null);
        Assert.Equal("w-siri", Last<SnapshotRequest>(fake, "snapshot")!.WindowId);
    }

    [Fact]
    public async Task Reads_the_next_window_when_the_focused_first_one_is_a_panel_accessibility_cannot_read()
    {
        var (surface, fake, _) = await Launched(
            new Scenario
            {
                ["listWindows"] =
                [
                    Windows(_ => [new { id = "w-panel", visible = true }, new { id = "w-doc", visible = true }]),
                ],
                ["snapshot"] =
                [
                    SnapshotAnswer("macos/appkit-form"),
                    Failure(
                        "WINDOW_NOT_FOUND",
                        "not_delivered",
                        "no accessibility element",
                        Kind("window_without_accessibility_element")
                    ),
                    SnapshotAnswer("macos/appkit-form"),
                ],
            },
            new LaunchOptions { Observe = false }
        );
        var observed = await surface.ObserveAsync(Operation(), null);
        Assert.NotEmpty(observed.Roots[0].Children);
        Assert.Equal(["w-panel", "w-doc"], fake.Of("snapshot").Skip(1).Select(WindowOf).ToArray());
    }

    [Fact]
    public async Task Skips_a_window_the_list_marks_unreadable_without_asking_the_driver_to_read_it()
    {
        var (surface, fake, _) = await Launched(
            new Scenario
            {
                ["listWindows"] =
                [
                    Windows(_ =>
                        [
                            new
                            {
                                id = "w-hidden",
                                is_focused = true,
                                accessible = false,
                            },
                            new { id = "w-mine", visible = true },
                        ]
                    ),
                ],
            },
            new LaunchOptions { Observe = false }
        );
        await surface.ObserveAsync(Operation(), null);
        Assert.All(fake.Of("snapshot"), call => Assert.Equal("w-mine", WindowOf(call)));
    }

    [Fact]
    public async Task Reads_no_empty_screen_from_an_app_whose_windows_accessibility_cannot_read_and_lets_a_locator_read_again()
    {
        var unreadable = Failure(
            "WINDOW_NOT_FOUND",
            "not_delivered",
            "no accessibility element",
            Kind("window_without_accessibility_element")
        );
        var (surface, _, _) = await Launched(
            new Scenario { ["snapshot"] = [SnapshotAnswer("macos/appkit-form"), unreadable] },
            new LaunchOptions { Observe = false }
        );
        await Rejects(surface.LocateAsync(TestId("nowhere"), Operation()), "NODE_STALE", "could be read", true);
        await Rejects(surface.ObserveAsync(Operation(), null), "NODE_STALE");
    }

    [Fact]
    public async Task Reads_no_empty_screen_from_an_app_that_quit()
    {
        var (surface, fake, _) = await Launched(
            new Scenario { ["listWindows"] = [Windows(_ => Focused()), Ok(Array.Empty<object>())] },
            new LaunchOptions { Observe = false }
        );
        Process.GetProcessById(fake.Pid).Kill();
        Assert.True(await Gone(fake.Pid));
        await Rejects(surface.LocateAsync(TestId("nowhere"), Operation()), "INVALID_STATE", "quit");
    }

    [Fact]
    public async Task Takes_a_process_whose_start_time_changed_as_the_app_quit_as_when_another_process_took_its_id_and_kills_nothing()
    {
        var (surface, fake, _) = await Launched([], new LaunchOptions { Observe = false });
        var pid = fake.Pid;
        fake.StartTimes[pid] = 42;
        await Rejects(surface.LocateAsync(TestId("nowhere"), Operation()), "INVALID_STATE", "quit");
        Assert.Empty(await surface.WindowsAsync(Operation(timeoutMs: 5_000)));
        await surface.EndAttemptAsync(CancellationToken.None, 5_000);
        Assert.False(await Gone(pid, 300));
    }

    [Fact]
    public async Task Reads_a_window_as_deep_as_desktop_maxDepth_says()
    {
        var (_, fake, _) = await Launched([], new LaunchOptions { Options = new DesktopOptions { MaxDepth = 40 } });
        Assert.Equal(40, Last<SnapshotRequest>(fake, "snapshot")!.MaxDepth);
    }

    [Fact]
    public async Task Refuses_a_placeholder_query_which_macOS_names_a_field_by_already()
    {
        var (surface, _, _) = await Launched(
            new Scenario { ["snapshot"] = [SnapshotAnswer("macos/sign-in-form")] },
            new LaunchOptions { Observe = false }
        );
        var placeholder = LocatorExpression.For("placeholder", "Username", new TextMatchOptions(), "value");
        await Rejects(surface.LocateAsync(placeholder, Operation()), "UNSUPPORTED_CAPABILITY");
        var textbox = LocatorExpression.ForRole("textbox", null, new RoleOptions());
        await Rejects(
            surface.LocateAsync(
                new LocatorExpression
                {
                    Kind = "filter",
                    Source = textbox,
                    Has = placeholder,
                },
                Operation()
            ),
            "UNSUPPORTED_CAPABILITY"
        );
        var named = LocatorExpression.ForRole("textbox", "Username", new RoleOptions());
        Assert.Single(await surface.LocateAsync(named, Operation()));
    }

    [Fact]
    public async Task Counts_no_matches_in_a_window_read_in_part_and_lets_the_locator_read_again()
    {
        var cut = Merged(SnapshotAnswer("macos/appkit-form"), new { complete = false });
        var (surface, _, _) = await Launched(
            new Scenario { ["snapshot"] = [cut, cut, cut, SnapshotAnswer("macos/appkit-form")] },
            new LaunchOptions { Observe = false }
        );
        await Rejects(surface.LocateAsync(TestId("nowhere"), Operation()), "NODE_STALE", "read only in part", true);
        await Rejects(surface.LocateAsync(TestId("username"), Operation()), "NODE_STALE");
        Assert.Single(await surface.LocateAsync(TestId("username"), Operation()));
    }

    [Fact]
    public async Task Answers_a_window_read_the_deadline_stopped_as_the_timeout_it_is_so_a_wait_ends_on_its_last_whole_read()
    {
        var stopped = Merged(SnapshotAnswer("macos/appkit-form"), new { complete = false, timed_out = true });
        var (surface, _, _) = await Launched(
            new Scenario { ["snapshot"] = [stopped] },
            new LaunchOptions { Observe = false }
        );
        await Rejects(surface.LocateAsync(TestId("nowhere"), Operation()), "OPERATION_TIMEOUT", retryable: false);
    }

    [Fact]
    public async Task Reads_the_whole_window_for_a_locator_even_with_interactive_snapshots_so_a_label_is_found()
    {
        var window = new
        {
            name = "Form",
            bounds = new
            {
                x = 0,
                y = 0,
                width = 100,
                height = 100,
            },
        };
        var label = new
        {
            role = "statictext",
            value = "Not signed in",
            bounds = new
            {
                x = 10,
                y = 40,
                width = 80,
                height = 20,
            },
        };
        var button = new
        {
            role = "button",
            name = "Sign in",
            ref_id = "@s:e1",
            bounds = new
            {
                x = 10,
                y = 10,
                width = 50,
                height = 20,
            },
        };
        var read = Screen(
            "w-mine",
            new
            {
                window.name,
                window.bounds,
                children = new object[] { button, label },
            }
        ) with
        {
            Interactive = Screen(
                "w-mine",
                new
                {
                    window.name,
                    window.bounds,
                    children = new object[] { button },
                }
            ),
        };
        var (surface, fake, _) = await Launched(
            new Scenario { ["snapshot"] = [read] },
            new LaunchOptions
            {
                Observe = false,
                Options = new DesktopOptions { Snapshot = DesktopSnapshotMode.Interactive },
            }
        );
        var observed = await surface.ObserveAsync(Operation(), null);
        Assert.Single(observed.Roots[0].Children);
        Assert.Single(
            await surface.LocateAsync(
                LocatorExpression.For("text", "Not signed in", new TextMatchOptions(), "value"),
                Operation()
            )
        );
        var reads = fake.Of("snapshot").Select(call => ((SnapshotRequest)call.Args[0]!).Interactive).ToArray();
        Assert.Equal([true, false], reads[^2..]);
    }

    [Fact]
    public async Task Reads_a_windows_title_from_the_drivers_list_and_lists_the_apps_windows()
    {
        var (surface, _, _) = await Launched(
            new Scenario
            {
                ["listWindows"] =
                [
                    Windows(_ =>
                        [
                            new
                            {
                                id = "w-mine",
                                title = "Spike Form",
                                is_focused = true,
                            },
                        ]
                    ),
                    Windows(_ =>
                        [
                            new
                            {
                                id = "w-mine",
                                title = "Spike Form",
                                is_focused = true,
                            },
                            new { id = "w-prefs", title = "Preferences" },
                            (object)new { id = "w-untitled" },
                        ]
                    ),
                ],
            },
            new LaunchOptions { Observe = false }
        );
        Assert.Equal(
            [
                new DesktopWindow("w-mine", "Spike Form", true),
                new DesktopWindow("w-prefs", "Preferences", false),
                new DesktopWindow("w-untitled", "", false),
            ],
            await surface.WindowsAsync(Operation(timeoutMs: 5_000))
        );
    }

    [Fact]
    public async Task Drops_the_identities_of_a_window_once_the_app_no_longer_lists_it()
    {
        static Answer Read(string id) =>
            Screen(
                id,
                new
                {
                    name = id,
                    bounds = new
                    {
                        x = 0,
                        y = 0,
                        width = 200,
                        height = 100,
                    },
                    children = new object[]
                    {
                        new
                        {
                            role = "button",
                            name = "OK",
                            ref_id = $"@{id}:e1",
                            bounds = new
                            {
                                x = 10,
                                y = 10,
                                width = 40,
                                height = 20,
                            },
                        },
                    },
                }
            );
        var (surface, _, _) = await Launched(
            new Scenario
            {
                ["listWindows"] =
                [
                    Windows(_ => Focused("w-1")),
                    Windows(_ => Focused("w-1")),
                    Windows(_ => Focused("w-2")),
                ],
                ["snapshot"] = [Read("w-1"), Read("w-1"), Read("w-2")],
            },
            new LaunchOptions { Observe = false }
        );
        var ids = surface.Ids;
        await surface.ObserveAsync(Operation(), null);
        Assert.Contains(ids.Keys, identity => identity.StartsWith("w-1|", StringComparison.Ordinal));
        await surface.ObserveAsync(Operation(), null);
        Assert.DoesNotContain(ids.Keys, identity => identity.StartsWith("w-1|", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Gives_no_id_to_a_control_under_a_node_a_read_cut_short_which_may_hide_its_twin()
    {
        var window = new
        {
            name = "Rows",
            bounds = new
            {
                x = 0,
                y = 0,
                width = 300,
                height = 300,
            },
        };
        static object Remove(string reference, int x) =>
            new
            {
                role = "button",
                name = "Delete",
                ref_id = reference,
                bounds = new
                {
                    x,
                    y = 10,
                    width = 60,
                    height = 20,
                },
            };
        static object List(object[] nodes, bool cut = false) =>
            cut
                ? new
                {
                    role = "list",
                    name = "Rows",
                    subtree_truncated = true,
                    children = nodes,
                }
                : new
                {
                    role = "list",
                    name = "Rows",
                    children = nodes,
                };
        Answer Read(object tree, bool complete = true) =>
            Screen(
                "w-mine",
                new
                {
                    window.name,
                    window.bounds,
                    children = new[] { tree },
                },
                new { complete }
            );
        var (surface, _, _) = await Launched(
            new Scenario
            {
                ["snapshot"] =
                [
                    Read(List([Remove("@s:e1", 200)])),
                    Read(List([Remove("@s:e1", 200)])),
                    Read(List([Remove("@s:e2", 10)], true), false),
                ],
            },
            new LaunchOptions { Observe = false }
        );
        var first = await surface.ObserveAsync(Operation(), null);
        var cut = await surface.ObserveAsync(Operation(), null);
        Assert.NotEqual(first.Roots[0].Children[0].Children[0].Ref, cut.Roots[0].Children[0].Children[0].Ref);
    }

    [Fact]
    public async Task Keeps_no_identity_for_a_control_under_a_look_alike_so_reads_do_not_pile_them_up()
    {
        static object Card(string reference) =>
            new
            {
                role = "group",
                name = "Card",
                children = new object[]
                {
                    new
                    {
                        role = "button",
                        name = "Open",
                        ref_id = reference,
                        bounds = new
                        {
                            x = 10,
                            y = 10,
                            width = 60,
                            height = 20,
                        },
                    },
                },
            };
        var read = Screen(
            "w-mine",
            new
            {
                name = "Cards",
                bounds = new
                {
                    x = 0,
                    y = 0,
                    width = 300,
                    height = 300,
                },
                subtree_truncated = true,
                children = new[] { Card("@s:e1"), Card("@s:e2") },
            },
            new { complete = false, truncated = true }
        );
        var (surface, _, _) = await Launched(
            new Scenario { ["snapshot"] = [read] },
            new LaunchOptions { Observe = false }
        );
        var ids = surface.Ids;
        await surface.ObserveAsync(Operation(), null);
        var size = ids.Count;
        for (var index = 0; index < 5; index++)
        {
            await surface.ObserveAsync(Operation(), null);
        }

        Assert.Equal(size, ids.Count);
    }

    [Fact]
    public async Task Gives_no_id_to_a_control_beside_a_wrapper_the_read_cut_off_at_the_depth_limit_which_may_hide_its_twin()
    {
        static object Remove(string reference, int x) =>
            new
            {
                role = "button",
                name = "Delete",
                ref_id = reference,
                bounds = new
                {
                    x,
                    y = 10,
                    width = 60,
                    height = 20,
                },
            };
        static Answer Read(params object[] nodes) =>
            Screen(
                "w-mine",
                new
                {
                    name = "Rows",
                    bounds = new
                    {
                        x = 0,
                        y = 0,
                        width = 300,
                        height = 300,
                    },
                    children = new object[]
                    {
                        new
                        {
                            role = "list",
                            name = "Rows",
                            children = nodes,
                        },
                    },
                }
            );
        var (surface, _, _) = await Launched(
            new Scenario
            {
                ["snapshot"] =
                [
                    Read(Remove("@s:e1", 200)),
                    Read(Remove("@s:e1", 200)),
                    Read(Remove("@s:e2", 10), new { role = "group", children_count = 2 }),
                ],
            },
            new LaunchOptions { Observe = false }
        );
        var first = await surface.ObserveAsync(Operation(), null);
        var cut = await surface.ObserveAsync(Operation(), null);
        Assert.NotEqual(first.Roots[0].Children[0].Children[0].Ref, cut.Roots[0].Children[0].Children[0].Ref);
    }

    [Fact]
    public async Task Passes_no_id_on_through_a_read_cut_short_that_gave_the_twins_parent_a_fresh_id()
    {
        static object Item(string name, string reference) =>
            new
            {
                role = "menuitem",
                name,
                ref_id = reference,
                native_id = new { kind = "ax_identifier", value = "menuItemSelected:" },
                bounds = new
                {
                    x = 10,
                    y = 10,
                    width = 100,
                    height = 20,
                },
            };
        static Answer Read(object[] items, bool cut = false) =>
            Screen(
                "w-mine",
                cut
                    ? new
                    {
                        name = "Menu",
                        bounds = new
                        {
                            x = 0,
                            y = 0,
                            width = 300,
                            height = 300,
                        },
                        subtree_truncated = true,
                        children = new object[]
                        {
                            new
                            {
                                role = "menu",
                                name = "Actions",
                                children = items,
                            },
                        },
                    }
                    : (object)
                        new
                        {
                            name = "Menu",
                            bounds = new
                            {
                                x = 0,
                                y = 0,
                                width = 300,
                                height = 300,
                            },
                            children = new object[]
                            {
                                new
                                {
                                    role = "menu",
                                    name = "Actions",
                                    children = items,
                                },
                            },
                        },
                cut ? new { complete = false, truncated = true } : null
            );
        var (surface, _, _) = await Launched(
            new Scenario
            {
                ["snapshot"] =
                [
                    Read([Item("Archive", "@s:e1")]),
                    Read([Item("Archive", "@s:e1")]),
                    Read([Item("Archive", "@s:e1"), Item("Delete", "@s:e2")], true),
                    Read([Item("Delete", "@s:e2")]),
                ],
            },
            new LaunchOptions { Observe = false }
        );
        var first = await surface.ObserveAsync(Operation(), null);
        await surface.ObserveAsync(Operation(), null);
        var final = await surface.ObserveAsync(Operation(), null);
        static SemanticNode Only(Observation observed) => observed.Roots[0].Children[0].Children[0];
        Assert.Equal("Delete", Only(final).Name);
        Assert.NotEqual(Only(first).Ref, Only(final).Ref);
    }

    [Fact]
    public async Task Keeps_the_id_of_a_control_a_read_cut_short_did_not_reach_and_retires_one_it_saw_a_twin_take()
    {
        var mail = new
        {
            role = "textfield",
            name = "To",
            ref_id = "@s:e1",
            native_id = new { kind = "ax_identifier", value = "mail" },
            bounds = new
            {
                x = 10,
                y = 10,
                width = 100,
                height = 20,
            },
        };
        var send = new
        {
            role = "button",
            name = "Send",
            ref_id = "@s:e2",
            bounds = new
            {
                x = 10,
                y = 40,
                width = 60,
                height = 20,
            },
        };
        var twin = new
        {
            send.role,
            send.name,
            ref_id = "@s:e3",
            send.bounds,
        };
        static Answer Read(object[] nodes, bool complete = true) =>
            Screen(
                "w-mine",
                new
                {
                    name = "Mail",
                    bounds = new
                    {
                        x = 0,
                        y = 0,
                        width = 300,
                        height = 300,
                    },
                    children = nodes,
                },
                new { complete }
            );
        var (surface, _, _) = await Launched(
            new Scenario
            {
                ["snapshot"] = [Read([mail, send]), Read([mail, send]), Read([twin, send], false), Read([mail, send])],
            },
            new LaunchOptions { Observe = false }
        );
        static string? IdOf(SemanticNode root, string name) =>
            root.Children.FirstOrDefault(node => node.Name == name)?.Ref;
        var first = await surface.ObserveAsync(Operation(), null);
        await surface.ObserveAsync(Operation(), null);
        var final = await surface.ObserveAsync(Operation(), null);
        Assert.Equal(IdOf(first.Roots[0], "To"), IdOf(final.Roots[0], "To"));
        Assert.NotEqual(IdOf(first.Roots[0], "Send"), IdOf(final.Roots[0], "Send"));
    }
}
