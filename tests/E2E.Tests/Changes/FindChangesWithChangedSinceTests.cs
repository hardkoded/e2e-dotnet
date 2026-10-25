// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.Changes;

/// <summary>Moves nothing: the tests that point <c>PATH</c> elsewhere run apart from every other test, since it is the process's.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessPathCollection
{
    public const string Name = "ProcessPath";
}

/// <summary>
/// Upstream's <c>describe('findChanges with --changed-since')</c> (<c>tests/unit/changes.test.ts</c>): the change test filters judge,
/// read from real throwaway git repositories.
/// </summary>
[Collection(ProcessPathCollection.Name)]
public sealed class FindChangesWithChangedSinceTests : IDisposable
{
    private readonly List<string> _dirs = [];

    public void Dispose()
    {
        foreach (var dir in _dirs)
        {
            GitRepo.RemoveRepo(dir);
        }
    }

    private string TempDir()
    {
        var dir = Directory.CreateTempSubdirectory("e2e-changes-").FullName;
        _dirs.Add(dir);
        return dir;
    }

    /// <summary>
    /// A repository whose <c>main</c> holds <c>file.txt</c> and <c>src/app.ts</c>, then a <c>feature</c> branch, checked out, that edits
    /// <c>src/app.ts</c> and adds <c>tests/new.e2e.ts</c>; <c>main</c> moves on after the branch point with <c>docs/later.md</c>, which the
    /// change never holds.
    /// </summary>
    private (string Dir, string Base) FeatureRepo()
    {
        var dir = TempDir();
        GitRepo.InitRepo(dir);
        var baseCommit = GitRepo.CommitFiles(dir, new Dictionary<string, string> { ["src/app.ts"] = "one\n" }, "app");
        GitRepo.RunGit(dir, "checkout", "--quiet", "-b", "feature");
        GitRepo.CommitFiles(dir, new Dictionary<string, string> { ["src/app.ts"] = "two\n", ["tests/new.e2e.ts"] = "test\n" }, "feature");
        GitRepo.RunGit(dir, "checkout", "--quiet", "main");
        GitRepo.CommitFiles(dir, new Dictionary<string, string> { ["docs/later.md"] = "later\n" }, "later");
        GitRepo.RunGit(dir, "checkout", "--quiet", "feature");
        return (dir, baseCommit);
    }

    /// <summary><c>FindChangesAsync</c> in <paramref name="cwd"/> over the change since <c>main</c> and a live token.</summary>
    private static Task<TestFilterChanges> LookUp(string cwd, string changedSince = "main", CancellationToken cancellationToken = default, TimeSpan? budget = null) =>
        E2E.Changes.FindChangesAsync(new ChangeOptions { Cwd = cwd, ChangedSince = changedSince, CancellationToken = cancellationToken, Budget = budget ?? E2E.Changes.GitBudget });

    /// <summary>The files a lookup found, failing the test when it found none.</summary>
    private static IReadOnlyList<string> FilesOf(TestFilterChanges lookup) =>
        lookup.Files.Count == 0 ? throw new InvalidOperationException($"expected a change since {lookup.Base}") : lookup.Files;

    /// <summary>A clone of <paramref name="dir"/>'s <paramref name="branch"/>, <paramref name="depth"/> commits deep, as actions/checkout makes one.</summary>
    private string ShallowClone(string dir, string branch, int depth)
    {
        var clone = Path.Combine(TempDir(), "shallow");
        GitRepo.RunGit(Path.GetDirectoryName(clone)!, "clone", "--quiet", "--depth", depth.ToString(System.Globalization.CultureInfo.InvariantCulture), "--branch", branch, new Uri(dir).AbsoluteUri, clone);
        return clone;
    }

