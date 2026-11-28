// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using E2E.Desktop;
using E2E.Engine;

namespace E2E.Desktop.Tests.Surface;

/// <summary>Ported from <c>describe('DesktopSurface: launching')</c> in upstream's <c>packages/desktop/tests/unit/surface.test.ts</c>.</summary>
public sealed class DesktopSurfaceLaunchingTests : SurfaceTestBase
{
    private static Dictionary<string, object?> Kind(string kind) => new() { ["kind"] = kind };

    [Fact]
    public async Task Passes_the_targets_launch_arguments_after_its_own_an_object_spelled_as_user_defaults()
    {
        var (_, fake, _) = await Launched(
            [],
            new LaunchOptions
            {
                LaunchArgumentValues = new Dictionary<string, LaunchArgumentValue>
                {
                    ["AppleLanguages"] = new[] { "fr" },
                    ["e2e"] = true,
                },
            }
        );
        Assert.Equal(
            ["-ApplePersistenceIgnoreState", "YES", "-AppleLanguages", "(fr)", "-e2e", "YES"],
            Last<LaunchRequest>(fake, "launch")!.Arguments
        );
    }

    [Fact]
    public async Task Launches_the_app_by_id_in_the_background_and_reads_only_the_windows_of_the_process_it_launched()
    {
        var (_, fake, _) = await Launched();
        var launch = Last<LaunchRequest>(fake, "launch")!;
        Assert.Equal("dev.e2e.spike.form", launch.BundleId);
        Assert.False(launch.Activate);
        Assert.Null(launch.Path);
        Assert.All(fake.Of("listWindows"), call => Assert.Equal(fake.Pid, call.Args[0]));
        var snapshot = Last<SnapshotRequest>(fake, "snapshot")!;
        Assert.Equal(fake.Pid, snapshot.Pid);
        Assert.Equal("w-mine", snapshot.WindowId);
        Assert.Equal(24, snapshot.MaxDepth);
        Assert.False(snapshot.Interactive);
    }

