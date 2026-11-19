// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using E2E.Internal;
using E2E.Tests.ManagedProcess;
using Microsoft.Extensions.Time.Testing;
using Lock = E2E.Internal.FileLock;

namespace E2E.Tests.FileLock;

/// <summary>Upstream's <c>file-lock.test.ts</c>: <c>describe('withFileLock')</c>.</summary>
public sealed class WithFileLockTests : IDisposable
{
    private const string NsPath = "/proc/self/ns/pid";

    private readonly List<string> _dirs = [];
    private string? _failed;

    public void Dispose()
    {
        foreach (var dir in _dirs)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Holds_the_lock_while_the_work_runs_and_removes_it_after()
    {
        var lockPath = TempLock();
        Assert.True(await Hold(lockPath, () => Task.FromResult(File.Exists(lockPath))));
        Assert.False(File.Exists(lockPath));
    }

    [Fact]
    public async Task Releases_the_lock_when_the_work_throws()
    {
        var lockPath = TempLock();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Hold<int>(lockPath, () => Task.FromException<int>(new InvalidOperationException("boom")))
        );
        Assert.Equal("boom", error.Message);
        Assert.False(File.Exists(lockPath));
        Assert.Equal("next", await Hold(lockPath, () => Task.FromResult("next")));
    }

    [Fact]
    public async Task Names_its_host_pid_namespace_and_pid_in_the_lock()
    {
        if (!File.Exists(NsPath))
        {
            return;
        }

        var lockPath = TempLock();
        var parts = await Hold(lockPath, () => Task.FromResult(File.ReadAllText(lockPath).Split(' ')));
        Assert.Equal(
            [
                System.Net.Dns.GetHostName() + "/" + new FileInfo(NsPath).LinkTarget,
                Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
            ],
            parts.Take(2)
        );
    }

    [Theory]
    [InlineData("this process")]
    // Run as root, pid 1 can be signalled, so this checks a running process instead.
    [InlineData("a process of another user")]
    [InlineData("another host")]
    [InlineData("no pid")]
    public async Task Waits_for_a_fresh_lock_held_by_(string holder)
    {
        if (holder == "a process of another user" && OperatingSystem.IsWindows())
        {
            return;
        }

        var lockPath = TempLock();
        File.WriteAllText(
            lockPath,
            holder switch
            {
                "this process" => HeldBy(Environment.ProcessId),
                "a process of another user" => HeldBy(1),
                "another host" => "elsewhere.example " + ExitedPid() + " other",
                _ => Host + " unknown other",
            }
        );
        var (entered, pending) = await EntersAtOnce(lockPath);
        Assert.False(entered);
        File.Delete(lockPath);
        await pending;
    }

    [Fact]
    public async Task Takes_over_at_once_a_lock_whose_holder_exited()
    {
        var lockPath = TempLock();
        File.WriteAllText(lockPath, HeldBy(ExitedPid()));
        var started = Stopwatch.StartNew();
        Assert.Equal("entered", await Hold(lockPath, () => Task.FromResult("entered")));
        Assert.True(started.ElapsedMilliseconds < 1_000);
        Assert.False(File.Exists(lockPath));
    }

    [Fact]
    public async Task Takes_over_an_empty_lock_gone_untouched_as_an_older_e2e_leaves_it()
    {
        var lockPath = TempLock();
        File.WriteAllText(lockPath, "");
        Age(lockPath, -60);
        Assert.Equal("entered", await Hold(lockPath, () => Task.FromResult("entered")));
    }

    [Fact]
    public async Task Takes_over_a_lock_its_holder_stopped_touching_even_when_the_pid_runs()
    {
        var lockPath = TempLock();
        // A pid that now belongs to another program, or a holder suspended past the stale bound.
        File.WriteAllText(lockPath, HeldBy(Environment.ProcessId));
        Age(lockPath, -60);
        var (entered, pending) = await EntersAtOnce(lockPath);
        Assert.True(entered);
        await pending;
    }

    [Fact]
    public async Task Never_takes_over_a_lock_another_process_created_after_the_check()
    {
        var lockPath = TempLock();
        File.WriteAllText(lockPath, "");
        Age(lockPath, -60);
        // Between this process's stale check and its takeover, another process takes the stale lock over and holds a fresh one.
        var once = true;
        var system = new LockSystem
        {
            OnStat = real =>
            {
                var stats = real();
                if (once)
                {
                    once = false;
                    File.Delete(lockPath);
                    File.WriteAllText(lockPath, HeldBy(Environment.ProcessId));
                }

                return stats;
            },
        };
        var (entered, pending) = await EntersAtOnce(lockPath, system);
        Assert.False(entered);
        File.Delete(lockPath);
        await pending;
    }

