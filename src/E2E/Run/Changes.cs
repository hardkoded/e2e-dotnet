// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E;

/// <summary>What <see cref="Changes.FindChangesAsync"/> asks git from, and when it gives up (upstream's <c>ChangeOptions</c>, <c>run/changes.ts</c>).</summary>
internal sealed record ChangeOptions
{
    /// <summary>The directory git is asked in: the project root.</summary>
    public required string Cwd { get; init; }

    /// <summary>The base ref <c>--changed-since</c> named.</summary>
    public required string ChangedSince { get; init; }

    /// <summary>The run's interrupt: cancelling it surfaces as a cancellation (upstream rejects with <c>INTERRUPTED</c>).</summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>The most time every git call together may take; 30 seconds by default (upstream's <c>budgetMs</c>).</summary>
    public TimeSpan Budget { get; init; } = Changes.GitBudget;
}

/// <summary>
/// The change a run's test filters judge: every file that differs from where HEAD branched off the ref, in the working tree, untracked
/// files included. A ref git cannot measure from is <c>INVALID_CHANGED_SINCE</c> (upstream's <c>run/changes.ts</c>).
/// </summary>
internal static class Changes
{
    /// <summary>Upstream's <c>GIT_BUDGET_MS</c>.</summary>
    internal static readonly TimeSpan GitBudget = TimeSpan.FromSeconds(30);

    /// <summary>How much one git call may print: a diff's file list on a large monorepo runs to megabytes (upstream's <c>GIT_MAX_BUFFER</c>, in characters).</summary>
    private const int GitMaxBuffer = 256 * 1024 * 1024;