    [Fact]
    public async Task Gives_the_app_only_a_few_variables_of_the_runs_environment_and_its_launch_token_apart_from_them()
    {
        var (_, fake, _) = await Launched();
        var launch = Last<LaunchRequest>(fake, "launch")!;
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["HOME"] = "/Users/ada",
                ["PATH"] = "/usr/bin:/bin",
                ["LANG"] = "en_US.UTF-8",
                ["LC_ALL"] = "C",
            },
            launch.Environment
        );
        Assert.Matches(@"\A[\da-f-]{36}\z", launch.Token);
        Assert.DoesNotContain("hunter2", JsonSerializer.Serialize(launch));
    }

    [Fact]
    public async Task Launches_the_app_in_the_foreground_when_the_run_asks_for_headed_or_desktop_headed_does_and_only_then()
    {
        var (_, fake, _) = await Launched([], new LaunchOptions { RunHeaded = true, Observe = false });
        Assert.True(Last<LaunchRequest>(fake, "launch")!.Activate);
        var (_, headed, _) = await Launched(
            [],
            new LaunchOptions
            {
                Options = new DesktopOptions { Headed = true },
                Observe = false,
            }
        );
        Assert.True(Last<LaunchRequest>(headed, "launch")!.Activate);
        var (_, background, _) = await Launched([], new LaunchOptions { Observe = false });
        Assert.False(Last<LaunchRequest>(background, "launch")!.Activate);
    }

    // plutil reads the bundle id: macOS only.
    [MacOsFact]
    public async Task Launches_an_appPath_build_by_its_path_and_refuses_a_bundleId_that_is_not_its_own()
    {
        var directory = Artifacts();
        var bundle = Path.Combine(directory, "Built.app");
        Directory.CreateDirectory(Path.Combine(bundle, "Contents"));
        File.WriteAllText(
            Path.Combine(bundle, "Contents", "Info.plist"),
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><plist version=\"1.0\"><dict><key>CFBundleIdentifier</key><string>dev.e2e.built</string></dict></plist>"
        );
        var (surface, fake) = await Initialized(
            [],
            new LaunchOptions
            {
                ProjectRoot = directory,
                App = new EngineAppInfo { AppPath = "Built.app", BundleId = "dev.e2e.built" },
            }
        );
        await surface.RestartAsync(Operation());
        Assert.Equal(
            new DirectoryInfo(bundle).ResolveLinkTarget(true)?.FullName ?? bundle,
            Last<LaunchRequest>(fake, "launch")!.Path
        );
        await Rejects(
            Initialized(
                [],
                new LaunchOptions
                {
                    ProjectRoot = directory,
                    App = new EngineAppInfo { AppPath = "Built.app", BundleId = "com.example.other" },
                }
            ),
            message: "is not the CFBundleIdentifier \"dev.e2e.built\""
        );
    }

    [Fact]
    public async Task Refuses_to_launch_without_the_Accessibility_permission()
    {
        var fake = new FakeDriver(Spawns) { Accessibility = false };
        await Rejects(Initialized(fake), "ENGINE_FAILURE", "Accessibility");
    }

    [Fact]
    public async Task Says_which_app_is_missing_when_the_bundle_id_names_none_and_takes_nothing_as_its_own()
    {
        var theirs = StandIn();
        var (surface, _) = await Initialized(
            new Scenario
            {
                ["launch"] =
                [
                    Failure(
                        "APP_NOT_FOUND",
                        "not_delivered",
                        "no installed app has the name or bundle id \"dev.e2e.spike.form\""
                    ),
                ],
            }
        );
        await Rejects(
            surface.RestartAsync(Operation()),
            message: "no installed app has the name or bundle id \"dev.e2e.spike.form\"; check the target"
        );
        await surface.EndAttemptAsync(CancellationToken.None, 5_000);
        Assert.False(await Gone(theirs, 300));
    }

    [Fact]
    public async Task Launches_nothing_for_an_open_cancelled_before_the_launch_was_sent()
    {
        var (surface, fake) = await Initialized();
        using var controller = new CancellationTokenSource();
        await controller.CancelAsync();
        await Rejects(surface.RestartAsync(Operation(signal: controller.Token)), "CANCELLED");
        Assert.Empty(fake.Of("launch"));
    }

    [Fact]
    public async Task Reports_an_open_LaunchServices_refused_as_a_failure_that_started_nothing()
    {
        var (surface, _) = await Initialized(
            new Scenario
            {
                ["launch"] =
                [
                    Failure(
                        "ACTION_FAILED",
                        "not_delivered",
                        "open could not start Built.app: its executable is missing",
                        Kind("not_started")
                    ),
                ],
            }
        );
        await Rejects(surface.RestartAsync(Operation()), "ENGINE_FAILURE", "its executable is missing$");
        var started = Stopwatch.StartNew();
        await surface.EndAttemptAsync(CancellationToken.None, 5_000);
        Assert.True(started.ElapsedMilliseconds < 1_000);
    }

    [Fact]
    public async Task Reports_a_launch_whose_process_could_not_be_told_apart_as_one_that_may_have_committed_and_quits_the_copy_carrying_its_token()
    {
        var (surface, fake) = await Initialized(
            new Scenario
            {
                ["launch"] =
                [
                    SpawnApp(
                        answer: Failure(
                            "TIMEOUT",
                            "delivered_unverified",
                            "no new process carries the token",
                            Kind("unclaimed")
                        )
                    ),
                ],
            }
        );
        await Rejects(surface.RestartAsync(Operation()), "ACTION_MAY_HAVE_COMMITTED", "looks for it again");
        await surface.EndAttemptAsync(CancellationToken.None, 5_000);
        Assert.True(await Gone(fake.Pid));
    }

    [Fact]
    public async Task Keeps_a_launch_that_ran_out_of_time_pending_and_the_next_quit_kills_the_process_the_driver_then_reports()
    {
        // The process carries no token: only the driver's late answer names it.
        var (surface, fake) = await Initialized(new Scenario { ["launch"] = [SpawnApp(token: false, sleepMs: 1_500)] });
        await Rejects(surface.RestartAsync(Operation(timeoutMs: 600)), "ACTION_MAY_HAVE_COMMITTED");
        // The cut-off launch is cancelled, so a driver whose `open` had not run yet starts nothing.
        var inputId = Last<LaunchRequest>(fake, "launch")!.InputId;
        var cancelled = Assert.Single(fake.Of("cancelInput"));
        Assert.Equal(inputId, cancelled.Args[0]);
        var pid = fake.Pid;
        Assert.True(LaunchOwnership.Alive(pid));
        await surface.EndAttemptAsync(CancellationToken.None, 5_000);
        Assert.True(await Gone(pid));
    }

    [Fact]
    public async Task Quits_the_process_carrying_the_token_of_a_launch_that_failed_after_it_may_have_started_the_app()
    {
        var theirs = StandIn();
        var (surface, fake) = await Initialized(
            new Scenario { ["launch"] = [SpawnApp(answer: Failure("TIMEOUT", "delivered_unverified"))] }
        );
        await Rejects(surface.RestartAsync(Operation()), "ACTION_MAY_HAVE_COMMITTED");
        await surface.EndAttemptAsync(CancellationToken.None, 5_000);
        Assert.True(await Gone(fake.Pid));
        Assert.False(await Gone(theirs, 300));
    }

    [Fact]
    public async Task Quits_an_app_a_launch_that_answered_without_one_starts_a_moment_later_by_its_token()
    {
        var (surface, fake) = await Initialized(
            new Scenario
            {
                ["launch"] =
                [
                    Failure("TIMEOUT", "delivered_unverified", "no new process carries the token", Kind("unclaimed")),
                ],
            }
        );
        await Rejects(surface.RestartAsync(Operation()), "ACTION_MAY_HAVE_COMMITTED");
        var token = Last<LaunchRequest>(fake, "launch")!.Token;
        var late = StandIn();
        _ = Task.Delay(350).ContinueWith(_ => fake.Carriers[late] = token, TaskScheduler.Default);
        await surface.EndAttemptAsync(CancellationToken.None, 5_000);
        Assert.True(await Gone(late));
    }

    [Fact]
    public async Task Ends_a_launch_the_driver_gave_up_before_open_ran_without_looking_for_an_app_it_never_started()
    {
        // The answer comes well after the cut-off, which comes well after the launch is sent, even on a loaded machine.
        var (surface, _) = await Initialized(
            new Scenario
            {
                ["launch"] =
                [
                    Failure("TIMEOUT", "not_delivered", "the launch was given up before open ran") with
                    {
                        SleepMs = 3_000,
                    },
                ],
            }
        );
        await Rejects(surface.RestartAsync(Operation(timeoutMs: 1_500)), "ACTION_MAY_HAVE_COMMITTED");
        var started = Stopwatch.StartNew();
        await surface.EndAttemptAsync(CancellationToken.None, 5_000);
        // The quit waits only for the late answer (about 1.5 s after the cut-off), never for the look for a late app.
        Assert.True(started.ElapsedMilliseconds < 2_500);
    }

    [Fact]
    public async Task Keeps_a_launch_pending_when_the_quit_has_no_time_left_to_look_for_its_late_app_so_the_next_quit_still_finds_it()
    {
        var (surface, fake) = await Initialized(
            new Scenario
            {
                ["launch"] =
                [
                    Failure("TIMEOUT", "delivered_unverified", "no new process carries the token", Kind("unclaimed")),
                ],
            }
        );
        await Rejects(surface.RestartAsync(Operation()), "ACTION_MAY_HAVE_COMMITTED");
        var token = Last<LaunchRequest>(fake, "launch")!.Token;
        await Rejects(surface.EndAttemptAsync(CancellationToken.None, 400), "ENGINE_FAILURE", "ran out of time");
        var late = StandIn();
        fake.Carriers[late] = token;
        await surface.EndAttemptAsync(CancellationToken.None, 5_000);
        Assert.True(await Gone(late));
    }

    [Fact]
    public async Task Keeps_a_launch_pending_when_the_look_for_its_late_app_is_cancelled_so_the_next_quit_still_finds_it()
    {
        var (surface, fake) = await Initialized(
            new Scenario
            {
                ["launch"] =
                [
                    Failure("TIMEOUT", "delivered_unverified", "no new process carries the token", Kind("unclaimed")),
                ],
            }
        );
        await Rejects(surface.RestartAsync(Operation()), "ACTION_MAY_HAVE_COMMITTED");
        var token = Last<LaunchRequest>(fake, "launch")!.Token;
        using var timeout = new CancellationTokenSource(150);
        await Rejects(surface.EndAttemptAsync(timeout.Token, 5_000), "CANCELLED");
        var late = StandIn();
        fake.Carriers[late] = token;
        await surface.EndAttemptAsync(CancellationToken.None, 5_000);
        Assert.True(await Gone(late));
    }

    [Fact]
    public async Task Never_kills_a_process_that_has_the_apps_id_when_its_start_cannot_be_read_and_it_carries_no_token()
    {
        var fake = NewFake(new Scenario { ["launch"] = [SpawnApp(token: false)] });
        var (surface, _, _) = await Launched(fake, new LaunchOptions { Observe = false });
        var pid = fake.Pid;
        fake.StartsUnreadable = true;
        try
        {
            await surface.EndAttemptAsync(CancellationToken.None, 2_000);
            Assert.False(await Gone(pid, 300));
        }
        finally
        {
            fake.StartsUnreadable = false;
        }
    }

    [Fact]
    public async Task Never_kills_the_process_a_late_launch_answer_named_once_its_id_went_to_another_process()
    {
        // The launch answers late with a process and its start; by the quit, that id is a process that started at another time.
        var fake = NewFake(
            new Scenario
            {
                ["launch"] =
                [
                    Scripted.From(
                        (driver, _) =>
                            new Answer
                            {
                                Ok = true,
                                Data = new
                                {
                                    app = "SpikeForm",
                                    pid = driver.Spawn(),
                                    started = 7,
                                },
                                SleepMs = 3_000,
                            }
                    ),
                ],
            }
        );
        var (surface, _) = await Initialized(fake);
        await Rejects(surface.RestartAsync(Operation(timeoutMs: 1_500)), "ACTION_MAY_HAVE_COMMITTED");
        var pid = fake.Pid;
        await Rejects(
            surface.EndAttemptAsync(CancellationToken.None, 5_000),
            message: "no process it started was found"
        );
        Assert.False(await Gone(pid, 300));
    }

    [Fact]
    public async Task Never_takes_a_running_copy_without_its_launch_token_and_says_the_launch_left_nothing_it_could_find()
    {
        var theirs = StandIn();
        var (surface, _) = await Initialized(
            new Scenario { ["launch"] = [Failure("TIMEOUT", "delivered_unverified")] }
        );
        await Rejects(surface.RestartAsync(Operation()), "ACTION_MAY_HAVE_COMMITTED");
        // A budget the whole look for a late app fits in: one cut short proves nothing and keeps the launch pending.
        await Rejects(
            surface.EndAttemptAsync(CancellationToken.None, 5_000),
            message: "no process it started was found"
        );
        Assert.False(await Gone(theirs, 300));
    }

    [Fact]
    public async Task Keeps_a_launch_whose_process_it_could_not_look_for_so_the_next_quit_looks_again()
    {
        var fake = NewFake(
            new Scenario { ["launch"] = [SpawnApp(answer: Failure("TIMEOUT", "delivered_unverified"))] }
        );
        var (surface, _) = await Initialized(fake);
        await Rejects(surface.RestartAsync(Operation()), "ACTION_MAY_HAVE_COMMITTED");
        fake.LookupFails = true;
        await Rejects(
            surface.EndAttemptAsync(CancellationToken.None, 1_000),
            message: "could not tell whether a launch"
        );
        fake.LookupFails = false;
        await surface.DisposeAsync(CancellationToken.None, 5_000);
        Assert.True(await Gone(fake.Pid));
    }

    [Fact]
    public async Task Keeps_the_launch_record_of_a_launch_whose_process_was_never_found_so_the_next_run_can_quit_it()
    {
        var fake = NewFake(new Scenario { ["launch"] = [Failure("TIMEOUT", "delivered_unverified")] });
        var (surface, _) = await Initialized(fake);
        var home = surface.Home!;
        try
        {
            await Rejects(surface.RestartAsync(Operation()), "ACTION_MAY_HAVE_COMMITTED");
            fake.LookupFails = true;
            await Rejects(surface.DisposeAsync(CancellationToken.None, 1_000), message: "could not tell");
            Assert.Equal(
                ["app.json", "owner"],
                Directory.GetFileSystemEntries(home).Select(entry => Path.GetFileName(entry)!).Order().ToArray()
            );
            Assert.Equal("abandoned", File.ReadAllText(Path.Combine(home, "owner")));
            Assert.True(
                JsonDocument
                    .Parse(File.ReadAllText(Path.Combine(home, "app.json")))
                    .RootElement.TryGetProperty("token", out var token)
                    && token.ValueKind == JsonValueKind.String
            );
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public async Task Leaves_nothing_to_look_for_after_an_open_whose_budget_ran_out_before_it_launched()
    {
        var (surface, fake) = await Initialized();
        await Rejects(surface.RestartAsync(Operation(timeoutMs: 0)), "OPERATION_TIMEOUT");
        Assert.Empty(fake.Of("launch"));
        await surface.EndAttemptAsync(CancellationToken.None, 5_000);
    }

    [Fact]
    public async Task Opens_again_after_a_failed_launch_that_left_no_process_rather_than_fail_on_the_launch_it_cannot_find()
    {
        var (surface, fake) = await Initialized(
            new Scenario { ["launch"] = [Failure("TIMEOUT", "delivered_unverified"), SpawnApp()] }
        );
        await Rejects(surface.RestartAsync(Operation()), "ACTION_MAY_HAVE_COMMITTED");
        await surface.RestartAsync(Operation());
        Assert.Single(fake.Spawned);
    }

    [Fact]
    public async Task Starts_a_reused_handles_next_host_clean_though_the_last_one_left_a_launch_it_could_not_settle()
    {
        var fake = NewFake(new Scenario { ["launch"] = [Failure("TIMEOUT", "delivered_unverified")] });
        var (surface, _) = await Initialized(fake);
        await Rejects(surface.RestartAsync(Operation()), "ACTION_MAY_HAVE_COMMITTED");
        fake.LookupFails = true;
        await Rejects(surface.EndAttemptAsync(CancellationToken.None, 1_000), message: "could not tell");
        await Rejects(surface.DisposeAsync(CancellationToken.None, 1_000), message: "could not tell");
        // The next host's app is healthy: it launches and lists as it should.
        fake.LookupFails = false;
        fake.Rescript([]);
        await surface.InitAsync(
            new DesktopInitInfo
            {
                ProjectRoot = Directory.GetCurrentDirectory(),
                App = new EngineAppInfo { BundleId = "dev.e2e.spike.second" },
            }
        );
        await surface.RestartAsync(Operation());
        Assert.Equal("dev.e2e.spike.second", Last<LaunchRequest>(fake, "launch")!.BundleId);
    }

    [Fact]
    public async Task Runs_two_overlapping_opens_one_after_the_other_and_leaves_no_copy_running()
    {
        var (surface, fake) = await Initialized();
        await Task.WhenAll(surface.RestartAsync(Operation()), surface.RestartAsync(Operation()));
        await surface.EndAttemptAsync(CancellationToken.None, 5_000);
        Assert.Equal(2, fake.Spawned.Count);
        foreach (var pid in fake.Spawned)
        {
            Assert.True(await Gone(pid));
        }
    }

    [Fact]
    public async Task Fails_the_open_of_an_app_that_quit_right_after_it_launched()
    {
        var started = Stopwatch.StartNew();
        await Rejects(
            Launched(
                new Scenario { ["launch"] = [SpawnApp(exits: true)], ["listWindows"] = [Ok(Array.Empty<object>())] },
                new LaunchOptions { Observe = false }
            ),
            message: "quit right after it launched"
        );
        Assert.True(started.ElapsedMilliseconds < 3_000);
    }
}
