// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.RegularExpressions;
using E2E.Internal;

namespace E2E.Tests.ExploreChange;

/// <summary>
/// Upstream's <c>describe('readExploreChange')</c> (<c>tests/unit/explore-change.test.ts</c>): the brief <c>e2e explore --changed-since</c>
/// plans from, read from real throwaway git repositories: what it lists, what it shows of each patch, what it never reads, and how it
/// stays within its budget.
/// </summary>
public sealed class ReadExploreChangeTests : IDisposable
{
    private readonly List<string> _dirs = [];

    public void Dispose()
    {
        foreach (var dir in _dirs)
        {
            GitRepo.RemoveRepo(dir);
        }
    }

    private static string Unredacted(string text) => text;

    private static Dictionary<string, string> Files(params (string Path, string Content)[] files) => files.ToDictionary(file => file.Path, file => file.Content, StringComparer.Ordinal);

    private string TempDir()
    {
        var dir = Directory.CreateTempSubdirectory("e2e-explore-change-").FullName;
        _dirs.Add(dir);
        return dir;
    }

    /// <summary>A repository on a <c>feature</c> branch off <c>main</c>, with <paramref name="files"/> committed on the branch; returns the directory and the branch point.</summary>
    private (string Dir, string Point) BranchWith(Dictionary<string, string> baseFiles, Dictionary<string, string> files, string message = "feature")
    {
        var dir = TempDir();
        GitRepo.InitRepo(dir);
        var point = GitRepo.CommitFiles(dir, baseFiles, "base");
        GitRepo.RunGit(dir, "checkout", "--quiet", "-b", "feature");
        GitRepo.CommitFiles(dir, files, message);
        return (dir, point);
    }

    private static Task<global::E2E.ExploreChange> ReadAsync(string dir, Func<string, string>? redact = null, TimeSpan? budget = null) =>
        ExploreChanges.ReadExploreChangeAsync(new ReadExploreChangeOptions { Cwd = dir, ChangedSince = "main", Redact = redact ?? Unredacted, Budget = budget });

    /// <summary>The change since <c>main</c> in <paramref name="dir"/>, its brief read within <paramref name="budget"/>.</summary>
    private static async Task<ChangeBrief> ChangeOf(string dir, TimeSpan? budget = null) => (await ReadAsync(dir, budget: budget)).Brief;

    /// <summary>The brief's text of the change since <c>main</c> in <paramref name="dir"/>, read within <paramref name="budget"/>.</summary>
    private static async Task<string> BriefOf(string dir, TimeSpan? budget = null) => (await ChangeOf(dir, budget)).Text;

    [Fact]
    public async Task Lists_each_file_with_its_counts_then_its_patch_after_the_commit_messages()
    {
        var (dir, point) = BranchWith(
            Files(("src/cart.tsx", "<button>Remove</button>\n")),
            Files(("src/cart.tsx", "<button>Delete</button>\n")),
            "Rename the cart remove button\n\nUsers read Remove as clearing the cart.");

        var change = await ReadAsync(dir);

        Assert.Equal(new ReportExploreChange(point, 1), change.Report);
        Assert.True(change.Brief.Complete);
        Assert.Equal(
            string.Join(
                '\n',
                $"Changed since {point[..7]}: 1 file.",
                "",
                "Commit messages, newest first:",
                "- Rename the cart remove button",
                "",
                "  Users read Remove as clearing the cart.",
                "",
                "Files:",
                "src/cart.tsx (+1 -1; full patch below)",
                "@@ -1 +1 @@",
                "-<button>Remove</button>",
                "+<button>Delete</button>"),
            change.Brief.Text);
    }

