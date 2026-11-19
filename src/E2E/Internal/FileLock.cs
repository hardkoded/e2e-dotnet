// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;

namespace E2E.Internal;

/// <summary>
/// A lock file shared across processes, for work that runs one process at a time (upstream's <c>internal/file-lock.ts</c>). The file
/// names its holder (<c>lockToken</c>), and the holder touches it while it holds it, so waiters take it over at once when a holder on
/// this host and pid namespace has exited, and otherwise only once it has gone untouched for the stale bound.
/// The holder touches and removes only a lock that names it.
/// </summary>
internal static class FileLock
{
    private static readonly TimeSpan LockRetry = TimeSpan.FromMilliseconds(25);

    /// <summary>How long a lock may go untouched before waiters take it over, whoever it names.</summary>
    private static readonly TimeSpan LockStale = TimeSpan.FromSeconds(10);

    private static readonly UTF8Encoding Utf8 = new(false);

    /// <summary>Runs <paramref name="work"/> holding the lock at <paramref name="lockPath"/>; throws <paramref name="busy"/>() once another holder has kept it <paramref name="wait"/>.</summary>
    public static async Task<T> WithFileLockAsync<T>(
        string lockPath,
        TimeSpan wait,
        Func<Exception> busy,
        Func<Task<T>> work,
        CancellationToken cancellationToken = default,
        TimeProvider? clock = null
    )
    {
        var release = await AcquireAsync(lockPath, wait, busy, cancellationToken, clock ?? TimeProvider.System)
            .ConfigureAwait(false);
        try
        {
            return await work().ConfigureAwait(false);
        }
        finally
        {
            release();
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
        CancellationToken cancellationToken,
        TimeProvider clock
    )
    {
        var token = LockToken();
        var deadline = clock.GetUtcNow() + wait;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Claim(lockPath, token))
            {
                var heartbeat = clock.CreateTimer(
                    _ =>
                    {
                        if (ReadLock(lockPath) == token)
                        {
                            Touch(lockPath, clock);
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
                        Remove(lockPath);
                    }
                };
            }

            if (clock.GetUtcNow() > deadline)
            {
                throw busy();
            }

            if (Abandoned(lockPath, clock) && BreakAbandoned(lockPath, clock))
            {
                continue;
            }

            await Task.Delay(LockRetry, clock, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Creates <paramref name="lockPath"/> holding <paramref name="token"/>, false when it exists; a lock whose write failed is removed.</summary>
    private static bool Claim(string lockPath, string token)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        FileStream stream;
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                stream = new FileStream(lockPath, options);
                break;
            }
            catch (IOException) when (File.Exists(lockPath))
            {
                // Another holder created it first.
                return false;
            }
            catch (IOException) when (attempt == 0 && Directory.Exists(Path.GetDirectoryName(lockPath)))
            {
                // It existed and went away between the create and the check: try once more. Any other failure is the caller's.
            }
        }

        try
        {
            // Writes the whole token: a cut-off one could name a pid that has exited.
            stream.Write(Utf8.GetBytes(token));
        }
        catch
        {
            stream.Dispose();
            File.Delete(lockPath);
            throw;
        }

        stream.Dispose();
        return true;
    }

    /// <summary>This host and, where the system has them, its pid namespace, since a sandbox on the same host sees none of the pids outside it.</summary>
    private static string HolderHost()
    {
        try
        {
            var link = new FileInfo("/proc/self/ns/pid").LinkTarget;
            if (link is not null)
            {
                return Environment.MachineName + "/" + link;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return Environment.MachineName;
    }

    /// <summary>This process on this host, and a random part no other holder shares.</summary>
    private static string LockToken() => HolderHost() + " " + Environment.ProcessId + " " + Guid.NewGuid();

    /// <summary>Marks the lock as held now.</summary>
    private static void Touch(string lockPath, TimeProvider clock)
    {
        try
        {
            File.SetLastWriteTimeUtc(lockPath, clock.GetUtcNow().UtcDateTime);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Released meanwhile.
        }
    }

    /// <summary>The lock's holder token, null when there is no lock.</summary>
    private static string? ReadLock(string lockPath)
    {
        try
        {
            // Shared for delete, so the holder can remove the lock while a waiter reads it, as Node's reads allow.
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
    private static bool Abandoned(string lockPath, TimeProvider clock)
    {
        var parts = (ReadLock(lockPath) ?? "").Split(' ');
        if (
            parts.Length > 1
            && parts[0] == HolderHost()
            && parts[1].Length > 0
            && parts[1].All(char.IsAsciiDigit)
            && int.TryParse(parts[1], out var pid)
            && !Running(pid)
        )
        {
            return true;
        }

        try
        {
            var modified = File.GetLastWriteTimeUtc(lockPath);
            return File.Exists(lockPath) && clock.GetUtcNow() - new DateTimeOffset(modified, TimeSpan.Zero) > LockStale;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>True while <paramref name="pid"/> exists, whoever owns it.</summary>
    private static bool Running(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The process exists but is not ours to inspect.
            return true;
        }
    }

    /// <summary>
    /// Removes an abandoned lock, false when another waiter is removing it. Waiters take turns through a second lock file and check
    /// the holder again while they hold it, so a lock created after the first check is never removed. A waiter that exits inside its
    /// turn, which lasts microseconds, leaves that file for the next waiter to take over; two waiters doing so at the same moment can
    /// still both remove.
    /// </summary>
    private static bool BreakAbandoned(string lockPath, TimeProvider clock)
    {
        var breaker = lockPath + ".break";
        if (!Claim(breaker, LockToken()))
        {
            if (Abandoned(breaker, clock))
            {
                Remove(breaker);
            }

            return false;
        }

        try
        {
            if (Abandoned(lockPath, clock))
            {
                Remove(lockPath);
            }

            return true;
        }
        finally
        {
            Remove(breaker);
        }
    }

    /// <summary>Removes <paramref name="path"/>; a file another process holds open stays for the next waiter, and never replaces the work's own result.</summary>
    private static void Remove(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
