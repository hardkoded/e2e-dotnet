// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop;
using E2E.Engine;

namespace E2E.Desktop.Tests.Surface;

/// <summary>Ported from <c>describe('DesktopSurface: points')</c> in upstream's <c>packages/desktop/tests/unit/surface.test.ts</c>.</summary>
public sealed class DesktopSurfacePointsTests : SurfaceTestBase
{
    private static readonly LaunchOptions Headed = new()
    {
        Observe = false,
        Options = new DesktopOptions { Headed = true },
    };

    private static object Box(double x, double y, double width, double height) =>
        new
        {
            x,
            y,
            width,
            height,
        };

    private static Answer Button(string id, int x) =>
        Screen(
            id,
            new
            {
                name = id,
                bounds = Box(x, 0, 200, 100),
                children = new object[]
                {
                    new
                    {
                        role = "button",
                        name = "OK",
                        ref_id = $"@{id}:e1",
                        bounds = Box(x + 10, 10, 40, 20),
                    },
                },
            },
            new { snapshot_id = id }
        );

    private static object?[] PointerArgs(FakeDriver fake) => fake.Of("pointer")[^1].Args;

    [Fact]
    public async Task Measures_the_agents_point_in_the_newest_observations_window_as_core_hit_tests_it_though_that_observation_had_no_image()
    {
        var (surface, fake, _) = await Launched(
            new Scenario
            {
                ["listWindows"] =
                [
                    Windows(_ =>
                        [
                            new { id = "w-a", bounds = Box(100, 0, 200, 100) },
                            new
                            {
                                id = "w-b",
                                is_focused = true,
                                bounds = Box(500, 0, 200, 100),
                            },
                        ]
                    ),
                ],
                // The open's read, the first observation and the read after its capture show window A; the second observation reads dialog B, whose capture fails.
                ["snapshot"] = [Button("w-a", 100), Button("w-a", 100), Button("w-a", 100), Button("w-b", 500)],
                ["screenshot"] = [Capture(200, 100), Failure("INTERNAL", "not_delivered", "capture failed")],
            },
            Headed
        );
        Assert.NotNull(
            (await surface.ObserveAsync(Operation(OperationOrigin.Agent), new ObserveOptions { Pixels = true })).Pixels
        );
        Assert.Null(
            (await surface.ObserveAsync(Operation(OperationOrigin.Agent), new ObserveOptions { Pixels = true })).Pixels
        );
        await surface.PerformAtAsync(new Point(20, 30), new PointerAction.Tap(), Operation(OperationOrigin.Agent));
        Assert.Equal(new object[] { 520d, 30d, "tap" }, PointerArgs(fake));
    }

    [Fact]
    public async Task Measures_a_tests_point_in_the_window_of_the_newest_read_though_that_read_took_no_image()
    {
        static Scripted Both(string focused) =>
            Windows(_ =>
                [
                    new
                    {
                        id = "w-a",
                        is_focused = focused == "w-a",
                        bounds = Box(100, 0, 200, 100),
                    },
                    new
                    {
                        id = "w-b",
                        is_focused = focused == "w-b",
                        bounds = Box(500, 0, 200, 100),
                    },
                ]
            );
        var (surface, fake, _) = await Launched(
            new Scenario
            {
                ["listWindows"] = [Both("w-a"), Both("w-a"), Both("w-b")],
                ["snapshot"] = [Button("w-a", 100), Button("w-a", 100), Button("w-b", 500)],
            },
            Headed
        );
        await surface.LocateAsync(LocatorExpression.ForRole("button", null, new RoleOptions()), Operation());
        await surface.ObserveAsync(Operation(), null);
        await surface.PerformAtAsync(new Point(100, 50), new PointerAction.Tap(), Operation());
        Assert.Equal(new object[] { 600d, 50d, "tap" }, PointerArgs(fake));
    }

