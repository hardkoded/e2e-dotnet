// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace E2E.Internal;

/// <summary>
/// A lock file shared across processes, for work that runs one process at a time (upstream's <c>internal/file-lock.ts</c>). The file
/// names its holder (<c>LockToken</c>), and the holder touches it while it holds it, so waiters take it over at once when a holder on
/// this host and pid namespace has exited, and otherwise only once it has gone untouched for the stale bound. The holder touches and
/// removes only a lock that names it.
/// </summary>
internal static class FileLock
{
    private const int LockRetryMs = 25;

    /// <summary>How long a lock may go untouched before waiters take it over, whoever it names.</summary>
    private static readonly TimeSpan LockStale = TimeSpan.FromSeconds(10);

    private static readonly UTF8Encoding Utf8 = new(false);

    /// <summary>Runs <paramref name="work"/> holding the lock at <paramref name="lockPath"/>; throws <paramref name="busy"/>'s error once another holder has kept it <paramref name="wait"/>.</summary>
    public static async Task<T> WithFileLockAsync<T>(
        string lockPath,
        TimeSpan wait,
        Func<Exception> busy,
        Func<Task<T>> work,
        FileLockSystem? system = null,
        CancellationToken cancellationToken = default
    )
    {
        system ??= FileLockSystem.Default;
        var release = await AcquireAsync(lockPath, wait, busy, system, cancellationToken).ConfigureAwait(false);
        try
        {
            return await work().ConfigureAwait(false);
        }
        finally
        {
            release();
        }
    }

    /// <summary>The <see cref="WithFileLockAsync{T}"/> of work that returns nothing.</summary>
    public static Task WithFileLockAsync(
        string lockPath,
        TimeSpan wait,
        Func<Exception> busy,
        Func<Task> work,
        FileLockSystem? system = null,
        CancellationToken cancellationToken = default
    ) =>
        WithFileLockAsync<bool>(
            lockPath,
            wait,
            busy,
            async () =>
            {
                await work().ConfigureAwait(false);
                return true;
            },
            system,
            cancellationToken
        );

    /// <summary>
    /// This host and, where the system has them, its pid namespace, since a sandbox on the same host sees none of the pids outside it.
    /// </summary>
    internal static string HolderHost()
    {
        try
        {
            var target = new FileInfo("/proc/self/ns/pid").LinkTarget;
            return target is null ? System.Net.Dns.GetHostName() : System.Net.Dns.GetHostName() + "/" + target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return System.Net.Dns.GetHostName();
        }
    }

    /// <summary>
    /// Waits for the lock and returns its release. A holder paused past the stale bound between the release's read and its remove can
    /// remove the lock of the waiter that took over: the same accepted cost as losing its own lock.
    /// </summary>
    private static async Task<Action> AcquireAsync(
        string lockPath,
        TimeSpan wait,
        Func<Exception> busy,
        FileLockSystem system,
        CancellationToken cancellationToken
    )
    {
        var token = LockToken();
        var deadline = system.Clock.GetUtcNow() + wait;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Claim(lockPath, token, system))
            {
                var heartbeat = system.Clock.CreateTimer(
                    _ =>
                    {
                        try
                        {
                            if (ReadLock(lockPath) == token)
                            {
                                system.Touch(lockPath);
                            }
                        }
                        catch (Exception)
                        {
                            // A missed beat costs nothing; a throw on a timer thread would end the process.
                        }
                    },
                    null,
                    LockStale / 4,
                    LockStale / 4
                );
                return () =>
                {
                    heartbeat.Dispose();
                    if (ReadLock(lockPath) == token)
                    {
                        system.Remove(lockPath);
                    }
                };
            }

            if (system.Clock.GetUtcNow() > deadline)
            {
                throw busy();
            }

            if (Abandoned(lockPath, system) && BreakAbandoned(lockPath, system))
            {
                continue;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(LockRetryMs), system.Clock, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Creates <paramref name="lockPath"/> holding <paramref name="token"/>, false when it exists; a lock whose write failed is removed.</summary>
    private static bool Claim(string lockPath, string token, FileLockSystem system)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        var stream = Create(lockPath, options);
        if (stream is null)
        {
            return false;
        }

        try
        {
            // Writes the whole token: a cut-off one could name a pid that has exited.
            system.WriteToken(stream, Utf8.GetBytes(token));
        }
        catch
        {
            stream.Dispose();
            system.Remove(lockPath);
            throw;
        }

        stream.Dispose();
        return true;
    }

