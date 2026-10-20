// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace E2E.Internal;

/// <summary>
/// The one way the runner asks git a question (upstream's <c>internal/git.ts</c>). Best-effort by design: git may be missing, slow, or
/// outside a repository, and none of that fails a run.
/// </summary>
internal static partial class Git
{
    /// <summary>How long git has to answer.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(1);

    /// <summary>A full SHA-1 or SHA-256 object name (upstream's <c>COMMIT_HASH</c>).</summary>
    public static Regex CommitHash { get; } = CommitHashPattern();

    [GeneratedRegex(@"\A[a-f0-9]{40,64}\z")]
    private static partial Regex CommitHashPattern();

    /// <summary>
    /// One git command's trimmed stdout; null when git is missing, slow, or refuses. A cancelled <paramref name="cancellationToken"/>
    /// still surfaces as a cancellation. <paramref name="budget"/> replaces the one-second budget (.NET-only, for tests).
    /// </summary>
    public static async Task<string?> RunAsync(string cwd, IReadOnlyList<string> args, CancellationToken cancellationToken = default, TimeSpan? budget = null)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
        };
        // Git never waits for the parent's terminal.
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(budget ?? Budget);
        Process? process = null;
        try
        {
            timeout.Token.ThrowIfCancellationRequested();
            process = Process.Start(info);
            if (process is null)
            {
                return null;
            }

            process.StandardInput.Close();

            // Both streams are read while the process runs, so a full pipe cannot hold it.
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var text = await stdout.ConfigureAwait(false);
            await stderr.ConfigureAwait(false);
            return process.ExitCode == 0 ? text.Trim() : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception error) when (error is Win32Exception or IOException or InvalidOperationException)
        {
            return null;
        }
        finally
        {
            if (process is not null)
            {
                KillTree(process);
                process.Dispose();
            }
        }
    }

    private static void KillTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception)
        {
            // It ended on its own between the check and the kill.
        }
    }
}
