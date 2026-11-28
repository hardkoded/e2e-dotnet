// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E.Desktop;

namespace E2E.Desktop.Tests.Surface;

/// <summary>Ported from <c>describe('DesktopSurface: sweeping what killed workers left')</c> in upstream's <c>packages/desktop/tests/unit/surface.test.ts</c>.</summary>
public sealed class DesktopSurfaceSweepingWhatKilledWorkersLeftTests : SurfaceTestBase
{
    private static void Remove(params string[] directories)
    {
        foreach (var directory in directories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Quits_the_app_a_killed_worker_left_running_by_its_launch_token_and_nothing_else()
    {
        var dead = await DeadPid();
        var fake = NewFake();
        var theirs = StandIn();
        var leaked = StandIn();
        fake.Carriers[theirs] = "another-token";
        fake.Carriers[leaked] = "token-1";
        // Written before the launch: the token alone, since the worker may be killed before it learns the process id.
        var first = LeftDirectory(dead.ToString(), new { token = "token-1" });
        var second = LeftDirectory(dead.ToString(), new { pid = theirs, token = "token-1" });
        try
        {
            await Initialized(fake);
            Assert.True(await Gone(leaked));
            Assert.False(await Gone(theirs, 300));
            Assert.False(Directory.Exists(first));
            Assert.False(Directory.Exists(second));
        }
        finally
        {
            Remove(first, second);
        }
    }

    [Fact]
    public async Task Quits_the_process_a_killed_workers_record_names_by_its_id_and_start_though_its_environment_cannot_be_read_and_not_a_later_one_given_that_id()
    {
        var dead = await DeadPid();
        var fake = NewFake();
        var leaked = StandIn();
        var reused = StandIn();
        var directories = new[]
        {
            LeftDirectory(
                dead.ToString(),
                new
                {
                    token = "token-1",
                    pid = leaked,
                    started = fake.ProcessStarted(leaked),
                }
            ),
            // The id the record names now belongs to a process that started at another time.
            LeftDirectory(
                dead.ToString(),
                new
                {
                    token = "token-2",
                    pid = reused,
                    started = 7,
                }
            ),
        };
        try
        {
            await Initialized(fake);
            Assert.True(await Gone(leaked));
            Assert.False(await Gone(reused, 300));
            foreach (var directory in directories)
            {
                Assert.False(Directory.Exists(directory));
            }
        }
        finally
        {
            Remove(directories);
        }
    }

    [Fact]
    public async Task Quits_the_process_an_agent_desktop_engines_record_names_by_its_id_and_ps_start_time_and_not_a_later_one_given_that_id()
    {
        var dead = await DeadPid();
        var fake = NewFake();
        var leaked = StandIn();
        var reused = StandIn();
        // 2026-11-26T10:00:00.123456Z, which `ps -o lstart=` in UTC prints to the second.
        fake.StartTimes[leaked] = 1_795_687_200_123_456;
        fake.StartTimes[reused] = 1_795_690_800_000_000;
        var directories = new[]
        {
            LeftDirectory(
                dead.ToString(),
                new
                {
                    token = "token-1",
                    pid = leaked,
                    started = "Thu Nov 26 10:00:00 2026",
                },
                "e2e-agent-desktop-"
            ),
            LeftDirectory(
                dead.ToString(),
                new
                {
                    token = "token-2",
                    pid = reused,
                    started = "Thu Nov 26 10:00:00 2026",
                },
                "e2e-agent-desktop-"
            ),
        };
        try
        {
            await Initialized(fake);
            Assert.True(await Gone(leaked));
            Assert.False(await Gone(reused, 300));
            Assert.False(Directory.Exists(directories[0]));
        }
        finally
        {
            Remove(directories);
        }
    }

    [Fact]
    public async Task Sweeps_the_agent_desktop_engines_state_directories_too_whose_field_text_must_not_stay_behind()
    {
        var dead = await DeadPid();
        var fake = NewFake();
        var leaked = StandIn();
        fake.Carriers[leaked] = "token-old";
        var left = LeftDirectory(dead.ToString(), new { token = "token-old" }, "e2e-agent-desktop-");
        Directory.CreateDirectory(Path.Combine(left, "refs"));
        File.WriteAllText(Path.Combine(left, "refs", "fields.json"), "{\"value\":\"ada\"}");
        try
        {
            await Initialized(fake);
            Assert.True(await Gone(leaked));
            Assert.False(Directory.Exists(left));
        }
        finally
        {
            Remove(left);
        }
    }

    [Fact]
    public async Task Keeps_the_launch_record_of_a_killed_worker_when_the_process_lookup_fails_and_quits_its_app_at_the_next_sweep()
    {
        var dead = await DeadPid();
        var fake = NewFake();
        var leaked = StandIn();
        fake.Carriers[leaked] = "token-keep";
        var left = LeftDirectory(dead.ToString(), new { token = "token-keep" });
        File.WriteAllText(Path.Combine(left, "fields.json"), "{\"text\":\"typed\"}");
        try
        {
            fake.LookupFails = true;
            await Initialized(fake);
            Assert.False(await Gone(leaked, 300));
            Assert.Equal(
                ["app.json", "owner"],
                Directory.GetFileSystemEntries(left).Select(entry => Path.GetFileName(entry)!).Order().ToArray()
            );
            fake.LookupFails = false;
            await Initialized(fake);
            Assert.True(await Gone(leaked));
            Assert.False(Directory.Exists(left));
        }
        finally
        {
            Remove(left);
        }
    }

    [Fact]
    public async Task Quits_the_app_a_failed_dispose_left_running_at_the_next_init_in_the_same_process()
    {
        var fake = NewFake();
        // No start time to tell the process by: only its token shows it is still the launch's.
        fake.StartsUnreadable = true;
        var (surface, _, _) = await Launched(fake, new LaunchOptions { Observe = false });
        var pid = fake.Pid;
        var home = surface.Home!;
        try
        {
            fake.LookupFails = true;
            await Rejects(
                surface.DisposeAsync(CancellationToken.None, 5_000),
                message: "could not tell whether process"
            );
            Assert.False(await Gone(pid, 300));
            Assert.Equal("abandoned", File.ReadAllText(Path.Combine(home, "owner")));
            Assert.Equal(
                ["app.json", "owner"],
                Directory.GetFileSystemEntries(home).Select(entry => Path.GetFileName(entry)!).Order().ToArray()
            );
            // A second host in this process, as a program that opens one after another does.
            fake.LookupFails = false;
            fake.StartsUnreadable = false;
            await Initialized(fake);
            Assert.True(await Gone(pid));
            Assert.False(Directory.Exists(home));
        }
        finally
        {
            Remove(home);
        }
    }

    [Fact]
    public async Task Trusts_no_state_directory_it_cannot_show_this_user_wrote_a_symlink_in_the_temporary_directory_is_left_alone()
    {
        var dead = await DeadPid();
        var fake = NewFake();
        var victim = StandIn();
        fake.Carriers[victim] = "token-planted";
        // What another user could put in a shared /tmp: a link named like a state directory, to a record that names a process of this user.
        var planted = Artifacts();
        File.WriteAllText(Path.Combine(planted, "owner"), dead.ToString());
        File.WriteAllText(Path.Combine(planted, "app.json"), "{\"token\":\"token-planted\"}");
        var link = Path.Combine(Path.GetTempPath(), $"e2e-desktop-worker-planted-{Environment.ProcessId}");
        Directory.CreateSymbolicLink(link, planted);
        try
        {
            await Initialized(fake);
            Assert.False(await Gone(victim, 300));
            Assert.True(File.Exists(Path.Combine(planted, "app.json")));
        }
        finally
        {
            // The link itself, never its target.
            Directory.Delete(link);
        }
    }

    [Fact]
    public async Task Sweeps_a_state_directory_whose_owners_id_another_process_took_and_records_when_its_own_worker_started()
    {
        // This test's own process id, but another start time: the worker that wrote it is gone, and the OS gave its id to this process.
        var left = LeftDirectory($"{Environment.ProcessId}\n7");
        try
        {
            var (surface, fake) = await Initialized();
            Assert.False(Directory.Exists(left));
            var home = surface.Home!;
            Assert.Equal(
                $"{Environment.ProcessId}\n{fake.ProcessStarted(Environment.ProcessId)}",
                File.ReadAllText(Path.Combine(home, "owner"))
            );
        }
        finally
        {
            Remove(left);
        }
    }

    [Fact]
    public async Task Keeps_the_state_directory_and_app_of_a_live_worker_its_start_written_the_way_the_agent_desktop_engine_wrote_it()
    {
        var fake = NewFake();
        var worker = StandIn();
        var app = StandIn();
        fake.Carriers[app] = "token-live";
        fake.StartTimes[worker] = 1_795_687_200_500_000;
        var used = LeftDirectory(
            $"{worker}\nThu Nov 26 10:00:00 2026",
            new { token = "token-live" },
            "e2e-agent-desktop-"
        );
        try
        {
            await Initialized(fake);
            Assert.True(Directory.Exists(used));
            Assert.False(await Gone(app, 300));
        }
        finally
        {
            Remove(used);
        }
    }

    [Fact]
    public async Task Sweeps_an_agent_desktop_workers_directory_whose_process_id_another_process_took_since()
    {
        var fake = NewFake();
        var reused = StandIn();
        fake.StartTimes[reused] = 1_795_690_800_000_000;
        var left = LeftDirectory(
            $"{reused}\nThu Nov 26 10:00:00 2026",
            new { token = "token-gone" },
            "e2e-agent-desktop-"
        );
        try
        {
            await Initialized(fake);
            Assert.False(Directory.Exists(left));
        }
        finally
        {
            Remove(left);
        }
    }

    [Fact]
    public async Task Sweeps_a_state_directory_whose_launch_record_was_cut_short_and_starts()
    {
        var left = LeftDirectory((await DeadPid()).ToString(), "{\"pid\":");
        try
        {
            await Initialized();
            Assert.False(Directory.Exists(left));
        }
        finally
        {
            Remove(left);
        }
    }

    [Fact]
    public async Task Sweeps_the_state_directories_of_workers_that_are_gone_and_keeps_the_ones_in_use()
    {
        var left = LeftDirectory((await DeadPid()).ToString());
        var used = LeftDirectory(Environment.ProcessId.ToString());
        try
        {
            await Initialized();
            Assert.False(Directory.Exists(left));
            Assert.True(Directory.Exists(used));
        }
        finally
        {
            Remove(left, used);
        }
    }
}