    /// <summary>
    /// Creates the file exclusively; null when it exists. A file that vanished between the failed create and the check is created
    /// again once, and any other failure (a full disk, a missing directory) is the caller's.
    /// </summary>
    private static FileStream? Create(string lockPath, FileStreamOptions options)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return new FileStream(lockPath, options);
            }
            catch (IOException) when (File.Exists(lockPath))
            {
                return null;
            }
            catch (IOException) when (attempt == 0)
            {
                // Gone again already: the holder released between the two calls.
            }
        }
    }

    /// <summary>This process on this host, and a random part no other holder shares.</summary>
    private static string LockToken() =>
        HolderHost()
        + " "
        + Environment.ProcessId.ToString(CultureInfo.InvariantCulture)
        + " "
        + Guid.NewGuid().ToString();

    /// <summary>The lock's holder token, null when there is no lock.</summary>
    private static string? ReadLock(string lockPath)
    {
        try
        {
            using var stream = new FileStream(
                lockPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete
            );
            using var reader = new StreamReader(stream, Utf8);
            return reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// True when the lock's holder on this host and pid namespace has exited, or the lock has gone untouched for the stale bound. The
    /// bound covers what the pid cannot tell: a lock from another host, sandbox, or older e2e, a pid another program now has, and a
    /// holder suspended that long, which then loses its lock.
    /// </summary>
    private static bool Abandoned(string lockPath, FileLockSystem system)
    {
        var parts = (ReadLock(lockPath) ?? "").Split(' ');
        if (
            parts.Length > 1
            && parts[0] == HolderHost()
            && parts[1].Length > 0
            && parts[1].All(char.IsAsciiDigit)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
            && !system.IsRunning(pid)
        )
        {
            return true;
        }

        var touched = system.LastWriteTimeUtc(lockPath);
        return touched is not null && system.Clock.GetUtcNow() - touched.Value > LockStale;
    }

    /// <summary>
    /// Removes an abandoned lock, false when another waiter is removing it. Waiters take turns through a second lock file and check the
    /// holder again while they hold it, so a lock created after the first check is never removed. A waiter that exits inside its turn,
    /// which lasts microseconds, leaves that file for the next waiter to take over; two waiters doing so at the same moment can still
    /// both remove.
    /// </summary>
    private static bool BreakAbandoned(string lockPath, FileLockSystem system)
    {
        var breaker = lockPath + ".break";
        if (!Claim(breaker, LockToken(), system))
        {
            if (Abandoned(breaker, system))
            {
                system.Remove(breaker);
            }

            return false;
        }

        try
        {
            if (Abandoned(lockPath, system))
            {
                system.Remove(lockPath);
            }

            return true;
        }
        finally
        {
            system.Remove(breaker);
        }
    }
}

/// <summary>
/// What <see cref="FileLock"/> asks of the machine: the clock, file times, removal, and whether a pid runs. .NET-only: upstream's tests
/// replace <c>statSync</c>, <c>rmSync</c>, <c>writeFileSync</c>, <c>utimesSync</c>, <c>process.kill</c> and the fake timers; the port's
/// tests replace these members.
/// </summary>
internal class FileLockSystem(TimeProvider? clock = null)
{
    public static FileLockSystem Default { get; } = new();

    public virtual TimeProvider Clock => clock ?? TimeProvider.System;

    /// <summary>When the file was last written; null when it cannot be read (upstream's <c>statSync</c> failing).</summary>
    public DateTimeOffset? LastWriteTimeUtc(string path)
    {
        try
        {
            return Stat(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Upstream's <c>statSync(path).mtimeMs</c>: throws when the file is gone.</summary>
    protected internal virtual DateTimeOffset Stat(string path)
    {
        var info = new FileInfo(path);
        return info.Exists
            ? new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero)
            : throw new FileNotFoundException(path);
    }

    /// <summary>Writes the holder into the lock that was just created (upstream's <c>writeFileSync(fd, token)</c>).</summary>
    public virtual void WriteToken(FileStream stream, byte[] token)
    {
        stream.Write(token);
        stream.Flush();
    }

    /// <summary>Removes the file; one that is already gone is fine.</summary>
    public virtual void Remove(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A file another process holds open on Windows, or one that vanished: the holder check on the next turn decides.
        }
    }

    /// <summary>Marks the lock as held now.</summary>
    public void Touch(string path)
    {
        try
        {
            SetLastWriteTime(path, DateTime.UtcNow);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Released meanwhile.
        }
    }

    /// <summary>Upstream's <c>utimesSync</c>.</summary>
    protected internal virtual void SetLastWriteTime(string path, DateTime when)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(path);
        }

        File.SetLastWriteTimeUtc(path, when);
    }

    /// <summary>True while <paramref name="pid"/> exists, whoever owns it (upstream's <c>running</c>).</summary>
    public virtual bool IsRunning(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
