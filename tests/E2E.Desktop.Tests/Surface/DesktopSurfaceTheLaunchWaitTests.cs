// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json.Nodes;
using E2E.Desktop;

namespace E2E.Desktop.Tests.Surface;

/// <summary>Ported from <c>describe('DesktopSurface: the launch wait')</c> in upstream's <c>packages/desktop/tests/unit/surface.test.ts</c>.</summary>
public sealed class DesktopSurfaceTheLaunchWaitTests : SurfaceTestBase
{
    [Fact]
    public async Task Fails_the_open_when_the_window_never_becomes_readable()
    {
        await Rejects(
            Launched(
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
                                    accessible = false,
                                },
                            ]
                        ),
                    ],
                },
                new LaunchOptions { OpenTimeoutMs = 15_000 }
            ),
            message: "cannot be read through accessibility"
        );
    }

    [Fact]
    public async Task Reports_a_launch_whose_window_reads_ran_out_of_time_as_unreadable_not_as_an_app_that_opened_no_window()
    {
        await Rejects(
            Launched(
                new Scenario { ["listWindows"] = [Failure("TIMEOUT", "not_delivered")] },
                new LaunchOptions { OpenTimeoutMs = 15_000 }
            ),
            message: @"cannot be read through accessibility \(a read ran out of time\)"
        );
    }

    [Fact]
    public async Task Says_a_background_app_that_opens_no_window_needs_headed_however_the_wait_ends_against_a_slow_window_list()
    {
        await Rejects(
            Launched(
                new Scenario { ["listWindows"] = [Ok(Array.Empty<object>()) with { SleepMs = 700 }] },
                new LaunchOptions { OpenTimeoutMs = 15_000 }
            ),
            message: @"opened no window.*desktop\(\{ headed: true \}\)"
        );
    }

    [Fact]
    public async Task Says_a_foreground_launch_listed_no_window_rather_than_that_its_window_cannot_be_read()
    {
        await Rejects(
            Launched(
                new Scenario { ["listWindows"] = [Ok(Array.Empty<object>())] },
                new LaunchOptions { RunHeaded = true, OpenTimeoutMs = 15_000 }
            ),
            message: "listed no window within 10 s"
        );
    }

    [Fact]
    public async Task Reads_until_the_end_of_the_launch_wait_so_a_window_that_shows_up_late_still_opens()
    {
        await Launched(
            new Scenario
            {
                ["listWindows"] =
                [
                    Ok(Array.Empty<object>()) with
                    {
                        SleepMs = 9_100,
                    },
                    Windows(_ => [new { id = "w-mine", is_focused = true }]),
                ],
            },
            new LaunchOptions { OpenTimeoutMs = 15_000, Observe = false }
        );
    }

    [Fact]
    public async Task Says_the_screen_is_locked_when_macOS_reports_it_so_rather_than_that_the_app_opened_no_window()
    {
        var fake = new FakeDriver(Spawns, new Scenario { ["listWindows"] = [Ok(Array.Empty<object>())] })
        {
            Locked = true,
        };
        await Rejects(Launched(fake, new LaunchOptions { OpenTimeoutMs = 15_000 }), message: "screen is locked");
    }

    [Fact]
    public async Task Reads_again_when_the_driver_says_a_read_of_the_window_may_be_retried()
    {
        var busy = Failure(
            "APP_UNRESPONSIVE",
            "not_delivered",
            "the app did not answer in time",
            new Dictionary<string, object?> { ["retryable"] = true }
        );
        var (_, fake, _) = await Launched(
            new Scenario { ["snapshot"] = [busy, SnapshotAnswer("macos/appkit-form")] },
            new LaunchOptions { Observe = false }
        );
        Assert.True(fake.Of("snapshot").Count >= 2);
        Assert.Single(fake.Spawned);
    }

    [Fact]
    public async Task Reads_again_when_the_window_answered_but_its_root_did_not_so_the_open_waits_for_a_readable_window()
    {
        var form = SnapshotAnswer("macos/appkit-form");
        // The root's attributes stalled: a bare window with no size, and the read marked incomplete.
        var data = ((JsonNode)form.Data!).DeepClone().AsObject();
        data["complete"] = false;
        data["root_unread"] = true;
        data["tree"] = new JsonObject { ["role"] = "window" };
        var unread = form with { Data = data };
        var (_, fake, _) = await Launched(
            new Scenario { ["snapshot"] = [unread, form] },
            new LaunchOptions { Observe = false }
        );
        Assert.True(fake.Of("snapshot").Count >= 2);
        Assert.Single(fake.Spawned);
    }

    [Fact]
    public async Task Reads_again_when_the_window_was_replaced_while_the_open_read_it_and_launches_once()
    {
        var (_, fake, _) = await Launched(
            new Scenario
            {
                ["snapshot"] = [Failure("STALE_REF", "not_delivered"), SnapshotAnswer("macos/appkit-form")],
            },
            new LaunchOptions { Observe = false }
        );
        Assert.True(fake.Of("snapshot").Count >= 2);
        Assert.Single(fake.Spawned);
    }

    [Fact]
    public async Task Reports_an_open_that_ran_out_of_its_own_budget_as_a_timeout_not_a_locked_screen()
    {
        await Rejects(
            Launched(
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
                                    accessible = false,
                                },
                            ]
                        ),
                    ],
                },
                new LaunchOptions { OpenTimeoutMs = 1_500 }
            ),
            "OPERATION_TIMEOUT"
        );
    }

    [Fact]
    public async Task Reports_a_read_that_fails_for_another_reason_as_it_is()
    {
        await Rejects(
            Launched(
                new Scenario { ["snapshot"] = [Failure("PERM_DENIED", "not_delivered")] },
                new LaunchOptions { OpenTimeoutMs = 1_500 }
            ),
            message: "Grant Accessibility"
        );
    }

    [Fact]
    public async Task Keeps_the_launch_wait_within_its_time_when_each_read_of_it_is_slow()
    {
        var started = Stopwatch.StartNew();
        await Rejects(
            Launched(
                new Scenario { ["listWindows"] = [Ok(Array.Empty<object>()) with { SleepMs = 2_000 }] },
                new LaunchOptions { OpenTimeoutMs = 3_000, Observe = false }
            ),
            "OPERATION_TIMEOUT"
        );
        Assert.True(started.ElapsedMilliseconds < 4_500);
    }

    [Fact]
    public async Task Gives_each_read_of_the_launch_wait_what_is_left_of_that_wait_not_of_the_whole_open()
    {
        var (_, fake, _) = await Launched([], new LaunchOptions { OpenTimeoutMs = 60_000, Observe = false });
        Assert.True(Last<SnapshotRequest>(fake, "snapshot")!.TimeoutMs <= 10_000);
    }
}