    [Fact]
    public async Task Cancels_pointer_input_at_a_point_the_call_gave_up_on_so_the_driver_sends_no_click_after_the_step_moved_on()
    {
        var placed = Windows(_ =>
            [
                new
                {
                    id = "w-mine",
                    is_focused = true,
                    bounds = Box(300, 200, 400, 300),
                },
            ]
        );
        var (surface, fake, _) = await Launched(
            new Scenario { ["listWindows"] = [placed], ["pointer"] = [Ok(null) with { SleepMs = 2_000 }] },
            Headed
        );
        using var controller = new CancellationTokenSource(100);
        await Rejects(
            surface.PerformAtAsync(
                new Point(20, 30),
                new PointerAction.DoubleTap(),
                Operation(signal: controller.Token)
            ),
            "CANCELLED"
        );
        var (inputId, timeoutMs, _) = Assert.Single(fake.PointerOptions);
        Assert.True(timeoutMs > 0);
        Assert.Equal([(object)inputId!], fake.Of("cancelInput").Select(entry => entry.Args[0]!).ToArray());
    }

    [Fact]
    public async Task Refuses_a_tap_at_a_point_when_a_sheet_took_focus_as_the_window_came_forward_and_taps_one_on_a_sheet_already_open()
    {
        static Scripted At(bool sheet) =>
            Windows(_ =>
                [
                    sheet
                        ? new
                        {
                            id = "w-mine",
                            is_focused = true,
                            sheet_focused = (bool?)true,
                            sheet_id = (string?)"s-1",
                            bounds = Box(100, 100, 400, 300),
                        }
                        : new
                        {
                            id = "w-mine",
                            is_focused = true,
                            sheet_focused = (bool?)null,
                            sheet_id = (string?)null,
                            bounds = Box(100, 100, 400, 300),
                        },
                ]
            );
        var opened = await Launched(new Scenario { ["listWindows"] = [At(false), At(false), At(true)] }, Headed);
        await Rejects(
            opened.Surface.PerformAtAsync(new Point(10, 50), new PointerAction.Tap(), Operation()),
            "NOT_ACTIONABLE",
            "sheet opened"
        );
        Assert.Empty(opened.Fake.Of("pointer"));
        var open = await Launched(new Scenario { ["listWindows"] = [At(true)] }, Headed);
        await open.Surface.PerformAtAsync(new Point(10, 50), new PointerAction.Tap(), Operation());
        Assert.Equal(new object[] { 110d, 150d, "tap" }, PointerArgs(open.Fake));
        // Bringing the window forward is input too: the call names it, so a cut-off cancels it.
        var focus = open.Fake.FocusOptions[^1];
        Assert.NotNull(focus.InputId);
        Assert.NotNull(focus.TimeoutMs);
        // The driver checks the window again after the move: it is told which window, and that its sheet was there.
        Assert.Equal(new PointerTarget(open.Fake.Pid, "w-mine", true, "s-1"), open.Fake.PointerOptions[^1].Target);
        // A sheet the driver gave no key is never known to be the one the point was taken on.
        var unnamed = Windows(_ =>
            [
                new
                {
                    id = "w-mine",
                    is_focused = true,
                    sheet_focused = true,
                    bounds = Box(100, 100, 400, 300),
                },
            ]
        );
        var keyless = await Launched(new Scenario { ["listWindows"] = [unnamed] }, Headed);
        await Rejects(
            keyless.Surface.PerformAtAsync(new Point(10, 50), new PointerAction.Tap(), Operation()),
            "NOT_ACTIONABLE"
        );
        Assert.Empty(keyless.Fake.Of("pointer"));
    }

    [Fact]
    public async Task Refuses_a_tap_at_an_observed_point_when_the_sheet_on_its_window_opened_closed_or_changed_after_the_observation()
    {
        static Scripted At(string? sheet) =>
            Windows(_ =>
                [
                    sheet is null
                        ? new
                        {
                            id = "w-8032",
                            is_focused = true,
                            sheet_focused = (bool?)null,
                            sheet_id = (string?)null,
                            bounds = Box(100, 100, 400, 300),
                        }
                        : new
                        {
                            id = "w-8032",
                            is_focused = true,
                            sheet_focused = (bool?)true,
                            sheet_id = (string?)sheet,
                            bounds = Box(100, 100, 400, 300),
                        },
                ]
            );
        (string? Before, string? Now, string Message)[] cases =
        [
            (null, "s-1", "sheet opened"),
            ("s-1", null, "sheet the point was taken on closed"),
            ("s-1", "s-2", "another sheet took the place"),
        ];
        foreach (var (before, now, message) in cases)
        {
            // The launch, the observation, then the tap's two reads of the window.
            var (surface, fake, _) = await Launched(
                new Scenario { ["listWindows"] = [At(before), At(before), At(now), At(now)] },
                Headed
            );
            await surface.ObserveAsync(Operation(), null);
            await Rejects(
                surface.PerformAtAsync(new Point(10, 50), new PointerAction.Tap(), Operation()),
                "NOT_ACTIONABLE",
                message
            );
            Assert.Empty(fake.Of("pointer"));
        }

        // The same sheet still there takes the tap, and the driver is told which sheet to check again after the move.
        var same = await Launched(
            new Scenario { ["listWindows"] = [At("s-1"), At("s-1"), At("s-1"), At("s-1")] },
            Headed
        );
        await same.Surface.ObserveAsync(Operation(), null);
        await same.Surface.PerformAtAsync(new Point(10, 50), new PointerAction.Tap(), Operation());
        Assert.Equal(new PointerTarget(same.Fake.Pid, "w-8032", true, "s-1"), same.Fake.PointerOptions[^1].Target);
    }

