// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E.Internal;
using E2E.Tests.ManagedProcess;
using Microsoft.Extensions.Time.Testing;
using Lock = E2E.Internal.FileLock;

namespace E2E.Tests.FileLock;

/// <summary>
/// Upstream's <c>describe('withFileLock')</c> in <c>file-lock.test.ts</c>. Where upstream replaces <c>statSync</c>, <c>writeFileSync</c>,
/// <c>rmSync</c>, <c>utimesSync</c>, or <c>process.kill</c>, the test passes a <see cref="HookedFileSystem"/> to the lock instead.
/// </summary>
public sealed class WithFileLockTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "e2e-lock-" + Guid.NewGuid().ToString("N"));

    public WithFileLockTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
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
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Hold<bool>(lockPath, () => Task.FromException<bool>(new InvalidOperationException("boom")))
        );
        Assert.False(File.Exists(lockPath));
        Assert.Equal("next", await Hold(lockPath, () => Task.FromResult("next")));
    }

    [Fact]
    public async Task Names_its_host_pid_namespace_and_pid_in_the_lock()
    {
        if (!File.Exists("/proc/self/ns/pid"))
        {
            return;
        }

        var lockPath = TempLock();
        var parts = await Hold(lockPath, async () => (await File.ReadAllTextAsync(lockPath)).Split(' '));
        Assert.Equal(
            [
                Environment.MachineName + "/" + new FileInfo("/proc/self/ns/pid").LinkTarget,
                Environment.ProcessId.ToString(),
            ],
            parts.Take(2)
        );
    }

    [Theory]
    [InlineData("this process")]
    [InlineData("a process of another user")]
    [InlineData("another host")]
    [InlineData("no pid")]
    public async Task Waits_for_a_fresh_lock_held_by(string holder)
    {
        var lockPath = TempLock();
        await File.WriteAllTextAsync(
            lockPath,
            holder switch
            {
                "this process" => HeldBy(Environment.ProcessId),
                // Run as root, pid 1 can be signalled, so this checks a running process instead.
                "a process of another user" => HeldBy(1),
                "another host" => "elsewhere.example " + ExitedPid() + " other",
                _ => Host() + " unknown other",
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
        await File.WriteAllTextAsync(lockPath, HeldBy(ExitedPid()));
        var started = Stopwatch.StartNew();
        Assert.Equal("entered", await Hold(lockPath, () => Task.FromResult("entered")));
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(1));
        Assert.False(File.Exists(lockPath));
    }

    [Fact]
    public async Task Takes_over_an_empty_lock_gone_untouched_as_an_older_e2e_leaves_it()
    {
        var lockPath = TempLock();
        await File.WriteAllTextAsync(lockPath, "");
        Age(lockPath, -60);
        Assert.Equal("entered", await Hold(lockPath, () => Task.FromResult("entered")));
    }

    [Fact]
    public async Task Takes_over_a_lock_its_holder_stopped_touching_even_when_the_pid_runs()
    {
        var lockPath = TempLock();
        // A pid that now belongs to another program, or a holder suspended past the stale bound.
        await File.WriteAllTextAsync(lockPath, HeldBy(Environment.ProcessId));
        Age(lockPath, -60);
        var (entered, pending) = await EntersAtOnce(lockPath);
        Assert.True(entered);
        await pending;
    }

    [Fact]
    public async Task Never_takes_over_a_lock_another_process_created_after_the_check()
    {
        var lockPath = TempLock();
        await File.WriteAllTextAsync(lockPath, "");
        Age(lockPath, -60);
        // Between this process's stale check and its takeover, another process takes the stale lock over and holds a fresh one.
        var replaced = false;
        var files = new HookedFileSystem
        {
            OnLastWrite = (path, stat) =>
            {
                if (!replaced)
                {
                    replaced = true;
                    File.Delete(lockPath);
                    File.WriteAllText(lockPath, HeldBy(Environment.ProcessId));
                }

                return stat;
            },
        };
        var (entered, pending) = await EntersAtOnce(lockPath, files);
        Assert.False(entered);
        File.Delete(lockPath);
        await pending;
    }

    [Theory]
    [InlineData("its first check, before its takeover turn", 1)]
    [InlineData("the check in its takeover turn", 2)]
    public async Task Lets_one_waiter_through_a_crashed_holders_lock_when_another_takes_it_over_during(
        string when,
        int check
    )
    {
        Assert.NotEmpty(when);
        var lockPath = TempLock();
        await File.WriteAllTextAsync(lockPath, HeldBy(ExitedPid()));
        var inside = 0;
        var most = 0;
        var gate = new object();
        async Task<bool> Work()
        {
            lock (gate)
            {
                most = Math.Max(most, ++inside);
            }

            await Task.Delay(50);
            lock (gate)
            {
                inside--;
            }

            return true;
        }

        // Each check asks whether the holder's pid still runs. On the chosen one, a second waiter runs its own takeover in the gap,
        // synchronously, as another process could between this waiter's check and its removal of the lock.
        var checks = 0;
        Task<bool>? other = null;
        HookedFileSystem? files = null;
        files = new HookedFileSystem
        {
            OnRunningCheck = _ =>
            {
                if (++checks == check)
                {
                    other = Hold(lockPath, Work, files);
                }
            },
        };
        await Hold(lockPath, Work, files);
        Assert.NotNull(other);
        await other;
        Assert.Equal(1, most);
    }

    // Real processes, as a soak check. Their races land at random, so a missing takeover turn shows up on most runs but
    // not all; the two-waiter cases above are the tests that fail every time.
    [Fact(Timeout = 120_000)]
    public async Task Lets_one_process_at_a_time_through_locks_that_holders_keep_crashing_with()
    {
        var lockPath = TempLock();
        var log = Path.Combine(_directory, "log");
        var stop = Path.Combine(_directory, "stop");
        await File.WriteAllTextAsync(log, "");
        // Every process boots before the start, then half the holds end as a crash that leaves the lock behind.
        var startAt = DateTimeOffset.UtcNow.AddSeconds(3).ToUnixTimeMilliseconds();
        var children = new List<Process>();
        int[] exitCodes;
        try
        {
            for (var index = 0; index < 12; index++)
            {
                var info = new ProcessStartInfo(AppProgram.Executable)
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                foreach (
                    var arg in new[]
                    {
                        "file-lock-worker",
                        lockPath,
                        log,
                        ExitedPid().ToString(System.Globalization.CultureInfo.InvariantCulture),
                        startAt.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        stop,
                    }
                )
                {
                    info.ArgumentList.Add(arg);
                }

                var child = Process.Start(info)!;
                children.Add(child);
                // Drain both pipes so a chatty worker never blocks on a full one.
                _ = child.StandardOutput.ReadToEndAsync();
                _ = child.StandardError.ReadToEndAsync();
            }

            // Each crash is one more race between waiters to take the lock over.
            var deadline = DateTimeOffset.UtcNow.AddSeconds(90);
            while (Crashes(log) < 300 && children.All(child => !child.HasExited || child.ExitCode == 0))
            {
                Assert.True(DateTimeOffset.UtcNow < deadline, "the workers did not reach 300 crashes in time");
                await Task.Delay(100);
            }

            await File.WriteAllTextAsync(stop, "");
            using var exitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            foreach (var child in children)
            {
                await child.WaitForExitAsync(exitTimeout.Token);
            }

            exitCodes = [.. children.Select(child => child.ExitCode)];
        }
        finally
        {
            // Whatever failed above: tell every started worker to stop, then kill what is left and release every handle.
            await File.WriteAllTextAsync(stop, "");
            foreach (var child in children)
            {
                try
                {
                    if (!child.HasExited)
                    {
                        child.Kill(entireProcessTree: true);
                        child.WaitForExit(TimeSpan.FromSeconds(10));
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Already gone, or not ours to stop: the next worker still gets its cleanup.
                    System.Diagnostics.Trace.WriteLine("could not stop a lock worker: " + ex.Message);
                }
                finally
                {
                    child.Dispose();
                }
            }
        }

        var lines = ReadShared(log).Trim().Split('\n');
        string? holder = null;
        var overlaps = new List<string>();
        for (var index = 0; index < lines.Length; index++)
        {
            var (mark, pid) = (lines[index][0], lines[index][1..]);
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
        Assert.All(exitCodes, code => Assert.Equal(0, code));
    }

    [Fact]
    public async Task Takes_over_the_turn_of_a_waiter_that_exited_during_it()
    {
        var lockPath = TempLock();
        await File.WriteAllTextAsync(lockPath, HeldBy(ExitedPid()));
        await File.WriteAllTextAsync(lockPath + ".break", HeldBy(ExitedPid()));
        var (entered, pending) = await EntersAtOnce(lockPath);
        Assert.True(entered);
        await pending;
        Assert.Empty(Directory.GetFileSystemEntries(_directory));
    }

    [Fact]
    public async Task Leaves_the_turn_of_a_live_waiter_whose_file_it_could_not_stat()
    {
        var lockPath = TempLock();
        await File.WriteAllTextAsync(lockPath, HeldBy(ExitedPid()));
        var breaker = lockPath + ".break";
        await File.WriteAllTextAsync(breaker, HeldBy(Environment.ProcessId));
        // The turn's holder runs, so the check reads its age, and that read fails as if the file had just been removed.
        var failed = false;
        var files = new HookedFileSystem
        {
            OnLastWrite = (path, stat) =>
            {
                if (!failed)
                {
                    failed = true;
                    throw new FileNotFoundException("no such file");
                }

                return stat;
            },
        };
        var (entered, pending) = await EntersAtOnce(lockPath, files);
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
        await Lock.WithFileLockAsync(
            lockPath,
            TimeSpan.FromSeconds(5),
            Busy,
            () =>
            {
                Age(lockPath, -60);
                // A holder touches its lock several times within the stale bound.
                clock.Advance(TimeSpan.FromSeconds(3));
                Assert.True(
                    File.GetLastWriteTimeUtc(lockPath) > DateTime.UtcNow - TimeSpan.FromSeconds(5),
                    "the lock was not touched"
                );
                return Task.FromResult(true);
            },
            clock: clock
        );
    }

    [Fact]
    public async Task Keeps_its_heartbeat_going_when_the_lock_vanishes_between_the_check_and_the_touch()
    {
        var lockPath = TempLock();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var failed = false;
        var files = new HookedFileSystem
        {
            OnSetLastWriteTime = _ =>
            {
                if (!failed)
                {
                    failed = true;
                    throw new FileNotFoundException("no such file");
                }
            },
        };
        await Lock.WithFileLockAsync(
            lockPath,
            TimeSpan.FromSeconds(5),
            Busy,
            () =>
            {
                Age(lockPath, -60);
                // The first touch fails, and the timer is not stopped by it: the next one marks the lock fresh again.
                var ex = Record.Exception(() => clock.Advance(TimeSpan.FromSeconds(3)));
                Assert.Null(ex);
                Assert.True(failed);
                clock.Advance(TimeSpan.FromSeconds(3));
                Assert.True(File.GetLastWriteTimeUtc(lockPath) > DateTime.UtcNow - TimeSpan.FromSeconds(5));
                return Task.FromResult(true);
            },
            clock: clock,
            fileSystem: files
        );
    }

    [Fact]
    public async Task Never_removes_or_touches_a_lock_it_no_longer_holds()
    {
        var lockPath = TempLock();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await Lock.WithFileLockAsync(
            lockPath,
            TimeSpan.FromSeconds(5),
            Busy,
            async () =>
            {
                // A waiter took the lock over and holds it now.
                await File.WriteAllTextAsync(lockPath, HeldBy(Environment.ProcessId));
                Age(lockPath, -60);
                clock.Advance(TimeSpan.FromSeconds(10));
                return true;
            },
            clock: clock
        );
        Assert.Equal(HeldBy(Environment.ProcessId), await File.ReadAllTextAsync(lockPath));
        Assert.True(File.GetLastWriteTimeUtc(lockPath) < DateTime.UtcNow - TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Leaves_the_lock_of_another_attempt_in_this_process_that_took_over()
    {
        var lockPath = TempLock();
        Task second = Task.CompletedTask;
        var finishSecond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await Hold(lockPath, async () =>
        {
            // Untouched past the stale bound, so a second attempt from this process takes it over.
            Age(lockPath, -60);
            var inside = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            second = Hold(lockPath, async () =>
            {
                inside.SetResult();
                await finishSecond.Task;
                return true;
            });
            await inside.Task;
            return true;
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
        await File.WriteAllTextAsync(lockPath, HeldBy(Environment.ProcessId));
        // Touched in the future, so the lock stays fresh however far the fake clock moves.
        Age(lockPath, 3_600);
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var pending = Lock.WithFileLockAsync(
            lockPath,
            TimeSpan.FromSeconds(30),
            Busy,
            () => Task.FromResult(true),
            clock: clock
        );
        await AdvanceAsync(clock, TimeSpan.FromSeconds(29));
        Assert.False(pending.IsCompleted);
        await AdvanceAsync(clock, TimeSpan.FromSeconds(2));
        Assert.Equal("busy", (await Assert.ThrowsAsync<InvalidOperationException>(() => pending)).Message);
    }

    [Fact]
    public async Task Gives_up_on_time_while_another_waiter_holds_the_takeover_turn()
    {
        var lockPath = TempLock();
        await File.WriteAllTextAsync(lockPath, HeldBy(ExitedPid()));
        Age(lockPath, -60);
        await File.WriteAllTextAsync(lockPath + ".break", HeldBy(Environment.ProcessId));
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var pending = Lock.WithFileLockAsync(
            lockPath,
            TimeSpan.FromSeconds(5),
            Busy,
            () => Task.FromResult(true),
            clock: clock
        );
        await AdvanceAsync(clock, TimeSpan.FromSeconds(6));
        await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
    }

    [Fact]
    public async Task Gives_up_on_time_while_the_locks_of_exiting_holders_keep_replacing_each_other()
    {
        var lockPath = TempLock();
        var exited = HeldBy(ExitedPid());
        await File.WriteAllTextAsync(lockPath, exited);
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        // Each takeover is followed by another holder taking the lock and exiting, a second later.
        var takeovers = 0;
        var files = new HookedFileSystem
        {
            OnDeleted = path =>
            {
                if (path == lockPath && takeovers++ < 20)
                {
                    File.WriteAllText(lockPath, exited);
                    clock.Advance(TimeSpan.FromSeconds(1));
                }
            },
        };
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Lock.WithFileLockAsync(
                lockPath,
                TimeSpan.FromSeconds(5),
                Busy,
                () => Task.FromResult(true),
                clock: clock,
                fileSystem: files
            )
        );
        Assert.Equal("busy", ex.Message);
        Assert.True(takeovers < 10, takeovers + " takeovers");
    }

    [Fact]
    public async Task Leaves_no_lock_behind_when_writing_its_holder_fails()
    {
        var lockPath = TempLock();
        var failed = false;
        var files = new HookedFileSystem
        {
            OnWriteToken = (stream, token) =>
            {
                if (!failed)
                {
                    failed = true;
                    stream.Write(token.AsSpan(0, 10));
                    throw new IOException("file too large");
                }

                stream.Write(token);
            },
        };
        var ex = await Assert.ThrowsAsync<IOException>(() => Hold(lockPath, () => Task.FromResult("entered"), files));
        Assert.Equal("file too large", ex.Message);
        Assert.Empty(Directory.GetFileSystemEntries(_directory));
        Assert.Equal("entered", await Hold(lockPath, () => Task.FromResult("entered"), files));
    }

    [Fact]
    public async Task Fails_at_once_when_the_lock_cannot_be_created()
    {
        var lockPath = Path.Combine(_directory, "missing", "x.lock");
        var started = Stopwatch.StartNew();
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => Hold(lockPath, () => Task.FromResult("entered")));
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(1));
    }

    /// <summary>A lock path in the test's directory.</summary>
    private string TempLock() => Path.Combine(_directory, "x.lock");

    private static string Host()
    {
        var link = File.Exists("/proc/self/ns/pid") ? new FileInfo("/proc/self/ns/pid").LinkTarget : null;
        return link is null ? Environment.MachineName : Environment.MachineName + "/" + link;
    }

    /// <summary>What a lock held by <paramref name="pid"/> on this host holds.</summary>
    private static string HeldBy(int pid) => Host() + " " + pid + " other";

    /// <summary>The pid of a process that has exited.</summary>
    private static int ExitedPid()
    {
        using var process = Process.Start(new ProcessStartInfo("true") { UseShellExecute = false })!;
        process.WaitForExit();
        return process.Id;
    }

    /// <summary>Sets the file's times <paramref name="seconds"/> from now.</summary>
    private static void Age(string file, int seconds) =>
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddSeconds(seconds));

    private static Exception Busy() => new InvalidOperationException("busy");

    /// <summary>Holds the lock at <paramref name="lockPath"/> for <paramref name="work"/>, giving up after 5 s.</summary>
    private static Task<T> Hold<T>(string lockPath, Func<Task<T>> work, FileLockFileSystem? files = null) =>
        Lock.WithFileLockAsync(lockPath, TimeSpan.FromSeconds(5), Busy, work, fileSystem: files);

    /// <summary>Whether <see cref="Hold{T}"/> enters within 150 ms; the attempt is left to finish once the lock is removed.</summary>
    private static async Task<(bool Entered, Task Pending)> EntersAtOnce(
        string lockPath,
        FileLockFileSystem? files = null
    )
    {
        var entered = false;
        var pending = Hold(
            lockPath,
            () =>
            {
                entered = true;
                return Task.FromResult(true);
            },
            files
        );
        await Task.Delay(150);
        return (entered, pending);
    }

    /// <summary>How many holders have crashed so far, as the workers' log counts them.</summary>
    private static int Crashes(string log) => ReadShared(log).Split('\n').Count(line => line.StartsWith('x'));

    /// <summary>The text of a file the workers are appending to.</summary>
    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// What upstream's tests do to <c>statSync</c>, <c>writeFileSync</c>, <c>rmSync</c>, <c>utimesSync</c>, and <c>process.kill</c> with
    /// <c>vi.mock</c> and <c>vi.spyOn</c>: each call goes through, after the hook has run, unless the hook throws.
    /// </summary>
    private sealed class HookedFileSystem : FileLockFileSystem
    {
        public Func<string, DateTimeOffset?, DateTimeOffset?>? OnLastWrite { get; init; }

        public Action<Stream, byte[]>? OnWriteToken { get; init; }

        public Action<string>? OnDeleted { get; init; }

        public Action<string>? OnSetLastWriteTime { get; init; }

        public Action<int>? OnRunningCheck { get; init; }

        public override DateTimeOffset? LastWrite(string path)
        {
            var stat = base.LastWrite(path);
            return OnLastWrite is null ? stat : OnLastWrite(path, stat);
        }

        public override void WriteToken(Stream stream, byte[] token)
        {
            if (OnWriteToken is null)
            {
                base.WriteToken(stream, token);
            }
            else
            {
                OnWriteToken(stream, token);
            }
        }

        public override void Delete(string path)
        {
            base.Delete(path);
            OnDeleted?.Invoke(path);
        }

        public override void SetLastWriteTimeUtc(string path, DateTime utc)
        {
            OnSetLastWriteTime?.Invoke(path);
            base.SetLastWriteTimeUtc(path, utc);
        }

        public override bool IsRunning(int pid)
        {
            OnRunningCheck?.Invoke(pid);
            return base.IsRunning(pid);
        }
    }

    /// <summary>Steps the fake clock in small turns, with a real yield before each, so a waiter's delay is seen between steps.</summary>
    private static async Task AdvanceAsync(FakeTimeProvider clock, TimeSpan by)
    {
        for (var elapsed = TimeSpan.Zero; elapsed < by; elapsed += TimeSpan.FromMilliseconds(25))
        {
            await Task.Delay(1);
            clock.Advance(TimeSpan.FromMilliseconds(25));
        }
    }
}