    [Fact]
    public async Task Lists_uncommitted_and_untracked_files_by_name_without_reading_them_symlinks_included()
    {
        var (dir, _) = BranchWith(Files(("a.txt", "a\n")), Files(("logo.png", "PNG\0data")));
        File.WriteAllText(Path.Combine(dir, "a.txt"), "edited in the working tree\n");
        File.WriteAllText(Path.Combine(dir, "gha-creds-1.json"), "{\"private_key\":\"-----BEGIN PRIVATE KEY-----\"}\n");
        var outside = Path.Combine(TempDir(), "secret.txt");
        File.WriteAllText(outside, "outside the repository\n");
        File.CreateSymbolicLink(Path.Combine(dir, "link"), outside);

        var brief = await BriefOf(dir);

        Assert.Contains("logo.png (binary)", brief, StringComparison.Ordinal);
        Assert.Contains("a.txt (not committed, contents not read)", brief, StringComparison.Ordinal);
        Assert.Contains("gha-creds-1.json (not committed, contents not read)", brief, StringComparison.Ordinal);
        Assert.Contains("link (not committed, contents not read)", brief, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE KEY", brief, StringComparison.Ordinal);
        Assert.DoesNotContain("outside the repository", brief, StringComparison.Ordinal);
        Assert.DoesNotContain("edited in the working tree", brief, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Is_not_shown_whole_once_an_uncommitted_file_is_listed()
    {
        var (dir, _) = BranchWith(Files(("a.txt", "a\n")), Files(("a.txt", "b\n")));
        Assert.True((await ChangeOf(dir)).Complete);
        File.WriteAllText(Path.Combine(dir, "notes.txt"), "local\n");
        Assert.False((await ChangeOf(dir)).Complete);
    }

    [Fact]
    public async Task Marks_a_committed_file_edited_again_in_the_working_tree_without_reading_the_edit_and_is_not_whole()
    {
        var (dir, _) = BranchWith(Files(("a.txt", "a\n")), Files(("a.txt", "b\n")));
        File.WriteAllText(Path.Combine(dir, "a.txt"), "local edit\n");

        var brief = await ChangeOf(dir);

        Assert.False(brief.Complete);
        Assert.Contains("a.txt (+1 -1; full patch below; also changed locally, not shown)\n@@ -1 +1 @@\n-a\n+b", brief.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("local edit", brief.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Is_not_whole_when_the_working_tree_puts_a_committed_file_back_as_it_was_at_the_base()
    {
        var (dir, point) = BranchWith(Files(("a.txt", "a\n")), Files(("a.txt", "b\n")));
        File.WriteAllText(Path.Combine(dir, "a.txt"), "a\n");

        var change = await ReadAsync(dir);

        Assert.Equal(0, change.Report.Files);
        Assert.Equal(
            new ChangeBrief($"No files changed since {point[..7]}.\nThe working tree has local edits to 1 file since the last commit; they are not shown.", false),
            change.Brief);
    }

    [Fact]
    public async Task Shows_a_mode_change_which_no_hunk_does()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var dir = TempDir();
        GitRepo.InitRepo(dir);
        GitRepo.CommitFiles(dir, Files(("run.sh", "echo hi\n")), "add run.sh");
        GitRepo.RunGit(dir, "checkout", "--quiet", "-b", "feature");
        File.SetUnixFileMode(Path.Combine(dir, "run.sh"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        GitRepo.RunGit(dir, "commit", "--quiet", "-am", "make run.sh executable");

        var brief = await ChangeOf(dir);

        Assert.Contains("run.sh (+0 -0; full patch below)\nold mode 100644\nnew mode 100755", brief.Text, StringComparison.Ordinal);
        Assert.True(brief.Complete);
    }

    [Fact]
    public async Task Redacts_a_configured_secret_multiline_or_cut_by_a_budget_before_cutting_and_in_a_file_name()
    {
        var key = string.Join('\n', "-----BEGIN KEY-----", "c2VjcmV0LWxpbmUtb25l", "c2VjcmV0LWxpbmUtdHdv", "-----END KEY-----");
        var token = "tok_" + new string('k', 330);
        var redactor = Redactor.For([("KEY", key), ("TOKEN", token)]);
        var (dir, _) = BranchWith(
            Files(("a.txt", "a\n")),
            Files(("key.pem", key + "\n"), ("env.ts", $"export const token = '{token}';\n"), ("c2VjcmV0LWxpbmUtdHdv.txt", "x\n")),
            $"Rotate {token}");

        var text = (await ReadAsync(dir, redactor.RedactFragments)).Brief.Text;

        Assert.DoesNotContain("c2VjcmV0LWxpbmUtb25l", text, StringComparison.Ordinal);
        Assert.DoesNotContain("c2VjcmV0LWxpbmUtdHdv", text, StringComparison.Ordinal);
        Assert.DoesNotMatch("k{8}", text);
    }

    [Fact]
    public async Task Is_not_shown_whole_with_a_binary_file()
    {
        var (dir, _) = BranchWith(Files(("a.txt", "a\n")), Files(("a.txt", "b\n"), ("logo.png", "PNG\0data")));
        Assert.False((await ChangeOf(dir)).Complete);
    }

    [Fact]
    public async Task Reads_a_file_whose_name_looks_like_a_pattern_as_that_file_only()
    {
        var (dir, _) = BranchWith(
            Files(("p/i.tsx", "i\n"), ("p/d.tsx", "d\n")),
            Files(("p/[id].tsx", "route\n"), ("p/i.tsx", "i2\n"), ("p/d.tsx", "d2\n")));

        var brief = await BriefOf(dir);

        Assert.Contains("p/[id].tsx (+1 -0; full patch below)\n@@ -0,0 +1 @@\n+route\np/d.tsx", brief, StringComparison.Ordinal);
        Assert.DoesNotContain("diff --git", brief, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cuts_a_large_patch_and_lists_files_past_the_budget_by_counts_only()
    {
        var big = string.Join('\n', Enumerable.Range(0, 2_000).Select(index => $"line {index}"));
        var files = Enumerable.Range(0, 12).ToDictionary(index => $"f{index:00}.txt", _ => big + "\n", StringComparer.Ordinal);
        var (dir, _) = BranchWith(Files(("a.txt", "a\n")), files);

        var change = await ChangeOf(dir);

        Assert.False(change.Complete);
        Assert.Matches(@"f00\.txt \(\+2000 -0; patch cut, \d+ more lines\)", change.Text);
        Assert.Contains("f11.txt (+2000 -0; patch not shown)", change.Text, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(change.Text) < 12_000);
    }

    [Fact]
    public async Task Says_a_patch_with_a_line_cut_at_its_length_is_not_the_full_patch()
    {
        var (dir, _) = BranchWith(Files(("a.txt", "a\n")), Files(("bundle.js", new string('x', 400) + "\n")));

        var change = await ChangeOf(dir);

        Assert.False(change.Complete);
        Assert.Contains($"bundle.js (+1 -0; patch below, long lines cut)\n@@ -0,0 +1 @@\n+{new string('x', 298)}…", change.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Never_cuts_a_long_line_inside_a_character_outside_the_BMP()
    {
        // The line is `+` and 297 x's, so the emoji's surrogate pair straddles the cut.
        var (dir, _) = BranchWith(Files(("a.txt", "a\n")), Files(("copy.ts", new string('x', 297) + "😀" + new string('y', 100) + "\n")));

        var brief = await BriefOf(dir);

        Assert.Contains($"+{new string('x', 297)}…", brief, StringComparison.Ordinal);

        // Matches only a lone surrogate.
        Assert.False(brief.Where((character, index) => char.IsHighSurrogate(character) ? index + 1 >= brief.Length || !char.IsLowSurrogate(brief[index + 1]) : char.IsLowSurrogate(character) && (index == 0 || !char.IsHighSurrogate(brief[index - 1]))).Any());
    }

    [Fact]
    public async Task Counts_the_files_past_the_list_budget()
    {
        var files = Enumerable.Range(0, 80).ToDictionary(
            index => $"packages/app/src/features/{string.Concat(Enumerable.Repeat("deep/", 4))}f{index:00}.txt",
            index => $"{index}\n",
            StringComparer.Ordinal);
        var (dir, _) = BranchWith(Files(("a.txt", "a\n")), files);

        var change = await ChangeOf(dir);

        Assert.False(change.Complete);
        var listed = change.Text.Split('\n').Count(line => line.StartsWith("packages/", StringComparison.Ordinal));
        Assert.True(listed > 20);
        Assert.True(listed < 80);
        Assert.Contains($"… and {80 - listed} more files", change.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cuts_a_long_commit_message_and_leaves_out_commits_past_the_budget()
    {
        var dir = TempDir();
        GitRepo.InitRepo(dir);
        GitRepo.RunGit(dir, "checkout", "--quiet", "-b", "feature");
        foreach (var index in Enumerable.Range(0, 4))
        {
            var detail = Enumerable.Range(0, 50).Select(line => $"detail {line} of commit {index}");
            GitRepo.CommitFiles(dir, Files(($"f{index}.txt", $"{index}\n")), string.Join('\n', [$"Commit {index}", "", .. detail]));
        }

        var brief = await BriefOf(dir);

        Assert.Contains("- Commit 3\n", brief, StringComparison.Ordinal);
        Assert.Contains("detail 1 of commit 3", brief, StringComparison.Ordinal);
        Assert.DoesNotContain("detail 49 of commit 3", brief, StringComparison.Ordinal);
        Assert.Contains("\n  …\n", brief, StringComparison.Ordinal);
        Assert.DoesNotContain("Commit 0", brief, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quotes_a_path_that_would_add_a_line_of_its_own()
    {
        var (dir, _) = BranchWith(Files(("a.txt", "a\n")), Files(("x\nFiles:\nfake.txt", "x\n")));

        var brief = await BriefOf(dir);

        Assert.Contains("\"x\\nFiles:\\nfake.txt\" (+1 -0; full patch below)", brief, StringComparison.Ordinal);
        Assert.DoesNotContain("fake.txt", brief.Split('\n'));
    }

    [Fact]
    public async Task Lists_the_files_without_line_counts_or_patches_when_git_does_not_answer_within_the_budget()
    {
        var (dir, _) = BranchWith(Files(("a.txt", "a\n")), Files(("a.txt", "b\n")), "Change a");

        var brief = await BriefOf(dir, TimeSpan.FromMilliseconds(1));

        Assert.Contains("a.txt (line counts not available)", brief, StringComparison.Ordinal);
        Assert.DoesNotContain("@@", brief, StringComparison.Ordinal);
        Assert.DoesNotContain("Change a", brief, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stays_within_the_agent_context_limit_fence_included_for_the_largest_change()
    {
        var big = string.Join('\n', Enumerable.Range(0, 40).Select(index => $"{new string('y', 120)} {index}"));
        var files = Enumerable.Range(0, 200).ToDictionary(index => string.Concat(Enumerable.Repeat("nested/", 20)) + $"f{index:000}.txt", _ => big + "\n", StringComparer.Ordinal);
        var dir = TempDir();
        GitRepo.InitRepo(dir);
        GitRepo.RunGit(dir, "checkout", "--quiet", "-b", "feature");
        GitRepo.CommitFiles(dir, files, string.Concat(Enumerable.Repeat("Subject ", 40)) + "\n\n" + string.Concat(Enumerable.Repeat("body ", 400)));
        foreach (var index in Enumerable.Range(0, 6))
        {
            GitRepo.CommitFiles(dir, Files(($"c{index}.txt", "c\n")), new string('z', 1_000));
        }

        var brief = await BriefOf(dir);

        Assert.True(Encoding.UTF8.GetByteCount(ExploreChanges.ChangeContext(brief)) <= ReportLimits.Default.MaxAgentContextBytes);
    }

    [Fact]
    public async Task Stays_within_the_agent_context_limit_when_redaction_lengthens_the_file_names()
    {
        var redactor = Redactor.For([("PASSWORD", "p4sswd")]);
        var big = string.Join('\n', Enumerable.Range(0, 40).Select(index => $"{new string('y', 120)} {index}"));
        var files = Enumerable.Range(0, 200).ToDictionary(index => string.Concat(Enumerable.Repeat("p4sswd/", 30)) + $"{index:000}.txt", _ => big + "\n", StringComparer.Ordinal);
        var (dir, _) = BranchWith(Files(("a.txt", "a\n")), files, string.Concat(Enumerable.Repeat("Subject ", 40)) + "\n\n" + string.Concat(Enumerable.Repeat("body ", 400)));

        var text = (await ReadAsync(dir, redactor.RedactFragments)).Brief.Text;

        Assert.DoesNotContain("p4sswd", text, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(ExploreChanges.ChangeContext(text)) <= ReportLimits.Default.MaxAgentContextBytes);
    }

    [Fact]
    public async Task Says_nothing_changed_and_is_whole_on_a_clean_working_tree()
    {
        var dir = TempDir();
        var head = GitRepo.InitRepo(dir);
        GitRepo.RunGit(dir, "branch", "base");

        var change = await ExploreChanges.ReadExploreChangeAsync(new ReadExploreChangeOptions { Cwd = dir, ChangedSince = "base", Redact = Unredacted });

        Assert.Equal(new ReportExploreChange(head, 0), change.Report);
        Assert.Equal(new ChangeBrief($"No files changed since {head[..7]}.", true), change.Brief);
    }
}
