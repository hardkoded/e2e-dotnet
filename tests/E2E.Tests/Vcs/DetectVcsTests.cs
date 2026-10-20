// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.Vcs;

/// <summary>Upstream's <c>describe('detectVcs')</c> (<c>tests/unit/vcs.test.ts</c>).</summary>
public sealed class DetectVcsTests : IDisposable
{
    private static readonly string Sha = new('a', 40);

    private readonly string _outside = Directory.CreateTempSubdirectory("e2e-vcs-").FullName;

    public void Dispose() => Directory.Delete(_outside, recursive: true);

    private static Dictionary<string, string?> Env(params (string Key, string Value)[] pairs) => pairs.ToDictionary(pair => pair.Key, pair => (string?)pair.Value);

    [Fact]
    public async Task Reads_the_commit_and_branch_of_a_git_checkout_over_CI_says_whether_it_is_dirty_and_names_no_branch_on_a_detached_HEAD()
    {
        var repo = Directory.CreateTempSubdirectory("e2e-vcs-repo-").FullName;
        try
        {
            var commit = GitRepo.InitRepo(repo);
            GitRepo.RunGit(repo, "switch", "--quiet", "-c", "feature");
            Assert.Equal(new VcsInfo(commit, "feature", false), await Internal.Vcs.DetectAsync(repo, Env(("GITHUB_SHA", Sha), ("GITHUB_REF_NAME", "main"))));
            File.WriteAllText(Path.Combine(repo, "file.txt"), "two\n");
            Assert.Equal(new VcsInfo(commit, "feature", true), await Internal.Vcs.DetectAsync(repo, Env()));
            GitRepo.RunGit(repo, "switch", "--quiet", "--detach");
            Assert.Equal(new VcsInfo(commit, null, true), await Internal.Vcs.DetectAsync(repo, Env()));
        }
        finally
        {
            Directory.Delete(repo, recursive: true);
        }
    }

    [Fact]
    public async Task Falls_back_to_the_CI_variables_outside_git()
    {
        Assert.Equal(new VcsInfo(Sha, "main"), await Internal.Vcs.DetectAsync(_outside, Env(("GITHUB_SHA", Sha), ("GITHUB_REF_NAME", "main"))));

        // A pull request run names the head branch, not the merge ref.
        Assert.Equal(new VcsInfo(Sha, "feature"), await Internal.Vcs.DetectAsync(_outside, Env(("GITHUB_SHA", Sha), ("GITHUB_HEAD_REF", "feature"), ("GITHUB_REF_NAME", "12/merge"))));
    }

    [Fact]
    public async Task Records_nothing_when_neither_git_nor_CI_knows_the_commit()
    {
        Assert.Null(await Internal.Vcs.DetectAsync(_outside, Env()));
        Assert.Null(await Internal.Vcs.DetectAsync(_outside, Env(("GITHUB_SHA", "not-a-sha"))));
    }
}
