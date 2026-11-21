// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

/// <summary>
/// Upstream's <c>tests/unit/helpers/file-lock-worker.ts</c>: one process for the crash-loop lock test. From <c>startAt</c> (epoch
/// milliseconds) until the file at <c>stop</c> exists it takes the lock at <c>lockPath</c> over and over. Each time it writes
/// <c>+pid</c> to the log on entering, then either leaves with <c>-pid</c> or, as if it crashed while holding the lock, writes
/// <c>xpid</c> and leaves the lock naming the exited pid. It writes <c>!pid</c> when its lock was removed or replaced while it held it.
/// </summary>
internal static class FileLockWorker
{
    public static async Task RunAsync(string lockPath, string log, string exitedPid, string startAt, string stop)
    {
        var pid = Environment.ProcessId;
        var delay =
            DateTimeOffset.FromUnixTimeMilliseconds(
                long.Parse(startAt, System.Globalization.CultureInfo.InvariantCulture)
            ) - DateTimeOffset.UtcNow;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay);
        }

        while (!File.Exists(stop))
        {
            await FileLock.WithFileLockAsync(
                lockPath,
                TimeSpan.FromSeconds(20),
                () => new InvalidOperationException("lock wait timed out"),
                async () =>
                {
                    Append(log, "+" + pid + "\n");
                    var token = ReadLock() ?? "";
                    await Task.Delay(1);
                    if (ReadLock() != token)
                    {
                        Append(log, "!" + pid + "\n");
                    }

                    if (Random.Shared.NextDouble() < 0.5)
                    {
                        Append(log, "-" + pid + "\n");
                        return true;
                    }

                    // What a crash leaves: this host's lock, naming a pid that no longer runs.
                    var host = token.Split(' ')[0];
                    Append(log, "x" + pid + "\n");
                    File.WriteAllText(lockPath, host + " " + exitedPid + " crashed");
                    return true;
                }
            );
        }

        string? ReadLock()
        {
            try
            {
                // Shared for delete, so the holder can remove the lock while this reads it.
                using var stream = new FileStream(
                    lockPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete
                );
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    /// <summary>Appends one line to the log shared by every worker, in one write, so lines never mix.</summary>
    private static void Append(string log, string line)
    {
        using var stream = new FileStream(
            log,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete
        );
        stream.Write(System.Text.Encoding.UTF8.GetBytes(line));
    }
}
