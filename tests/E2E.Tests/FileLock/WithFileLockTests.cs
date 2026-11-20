// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using Lock = E2E.Internal.FileLock;

namespace E2E.Tests.FileLock;

/// <summary>
/// Upstream's <c>describe('withFileLock')</c> in <c>file-lock.test.ts</c>. Not ported, because each needs a seam on the file system
/// or on <c>process.kill</c> that .NET has no way to mock: "never takes over a lock another process created after the check", the two
/// "lets one waiter through a crashed holder's lock when another takes it over during ..." cases, "lets one process at a time
/// through locks that holders keep crashing with" (needs the worker process), "leaves the turn of a live waiter whose file it could
/// not stat", "keeps its heartbeat going when the lock vanishes between the check and the touch", "gives up on time while the locks
/// of exiting holders keep replacing each other", and "leaves no lock behind when writing its holder fails".
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
    private static Task<T> Hold<T>(string lockPath, Func<Task<T>> work) =>
        Lock.WithFileLockAsync(lockPath, TimeSpan.FromSeconds(5), Busy, work);

    /// <summary>Whether <see cref="Hold{T}"/> enters within 150 ms; the attempt is left to finish once the lock is removed.</summary>
    private static async Task<(bool Entered, Task Pending)> EntersAtOnce(string lockPath)
    {
        var entered = false;
        var pending = Hold(lockPath, () =>
        {
            entered = true;
            return Task.FromResult(true);
        });
        await Task.Delay(150);
        return (entered, pending);
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