    /// <summary>
    /// Finds the change the test filters judge: every file that differs from the merge base of the ref and HEAD, in the working tree,
    /// untracked files included, sorted; <c>Files</c> is empty when nothing changed. Every git call together has a 30 second budget; a
    /// ref git cannot measure from, in time or at all, is <c>INVALID_CHANGED_SINCE</c>.
    /// </summary>
    public static async Task<TestFilterChanges> FindChangesAsync(ChangeOptions options)
    {
        var reference = options.ChangedSince;
        if (reference.Length == 0 || reference[0] == '-')
        {
            // A ref git would read as an option (upstream rejects it at the CLI, before anything runs).
            throw InvalidChangedSince($"--changed-since {JsonQuote.Of(reference)}: a ref is a name, not an empty text or an option");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);
        deadline.CancelAfter(options.Budget);
        try
        {
            return await LookUp(reference, options.Cwd, deadline.Token).ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is not ConfigurationException)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            var why = cause is OperationCanceledException
                ? "git did not answer within " + Time.BudgetText((long)options.Budget.TotalMilliseconds)
                : cause is GitFailure ? cause.Message : cause.ToString();
            throw InvalidChangedSince($"--changed-since {JsonQuote.Of(reference)}: {why}", cause);
        }
    }

    /// <summary>The change since <paramref name="reference"/>, asking git within <paramref name="token"/>.</summary>
    private static async Task<TestFilterChanges> LookUp(string reference, string cwd, CancellationToken token)
    {
        var root = await RepositoryRoot(cwd, token).ConfigureAwait(false);
        var quoted = JsonQuote.Of(reference);
        var commit = await CommitOf(root, reference, token).ConfigureAwait(false)
            ?? throw InvalidChangedSince($"--changed-since {quoted} names no commit in this checkout; fetch it, such as with fetch-depth: 0 on actions/checkout or GIT_DEPTH: 0 on GitLab");
        var mergeBase = await MergeBase(root, commit, token).ConfigureAwait(false);
        if (mergeBase is null)
        {
            throw InvalidChangedSince(
                await IsShallow(root, token).ConfigureAwait(false)
                    ? $"--changed-since {quoted} shares no history with HEAD in this checkout; fetch more history, such as git fetch --unshallow"
                    : $"--changed-since {quoted} shares no history with HEAD: their histories are unrelated; name a ref on the history of HEAD");
        }

        return await ChangesSince(root, mergeBase, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Every file in the working tree that differs from <paramref name="mergeBase"/>, staged or not, and the untracked files, sorted by
    /// code point. An untracked nested repository, which git lists as its directory, is not this repository's change.
    /// </summary>
    private static async Task<TestFilterChanges> ChangesSince(string root, string mergeBase, CancellationToken token)
    {
        var diff = GitOutput(root, ["diff", "--name-only", "--no-renames", "--no-ext-diff", "-z", mergeBase, "--"], token);
        var untracked = GitOutput(root, ["ls-files", "--others", "--exclude-standard", "--full-name", "-z"], token);
        var outputs = await Task.WhenAll(diff, untracked).ConfigureAwait(false);
        var files = outputs[0].Split('\0')
            .Concat(outputs[1].Split('\0').Where(file => !file.EndsWith('/')))
            .Where(file => file.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(CodePointComparer.Instance)
            .ToArray();

        // As on disk, like the filter context's ProjectRoot: git prints C:/repo on Windows.
        return new TestFilterChanges(mergeBase, Paths.OnDiskPath(root), files);
    }

    /// <summary>The repository <paramref name="cwd"/> is in, by its absolute root.</summary>
    private static async Task<string> RepositoryRoot(string cwd, CancellationToken token)
    {
        var root = (await GitOutput(cwd, ["rev-parse", "--show-toplevel"], token).ConfigureAwait(false)).TrimEnd('\n').TrimEnd('\r');
        return root.Length == 0 ? throw new GitFailure("git rev-parse --show-toplevel printed no repository root") : root;
    }

    /// <summary>
    /// The commit <paramref name="rev"/> names in the checkout, or null when it names none here. The object is found first and peeled
    /// after, so a rev whose syntax takes the rest of the text, as <c>:/message</c> does, is not handed <c>^{commit}</c>.
    /// </summary>
    private static async Task<string?> CommitOf(string root, string rev, CancellationToken token)
    {
        var found = await Git.RunGitAsync(root, ["rev-parse", "--verify", "--quiet", rev], token, GitMaxBuffer).ConfigureAwait(false);
        var obj = found.Stdout.Trim();
        if (found.ExitCode != 0 || !Git.CommitHash.IsMatch(obj))
        {
            return null;
        }

        var peeled = await Git.RunGitAsync(root, ["rev-parse", "--verify", "--quiet", obj + "^{commit}"], token, GitMaxBuffer).ConfigureAwait(false);
        var commit = peeled.Stdout.Trim();
        return peeled.ExitCode == 0 && Git.CommitHash.IsMatch(commit) ? commit : null;
    }

    /// <summary>The merge base of <paramref name="commit"/> and HEAD, or null when the checkout holds no shared history.</summary>
    private static async Task<string?> MergeBase(string root, string commit, CancellationToken token)
    {
        var result = await Git.RunGitAsync(root, ["merge-base", commit, "HEAD"], token, GitMaxBuffer).ConfigureAwait(false);
        if (result.ExitCode == 1)
        {
            return null;
        }

        if (result.ExitCode != 0)
        {
            throw new GitFailure(FailureText(["merge-base"], result));
        }

        var mergeBase = result.Stdout.Trim();
        return Git.CommitHash.IsMatch(mergeBase) ? mergeBase : null;
    }

    /// <summary>Whether the checkout is shallow, so a missing merge base may be history it has not fetched.</summary>
    private static async Task<bool> IsShallow(string root, CancellationToken token) =>
        (await GitOutput(root, ["rev-parse", "--is-shallow-repository"], token).ConfigureAwait(false)).Trim() == "true";

    /// <summary>A git call's stdout; a non-zero exit is a <see cref="GitFailure"/> quoting what git said.</summary>
    private static async Task<string> GitOutput(string cwd, IReadOnlyList<string> args, CancellationToken token)
    {
        var result = await Git.RunGitAsync(cwd, args, token, GitMaxBuffer).ConfigureAwait(false);
        return result.ExitCode == 0 ? result.Stdout : throw new GitFailure(FailureText(args, result));
    }

    /// <summary><c>git rev-parse: not a git repository</c>: the command and the first line git wrote, without its <c>fatal:</c> prefix.</summary>
    private static string FailureText(IReadOnlyList<string> args, GitResult result)
    {
        var said = result.Stderr.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0);
        if (said is not null)
        {
            foreach (var prefix in new[] { "fatal: ", "error: " })
            {
                if (said.StartsWith(prefix, StringComparison.Ordinal))
                {
                    said = said[prefix.Length..];
                    break;
                }
            }
        }

        return $"git {(args.Count > 0 ? args[0] : "")}: {ReportFormat.Ellipsize(said ?? $"exited with code {result.ExitCode}", 200)}";
    }

    /// <summary><c>--changed-since</c> named something git cannot measure a change from.</summary>
    private static ConfigurationException InvalidChangedSince(string message, Exception? cause = null) =>
        cause is null ? new ConfigurationException("INVALID_CHANGED_SINCE", message) : new ConfigurationException("INVALID_CHANGED_SINCE", message, cause);
}

/// <summary>Orders text by Unicode code point, as upstream's <c>compareCodePoints</c> (<c>internal/globs.ts</c>) does.</summary>
internal sealed class CodePointComparer : IComparer<string>
{
    public static CodePointComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        using var left = (x ?? "").EnumerateRunes().GetEnumerator();
        using var right = (y ?? "").EnumerateRunes().GetEnumerator();
        while (true)
        {
            var hasLeft = left.MoveNext();
            var hasRight = right.MoveNext();
            if (!hasLeft || !hasRight)
            {
                return hasLeft.CompareTo(hasRight);
            }

            var order = left.Current.Value.CompareTo(right.Current.Value);
            if (order != 0)
            {
                return order;
            }
        }
    }
}