    [Theory]
    [InlineData("its first check, before its takeover turn", 1)]
    [InlineData("the check in its takeover turn", 2)]
    public async Task Lets_one_waiter_through_a_crashed_holders_lock_when_another_takes_it_over_during_(
        string when,
        int check
    )
    {
        Assert.NotEmpty(when);
        var lockPath = TempLock();
        File.WriteAllText(lockPath, HeldBy(ExitedPid()));
        var inside = 0;
        var most = 0;
        async Task Work()
        {
            most = Math.Max(most, Interlocked.Increment(ref inside));
            await Task.Delay(50);
            Interlocked.Decrement(ref inside);
        }

        // Each check asks whether the holder's pid still runs. On the chosen one, a second waiter runs its own takeover in the gap,
        // synchronously, as another process could between this waiter's check and its removal of the lock.
        var checks = 0;
        Task? other = null;
        var system = new LockSystem
        {
            OnIsRunning = (_, real) =>
            {
                if (++checks == check)
                {
                    other = Hold(lockPath, Work);
                }

                return real();
            },
        };
        await Hold(lockPath, Work, system);
        await other!;
        Assert.NotNull(other);
        Assert.Equal(1, most);
    }

    // Real processes, as a soak check. Their races land at random, so a missing takeover turn shows up on most runs but
    // not all; the two-waiter cases above are the tests that fail every time.
    [Fact]
    public async Task Lets_one_process_at_a_time_through_locks_that_holders_keep_crashing_with()
    {
        var lockPath = TempLock();
        var dir = Path.GetDirectoryName(lockPath)!;
        var log = Path.Combine(dir, "log");
        var stop = Path.Combine(dir, "stop");
        File.WriteAllText(log, "");
        // Every process boots before the start, then half the holds end as a crash that leaves the lock behind.
        var startAt = DateTimeOffset.UtcNow.AddSeconds(2).ToUnixTimeMilliseconds();
        string[] args =
        [
            "file-lock-worker",
            lockPath,
            log,
            ExitedPid().ToString(CultureInfo.InvariantCulture),
            startAt.ToString(CultureInfo.InvariantCulture),
            stop,
        ];
        // Killed when the test ends, even on a timeout, so a worker that hangs is not left running.
        var children = new List<Process>();
        try
        {
            var exits = Enumerable
                .Range(0, 12)
                .Select(worker =>
                {
                    var info = new ProcessStartInfo(AppProgram.Executable)
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };
                    foreach (var arg in args)
                    {
                        info.ArgumentList.Add(arg);
                    }

                    var child = Process.Start(info)!;
                    children.Add(child);
                    var error = child.StandardError.ReadToEndAsync();
                    _ = child.StandardOutput.ReadToEndAsync();
                    return Watch(child, error);
                })
                .ToList();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                // Each crash is one more race between waiters to take the lock over.
                while (Crashes(log) < 300 && _failed is null)
                {
                    await Task.Delay(100, timeout.Token);
                }
            }
            finally
            {
                File.WriteAllText(stop, "");
            }