    [Fact]
    public async Task Taps_double_taps_secondary_taps_and_hovers_a_point_in_the_window_where_it_is_now_with_no_observation_first()
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
                                id = "w-mine",
                                is_focused = true,
                                bounds = Box(300, 200, 400, 300),
                            },
                        ]
                    ),
                ],
            },
            Headed
        );
        PointerAction[] kinds =
        [
            new PointerAction.Tap(),
            new PointerAction.DoubleTap(),
            new PointerAction.SecondaryTap(),
            new PointerAction.Hover(),
        ];
        foreach (var kind in kinds)
        {
            await surface.PerformAtAsync(new Point(20, 30), kind, Operation());
        }

        Assert.Equal(
            [
                [320d, 230d, "tap"],
                [320d, 230d, "doubleTap"],
                [320d, 230d, "secondaryTap"],
                [320d, 230d, "hover"],
            ],
            fake.Of("pointer").Select(call => call.Args).ToArray()
        );
        Assert.All(fake.Of("focusWindow"), call => Assert.Equal("w-mine", call.Args[1]));
    }

    [Fact]
    public async Task Taps_a_point_in_the_window_it_was_taken_from_even_after_another_window_took_focus()
    {
        static Scripted Pair(int first) =>
            Windows(_ =>
                [
                    new
                    {
                        id = "w-mine",
                        is_focused = first == 0,
                        bounds = Box(100, 100, 400, 300),
                    },
                    new
                    {
                        id = "w-other",
                        is_focused = first == 1,
                        bounds = Box(700, 200, 400, 300),
                    },
                ]
            );
        var form = Renamed("macos/sign-in-form", "w-8321", "w-mine");
        var (width, height) = TreeSize(form);
        // The open's read, the observation and the read after its capture list w-mine focused; then w-other takes focus, and w-mine comes back once brought forward.
        var (surface, fake, _) = await Launched(
            new Scenario
            {
                ["snapshot"] = [form],
                ["screenshot"] = [Capture(width, height)],
                ["listWindows"] = [Pair(0), Pair(0), Pair(0), Pair(1), Pair(0)],
            },
            Headed
        );
        Assert.NotNull((await surface.ObserveAsync(Operation(), new ObserveOptions { Pixels = true })).Pixels);
        await surface.PerformAtAsync(new Point(10, 20), new PointerAction.Tap(), Operation());
        Assert.Equal(new object[] { fake.Pid, "w-mine" }, fake.Of("focusWindow")[^1].Args);
        Assert.Equal(new object[] { 110d, 120d, "tap" }, PointerArgs(fake));
    }

    [Fact]
    public async Task Measures_a_point_in_the_window_a_locate_read_after_a_dialog_window_took_over()
    {
        var pair = Windows(_ =>
            [
                new
                {
                    id = "w-8032",
                    is_focused = false,
                    bounds = Box(100, 100, 400, 300),
                },
                new
                {
                    id = "w-dialog",
                    is_focused = true,
                    bounds = Box(500, 100, 300, 200),
                },
            ]
        );
        var (surface, fake, _) = await Launched(
            new Scenario
            {
                ["snapshot"] =
                [
                    SnapshotAnswer("macos/appkit-form"),
                    SnapshotAnswer("macos/appkit-form"),
                    Renamed("macos/appkit-form", "w-8032", "w-dialog"),
                ],
                ["listWindows"] = [pair],
            },
            new LaunchOptions { Options = new DesktopOptions { Headed = true } }
        );
        await surface.LocateAsync(TestId("sign-in"), Operation());
        await surface.PerformAtAsync(new Point(11.5, 16.25), new PointerAction.Tap(), Operation());
        Assert.Equal("w-dialog", fake.Of("focusWindow")[^1].Args[1]);
        Assert.Equal(new object[] { 511.5, 116.25, "tap" }, PointerArgs(fake));
    }

    [Fact]
    public async Task Taps_a_point_in_the_window_a_test_chose_after_a_read_of_another_one()
    {
        static Scripted Pair(bool panel = false) =>
            Windows(_ =>
                [
                    new
                    {
                        id = "w-8032",
                        is_focused = !panel,
                        bounds = Box(100, 100, 400, 300),
                    },
                    new
                    {
                        id = "w-panel",
                        is_focused = panel,
                        bounds = Box(600, 100, 300, 200),
                    },
                ]
            );
        // The panel has focus once it is brought forward, the fifth listing.
        var (surface, fake, _) = await Launched(
            new Scenario { ["listWindows"] = [Pair(), Pair(), Pair(), Pair(), Pair(true)] },
            new LaunchOptions { Options = new DesktopOptions { Headed = true } }
        );
        await surface.LocateAsync(TestId("sign-in"), Operation());
        await surface.UseWindowAsync("w-panel", Operation(timeoutMs: 5_000));
        await surface.PerformAtAsync(new Point(10, 20), new PointerAction.Tap(), Operation());
        Assert.Equal("w-panel", fake.Of("focusWindow")[^1].Args[1]);
        Assert.Equal(new object[] { 610d, 120d, "tap" }, PointerArgs(fake));
    }

    [Fact]
    public async Task Refuses_a_point_when_another_window_took_focus_as_the_target_came_forward_since_the_click_carries_no_window()
    {
        static Scripted List(bool sheet) =>
            Windows(_ =>
                sheet
                    ?
                    [
                        new
                        {
                            id = "w-mine",
                            is_focused = false,
                            bounds = Box(100, 100, 400, 300),
                        },
                        new
                        {
                            id = "w-sheet",
                            is_focused = true,
                            bounds = Box(100, 120, 400, 200),
                        },
                    ]
                    :
                    [
                        new
                        {
                            id = "w-mine",
                            is_focused = true,
                            bounds = Box(100, 100, 400, 300),
                        },
                    ]
            );
        var (surface, fake, _) = await Launched(
            new Scenario
            {
                ["snapshot"] = [Renamed("macos/appkit-form", "w-8032", "w-mine")],
                ["listWindows"] = [List(false), List(false), List(false), List(true)],
            },
            new LaunchOptions { Options = new DesktopOptions { Headed = true } }
        );
        await Rejects(
            surface.PerformAtAsync(new Point(10, 20), new PointerAction.Tap(), Operation()),
            "NOT_ACTIONABLE",
            "another window took focus"
        );
        Assert.Empty(fake.Of("pointer"));
    }

    [Fact]
    public async Task Measures_a_point_from_where_the_window_is_once_it_was_brought_forward()
    {
        static Scripted At(int x) =>
            Windows(_ =>
                [
                    new
                    {
                        id = "w-mine",
                        is_focused = true,
                        bounds = Box(x, 200, 400, 300),
                    },
                ]
            );
        var (surface, fake, _) = await Launched(
            new Scenario
            {
                ["snapshot"] = [Renamed("macos/appkit-form", "w-8032", "w-mine")],
                ["listWindows"] = [At(100), At(100), At(100), At(500)],
            },
            new LaunchOptions { Options = new DesktopOptions { Headed = true } }
        );
        await surface.PerformAtAsync(new Point(10, 20), new PointerAction.Tap(), Operation());
        Assert.Equal(new object[] { 510d, 220d, "tap" }, PointerArgs(fake));
    }

    [Fact]
    public async Task Refuses_a_point_outside_the_window_which_would_land_on_another_app()
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
                                id = "w-mine",
                                is_focused = true,
                                bounds = Box(100, 100, 100, 100),
                            },
                        ]
                    ),
                ],
            },
            Headed
        );
        await Rejects(
            surface.PerformAtAsync(new Point(115, 20), new PointerAction.Tap(), Operation()),
            "NOT_ACTIONABLE"
        );
        Assert.Empty(fake.Of("pointer"));
        Assert.Empty(fake.Of("focusWindow"));
    }
}