    private static async Task<ConfigurationException> InvalidChangedSince(Task<TestFilterChanges> lookup)
    {
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => lookup);
        Assert.Equal("INVALID_CHANGED_SINCE", error.Code);
        return error;
    }

    /// <summary>Runs <paramref name="body"/> with <c>PATH</c> set to <paramref name="path"/>.</summary>
    private static async Task WithPath(string path, Func<Task> body)
    {
        var before = Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH", path);
        try
        {
            await body();
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", before);
        }
    }

    [Fact]
    public async Task Measures_from_the_merge_base_of_the_ref_and_HEAD_at_the_repository_root_and_sorted()
    {
        var (dir, baseCommit) = FeatureRepo();
        var lookup = await LookUp(dir);
        Assert.Equal(baseCommit, lookup.Base);
        Assert.Equal(Paths.OnDiskPath(dir), lookup.Root);
        Assert.Equal(["src/app.ts", "tests/new.e2e.ts"], lookup.Files);

        // Asked from a subdirectory, paths stay repository-relative, and untracked files outside it count too.
        File.WriteAllText(Path.Combine(dir, "root-new.ts"), "new\n");
        File.WriteAllText(Path.Combine(dir, "src", "new.ts"), "new\n");
        Assert.Equal(["root-new.ts", "src/app.ts", "src/new.ts", "tests/new.e2e.ts"], FilesOf(await LookUp(Path.Combine(dir, "src"))));
    }

    [Fact]
    public async Task Includes_staged_unstaged_deleted_and_untracked_files_those_in_an_untracked_directory_too_never_an_ignored_one_or_a_nested_repository_which_git_lists_as_its_directory_sorted_by_code_point()
    {
        var (dir, _) = FeatureRepo();
        File.WriteAllText(Path.Combine(dir, ".gitignore"), "build/\n");
        File.WriteAllText(Path.Combine(dir, "staged.ts"), "new\n");
        GitRepo.RunGit(dir, "add", "staged.ts");
        File.WriteAllText(Path.Combine(dir, "file.txt"), "edited\n");
        File.Delete(Path.Combine(dir, "src", "app.ts"));
        File.WriteAllText(Path.Combine(dir, "untracked.ts"), "new\n");
        File.WriteAllText(Path.Combine(dir, "\U0001F600.ts"), "new\n");
        File.WriteAllText(Path.Combine(dir, "～.ts"), "new\n");
        Directory.CreateDirectory(Path.Combine(dir, "build"));
        File.WriteAllText(Path.Combine(dir, "build", "out.js"), "ignored\n");
        Directory.CreateDirectory(Path.Combine(dir, "plain"));
        File.WriteAllText(Path.Combine(dir, "plain", "g.txt"), "new\n");
        Directory.CreateDirectory(Path.Combine(dir, "tools"));
        GitRepo.InitRepo(Path.Combine(dir, "tools"));
        var files = FilesOf(await LookUp(dir));
        Assert.Equal([".gitignore", "file.txt", "plain/g.txt", "src/app.ts", "staged.ts", "tests/new.e2e.ts", "untracked.ts", "～.ts", "\U0001F600.ts"], files);
    }

    [Fact]
    public async Task Never_passes_no_relative_which_git_before_2_28_such_as_Ubuntu_20_04s_2_25_rejects()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var (dir, _) = FeatureRepo();
        var bin = TempDir();
        var real = Directory.GetFiles("/usr/bin", "git").FirstOrDefault() ?? "/bin/git";
        var script = Path.Combine(bin, "git");
        File.WriteAllText(script, $"#!/bin/sh\nfor arg; do [ \"$arg\" = --no-relative ] && exit 129; done\nexec \"{real}\" \"$@\"\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        await WithPath(bin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"), async () => Assert.Equal(["src/app.ts", "tests/new.e2e.ts"], FilesOf(await LookUp(dir))));
    }

    [Fact]
    public async Task Lists_both_paths_of_a_rename()
    {
        var (dir, _) = FeatureRepo();
        GitRepo.RunGit(dir, "mv", "file.txt", "moved.txt");
        GitRepo.CommitFiles(dir, new Dictionary<string, string>(), "rename");
        Assert.Equal(["file.txt", "moved.txt", "src/app.ts", "tests/new.e2e.ts"], FilesOf(await LookUp(dir)));
    }

    [Fact]
    public async Task Finds_no_change_naming_the_merge_base_when_the_working_tree_matches_it()
    {
        var (dir, _) = FeatureRepo();
        var head = GitRepo.RunGit(dir, "rev-parse", "HEAD");
        Assert.Equal(new TestFilterChanges(head, Paths.OnDiskPath(dir), []), await LookUp(dir, "HEAD"), new ChangesComparer());
    }

    [Fact]
    public async Task Resolves_a_ref_whose_syntax_takes_the_rest_of_the_text_as_a_commit_message_search_does()
    {
        var (dir, baseCommit) = FeatureRepo();
        var subject = GitRepo.RunGit(dir, "log", "-1", "--format=%s", baseCommit);
        Assert.Equal(baseCommit, (await LookUp(dir, ":/" + subject)).Base);
    }

    [Fact]
    public async Task Is_INVALID_CHANGED_SINCE_for_a_ref_that_names_no_commit_or_one_with_unrelated_history()
    {
        var (dir, _) = FeatureRepo();
        var missing = await InvalidChangedSince(LookUp(dir, "origin/mian"));
        Assert.Equal("--changed-since \"origin/mian\" names no commit in this checkout; fetch it, such as with fetch-depth: 0 on actions/checkout or GIT_DEPTH: 0 on GitLab", missing.Message);
        GitRepo.RunGit(dir, "checkout", "--quiet", "--orphan", "unrelated");
        GitRepo.CommitFiles(dir, new Dictionary<string, string> { ["other.txt"] = "x\n" }, "unrelated");
        GitRepo.RunGit(dir, "checkout", "--quiet", "feature");

        // A full clone holds every commit: more history cannot help.
        var unrelated = await InvalidChangedSince(LookUp(dir, "unrelated"));
        Assert.Equal("--changed-since \"unrelated\" shares no history with HEAD: their histories are unrelated; name a ref on the history of HEAD", unrelated.Message);
    }

    [Fact]
    public async Task Is_INVALID_CHANGED_SINCE_saying_to_fetch_more_history_when_a_shallow_checkout_lacks_the_merge_base()
    {
        var (dir, _) = FeatureRepo();
        var clone = ShallowClone(dir, "feature", 1);
        GitRepo.RunGit(clone, "fetch", "--quiet", "--depth", "1", "origin", "main:refs/remotes/origin/main");
        var error = await InvalidChangedSince(LookUp(clone, "origin/main"));
        Assert.Equal("--changed-since \"origin/main\" shares no history with HEAD in this checkout; fetch more history, such as git fetch --unshallow", error.Message);
    }

    [Fact]
    public async Task Is_INVALID_CHANGED_SINCE_outside_a_repository_quoting_git_when_git_is_missing_and_when_git_runs_out_of_budget()
    {
        var outside = await InvalidChangedSince(LookUp(TempDir()));
        Assert.Matches(@"\A--changed-since ""main"": git rev-parse: not a git repository", outside.Message);

        var (dir, _) = FeatureRepo();
        await WithPath(TempDir(), async () => Assert.Equal("--changed-since \"main\": git was not found", (await InvalidChangedSince(LookUp(dir))).Message));

        var slow = await InvalidChangedSince(LookUp(dir, budget: TimeSpan.FromMilliseconds(1)));
        Assert.Equal("--changed-since \"main\": git did not answer within 1ms", slow.Message);
    }

    /// <summary>Partial: a cancelled token surfaces as a cancellation, where upstream rejects with <c>INTERRUPTED</c> (category <c>interrupted</c>) and the message <c>interrupted while finding the change the test filters judge</c>.</summary>
    [Fact]
    public async Task Partial_rejects_once_the_run_is_interrupted()
    {
        var (dir, _) = FeatureRepo();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => LookUp(dir, cancellationToken: cancelled.Token));
    }

    /// <summary>Compares the change by value, its files by element.</summary>
    private sealed class ChangesComparer : IEqualityComparer<TestFilterChanges>
    {
        public bool Equals(TestFilterChanges? x, TestFilterChanges? y) => x is not null && y is not null && x.Base == y.Base && x.Root == y.Root && x.Files.SequenceEqual(y.Files);

        public int GetHashCode(TestFilterChanges obj) => obj.Base.GetHashCode(StringComparison.Ordinal);
    }
}