            await Task.WhenAll(exits).WaitAsync(timeout.Token);
        }
        finally
        {
            foreach (var child in children)
            {
                try
                {
                    child.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // Already exited.
                    continue;
                }
                finally
                {
                    child.Dispose();
                }
            }
        }

        string? holder = null;
        List<string> overlaps = [];
        var lines = File.ReadAllText(log).Trim().Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var mark = lines[index][0];
            var pid = lines[index][1..];
            if (mark == '+' && holder is not null)
            {
                overlaps.Add("line " + (index + 1) + ": " + pid + " entered while " + holder + " held the lock");
            }

            if (mark == '!')
            {
                overlaps.Add("line " + (index + 1) + ": " + pid + " lost the lock while holding it");
            }

            holder =
                mark == '+' ? pid
                : mark is '-' or 'x' ? null
                : holder;
        }

        Assert.Empty(overlaps);
        Assert.True(_failed is null, _failed);
    }

    [Fact]
    public async Task Takes_over_the_turn_of_a_waiter_that_exited_during_it()
    {
        var lockPath = TempLock();
        File.WriteAllText(lockPath, HeldBy(ExitedPid()));
        File.WriteAllText(lockPath + ".break", HeldBy(ExitedPid()));
        var (entered, pending) = await EntersAtOnce(lockPath);
        Assert.True(entered);
        await pending;
        Assert.Empty(Directory.GetFileSystemEntries(Path.GetDirectoryName(lockPath)!));
    }

    [Fact]
    public async Task Leaves_the_turn_of_a_live_waiter_whose_file_it_could_not_stat()
    {
        var lockPath = TempLock();
        File.WriteAllText(lockPath, HeldBy(ExitedPid()));
        var breaker = lockPath + ".break";
        File.WriteAllText(breaker, HeldBy(Environment.ProcessId));
        // The turn's holder runs, so the check reads its age, and that read fails as if the file had just been removed.
        var once = true;
        var system = new LockSystem
        {
            OnStat = real =>
            {
                if (once)
                {
                    once = false;
                    throw new FileNotFoundException("no such file");
                }

                return real();
            },
        };
        var (entered, pending) = await EntersAtOnce(lockPath, system);
        Assert.False(entered);
        Assert.True(File.Exists(breaker));
        File.Delete(breaker);
        await pending;
    }

    [Fact]
    public async Task Keeps_a_held_lock_fresh_so_waiters_never_take_it_as_untouched()
    {
        var lockPath = TempLock();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var system = new LockSystem { FakeClock = clock };
        await Hold(
            lockPath,
            () =>
            {
                Age(lockPath, -60);
                // A holder touches its lock several times within the stale bound.
                clock.Advance(TimeSpan.FromSeconds(3));
                Assert.True(File.GetLastWriteTimeUtc(lockPath) > DateTime.UtcNow.AddSeconds(-5));
                return Task.CompletedTask;
            },
            system
        );
        var touches = system.Touches;
        // Released: the heartbeat is gone.
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(touches, system.Touches);
    }

    [Fact]
    public async Task Keeps_its_heartbeat_going_when_the_lock_vanishes_between_the_check_and_the_touch()
    {
        var lockPath = TempLock();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var once = true;
        var system = new LockSystem
        {
            FakeClock = clock,
            OnTouch = real =>
            {
                if (once)
                {
                    once = false;
                    throw new FileNotFoundException("no such file");
                }

                real();
            },
        };
        await Hold(
            lockPath,
            () =>
            {
                Assert.Null(Record.Exception(() => clock.Advance(TimeSpan.FromSeconds(3))));
                return Task.CompletedTask;
            },
            system
        );
    }

    [Fact]
    public async Task Never_removes_or_touches_a_lock_it_no_longer_holds()
    {
        var lockPath = TempLock();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var system = new LockSystem { FakeClock = clock };
        await Hold(
            lockPath,
            () =>
            {
                // A waiter took the lock over and holds it now.
                File.WriteAllText(lockPath, HeldBy(Environment.ProcessId));
                Age(lockPath, -60);
                clock.Advance(TimeSpan.FromSeconds(10));
                return Task.CompletedTask;
            },
            system
        );
        Assert.Equal(HeldBy(Environment.ProcessId), File.ReadAllText(lockPath));
        Assert.True(File.GetLastWriteTimeUtc(lockPath) < DateTime.UtcNow.AddSeconds(-30));
    }

    [Fact]
    public async Task Leaves_the_lock_of_another_attempt_in_this_process_that_took_over()
    {
        var lockPath = TempLock();
        Task second = Task.CompletedTask;
        var finishSecond = new TaskCompletionSource();
        await Hold(lockPath, async () =>
        {
            // Untouched past the stale bound, so a second attempt from this process takes it over.
            Age(lockPath, -60);
            var inside = new TaskCompletionSource();
            second = Hold(lockPath, () =>
            {
                inside.SetResult();
                return finishSecond.Task;
            });
            await inside.Task;
        });
        Assert.True(File.Exists(lockPath));
        finishSecond.SetResult();
        await second;
        Assert.False(File.Exists(lockPath));
    }

    [Fact]
    public async Task Gives_up_with_the_busy_error_once_another_holder_keeps_the_lock_past_the_wait()
    {
        var lockPath = TempLock();
        File.WriteAllText(lockPath, HeldBy(Environment.ProcessId));
        // Touched in the future, so the lock stays fresh however far the fake clock moves.
        Age(lockPath, 3_600);
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var pending = Lock.WithFileLockAsync(
            lockPath,
            TimeSpan.FromSeconds(30),
            Busy,
            () => Task.CompletedTask,
            new LockSystem { FakeClock = clock }
        );
        await AdvanceAsync(clock, TimeSpan.FromSeconds(29));
        Assert.False(pending.IsCompleted);
        await AdvanceAsync(clock, TimeSpan.FromSeconds(2));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
        Assert.Equal("busy", error.Message);
    }

    [Fact]
    public async Task Gives_up_on_time_while_another_waiter_holds_the_takeover_turn()
    {
        var lockPath = TempLock();
        File.WriteAllText(lockPath, HeldBy(ExitedPid()));
        Age(lockPath, -60);
        File.WriteAllText(lockPath + ".break", HeldBy(Environment.ProcessId));
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var pending = Hold(lockPath, () => Task.CompletedTask, new LockSystem { FakeClock = clock });
        await AdvanceAsync(clock, TimeSpan.FromSeconds(6));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
        Assert.Equal("busy", error.Message);
    }

    [Fact]
    public async Task Gives_up_on_time_while_the_locks_of_exiting_holders_keep_replacing_each_other()
    {
        var lockPath = TempLock();
        var exited = HeldBy(ExitedPid());
        File.WriteAllText(lockPath, exited);
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        // Each takeover is followed by another holder taking the lock and exiting, a second later.
        var takeovers = 0;
        var system = new LockSystem
        {
            FakeClock = clock,
            OnRemove = (path, real) =>
            {
                real();
                if (path == lockPath && takeovers++ < 20)
                {
                    File.WriteAllText(lockPath, exited);
                    clock.Advance(TimeSpan.FromSeconds(1));
                }
            },
        };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Hold(lockPath, () => Task.CompletedTask, system)
        );
        Assert.Equal("busy", error.Message);
        Assert.True(takeovers < 10);
    }

    [Fact]
    public async Task Leaves_no_lock_behind_when_writing_its_holder_fails()
    {
        var lockPath = TempLock();
        var once = true;
        var system = new LockSystem
        {
            OnWriteToken = (stream, token, real) =>
            {
                if (once)
                {
                    once = false;
                    stream.Write(token.AsSpan(0, 10));
                    throw new IOException("file too large");
                }

                real();
            },
        };
        var error = await Assert.ThrowsAsync<IOException>(() =>
            Hold(lockPath, () => Task.FromResult("entered"), system)
        );
        Assert.Equal("file too large", error.Message);
        Assert.Empty(Directory.GetFileSystemEntries(Path.GetDirectoryName(lockPath)!));
        Assert.Equal("entered", await Hold(lockPath, () => Task.FromResult("entered"), system));
    }

    [Fact]
    public async Task Fails_at_once_when_the_lock_cannot_be_created()
    {
        var lockPath = Path.Combine(Path.GetDirectoryName(TempLock())!, "missing", "x.lock");
        var started = Stopwatch.StartNew();
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => Hold(lockPath, () => Task.FromResult("entered")));
        Assert.True(started.ElapsedMilliseconds < 1_000);
    }

    /// <summary>Records the stderr of a worker that exits with a failure.</summary>
    private async Task Watch(Process child, Task<string> error)
    {
        await child.WaitForExitAsync();
        if (child.ExitCode != 0)
        {
            _failed = await error;
        }
    }

    private static string Host { get; } = Lock.HolderHost();

    /// <summary>What a lock held by <paramref name="pid"/> on this host holds.</summary>
    private static string HeldBy(int pid) => Host + " " + pid.ToString(CultureInfo.InvariantCulture) + " other";

    /// <summary>The pid of a process that has exited.</summary>
    private static int ExitedPid()
    {
        using var process = Process.Start(
            new ProcessStartInfo(AppProgram.Executable, ["exit", "0"]) { UseShellExecute = false }
        )!;
        var pid = process.Id;
        process.WaitForExit();
        return pid;
    }

    /// <summary>Sets the file's times <paramref name="seconds"/> from now.</summary>
    private static void Age(string file, int seconds) =>
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddSeconds(seconds));

    private static InvalidOperationException Busy() => new("busy");

    private static int Crashes(string log) => File.ReadAllText(log).Split('\n').Count(line => line.StartsWith('x'));

    /// <summary>Holds the lock at <paramref name="lockPath"/> for <paramref name="work"/>, giving up after 5 s.</summary>
    private static Task<T> Hold<T>(string lockPath, Func<Task<T>> work, FileLockSystem? system = null) =>
        Lock.WithFileLockAsync(lockPath, TimeSpan.FromSeconds(5), Busy, work, system);

    private static Task Hold(string lockPath, Func<Task> work, FileLockSystem? system = null) =>
        Lock.WithFileLockAsync(lockPath, TimeSpan.FromSeconds(5), Busy, work, system);

    /// <summary>Whether <see cref="Hold(string, Func{Task}, FileLockSystem?)"/> enters within 150 ms; the attempt is left to finish once the lock is removed.</summary>
    private static async Task<(bool Entered, Task Pending)> EntersAtOnce(string lockPath, FileLockSystem? system = null)
    {
        var entered = false;
        var pending = Hold(
            lockPath,
            () =>
            {
                entered = true;
                return Task.CompletedTask;
            },
            system
        );
        await Task.Delay(150);
        return (entered, pending);
    }

    /// <summary>Moves a fake clock in steps of the lock's retry wait, letting each wait that ends run before the next step.</summary>
    private static async Task AdvanceAsync(FakeTimeProvider clock, TimeSpan by)
    {
        var step = TimeSpan.FromMilliseconds(25);
        for (var left = by; left > TimeSpan.Zero; left -= step)
        {
            clock.Advance(left < step ? left : step);
            await Task.Delay(1);
        }
    }

    private string TempLock()
    {
        var dir = Path.Combine(Path.GetTempPath(), "e2e-lock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _dirs.Add(dir);
        return Path.Combine(dir, "x.lock");
    }
}
