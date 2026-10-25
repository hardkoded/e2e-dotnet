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
    /// The variables that point git at a repository. A hook or wrapper that runs the process sets them, and they would override
    /// <c>cwd</c> (.NET-only; upstream passes the parent's environment through).
    /// </summary>
    public static IReadOnlyList<string> RepositoryLocationVariables { get; } =
    [
        "GIT_DIR", "GIT_WORK_TREE", "GIT_COMMON_DIR", "GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY", "GIT_ALTERNATE_OBJECT_DIRECTORIES", "GIT_PREFIX",
    ];

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
        foreach (var inherited in RepositoryLocationVariables)
        {
            info.Environment.Remove(inherited);
        }

        // Git never waits for the parent's terminal.
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // CancelAfter fires on the thread pool, so a budget of zero could still lose the race to a git that answers at once.
        var allowed = budget ?? Budget;
        if (allowed == TimeSpan.Zero)
        {
            timeout.Cancel();
        }
        else
        {
            timeout.CancelAfter(allowed);
        }

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

    /// <summary>
    /// Runs git once in <paramref name="cwd"/>: its exit code and output, whatever the code (upstream's <c>runGit</c>). Git that is
    /// missing or prints past <paramref name="maxBuffer"/> characters of either stream is a <see cref="GitFailure"/>; a cancelled token
    /// surfaces as a cancellation, and the caller reads its own token for why.
    /// </summary>
    public static async Task<GitResult> RunGitAsync(string cwd, IReadOnlyList<string> args, CancellationToken cancellationToken = default, int maxBuffer = 1024 * 1024)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        foreach (var inherited in RepositoryLocationVariables)
        {
            info.Environment.Remove(inherited);
        }

        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        var name = args.Count > 0 ? args[0] : "";
        Process? process = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                process = Process.Start(info);
            }
            catch (Win32Exception)
            {
                throw new GitFailure("git was not found");
            }

            if (process is null)
            {
                throw new GitFailure("git was not found");
            }

            process.StandardInput.Close();

            // Both streams are read while the process runs, so a full pipe cannot hold it.
            var stdout = ReadBoundedAsync(process.StandardOutput, maxBuffer, cancellationToken);
            var stderr = ReadBoundedAsync(process.StandardError, maxBuffer, cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var text = await stdout.ConfigureAwait(false);
            var errors = await stderr.ConfigureAwait(false);
            if (text is null || errors is null)
            {
                throw new GitFailure($"git {name} printed more than {maxBuffer / 1024 / 1024} MiB");
            }

            return new GitResult(process.ExitCode, text, errors);
        }
        catch (Exception error) when (error is IOException or InvalidOperationException)
        {
            throw new GitFailure($"git {name}: {error.Message}");
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

    /// <summary>The text of <paramref name="reader"/>, or null once it passes <paramref name="maxChars"/> characters. The rest is still drained, so git is never held on a full pipe.</summary>
    private static async Task<string?> ReadBoundedAsync(StreamReader reader, int maxChars, CancellationToken cancellationToken)
    {
        var text = new System.Text.StringBuilder();
        var buffer = new char[8192];
        var over = false;
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (!over)
            {
                text.Append(buffer, 0, read);
                over = text.Length > maxChars;
            }
        }

        return over ? null : text.ToString();
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

/// <summary>One git call's exit code and output (upstream's <c>GitResult</c>).</summary>
internal sealed record GitResult(int ExitCode, string Stdout, string Stderr);

/// <summary>Git could not answer: it is missing, printed too much, or failed outright. The message says which (upstream's <c>GitFailure</c>).</summary>
internal sealed class GitFailure(string message) : Exception(message);
