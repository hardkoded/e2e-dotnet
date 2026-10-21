// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Internal;

/// <summary>Where the code under test came from (upstream's <c>VcsInfo</c>, <c>internal/vcs.ts</c>).</summary>
/// <param name="Commit">The commit the working tree is checked out at.</param>
/// <param name="Branch">The branch name; null on a detached HEAD or when only the commit is known.</param>
/// <param name="Dirty">True when tracked files differ from the commit; null when git could not say.</param>
internal sealed record VcsInfo(string Commit, string? Branch = null, bool? Dirty = null);

/// <summary>
/// Where the code under test came from, for the report (upstream's <c>detectVcs</c>). A verdict without a commit cannot be joined to the
/// pull request that produced the code. Best-effort and quick: git is asked with a one-second budget, and a checkout without git falls
/// back to what CI tells the process. Nothing here fails a run.
/// </summary>
internal static class Vcs
{
    /// <summary>
    /// The commit, branch, and cleanliness of <paramref name="projectRoot"/>: from git when it answers, else from the CI variables
    /// GitHub Actions sets, else null. <paramref name="budget"/> replaces git's one-second budget (.NET-only, for tests on a loaded machine).
    /// </summary>
    public static async Task<VcsInfo?> DetectAsync(string projectRoot, IReadOnlyDictionary<string, string?> env, CancellationToken cancellationToken = default, TimeSpan? budget = null)
    {
        var commitTask = Git.RunAsync(projectRoot, ["rev-parse", "HEAD"], cancellationToken, budget);
        var branchTask = Git.RunAsync(projectRoot, ["rev-parse", "--abbrev-ref", "HEAD"], cancellationToken, budget);
        var statusTask = Git.RunAsync(projectRoot, ["status", "--porcelain", "--untracked-files=no"], cancellationToken, budget);
        await Task.WhenAll(commitTask, branchTask, statusTask).ConfigureAwait(false);
        var (commit, branch, status) = (commitTask.Result, branchTask.Result, statusTask.Result);
        if (commit is not null && Git.CommitHash.IsMatch(commit))
        {
            return new VcsInfo(commit, branch is null or "" or "HEAD" ? null : branch, status is null ? null : status.Length > 0);
        }

        if (!env.TryGetValue("GITHUB_SHA", out var ciCommit) || ciCommit is null || !Git.CommitHash.IsMatch(ciCommit))
        {
            return null;
        }

        // A pull request run names the head branch, not the merge ref.
        var ciBranch = env.TryGetValue("GITHUB_HEAD_REF", out var head) && !string.IsNullOrEmpty(head) ? head
            : env.TryGetValue("GITHUB_REF_NAME", out var named) && !string.IsNullOrEmpty(named) ? named
            : null;
        return new VcsInfo(ciCommit, ciBranch);
    }
}
