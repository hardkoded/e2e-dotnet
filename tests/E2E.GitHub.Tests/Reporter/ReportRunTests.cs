// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Reports;
using static E2E.GitHub.Tests.FakeGitHub;
using static E2E.GitHub.Tests.Fixtures;

namespace E2E.GitHub.Tests.Reporter;

/// <summary>
/// Upstream's <c>describe('reportRun')</c> (<c>tests/unit/reporter.test.ts</c>): the reporter end to end against fakes, where it posts, where it
/// writes, and what each row says.
/// </summary>
public sealed class ReportRunTests
{
    private static readonly Func<HttpResponseMessage> Posted = () => Json(201, new { id = 5, html_url = "https://github.com/octo/app/pull/41#issuecomment-5" });

    private static readonly Dictionary<string, Func<Call, HttpResponseMessage>> Script = new()
    {
        ["GET *"] = _ => Json(200, Array.Empty<object>()),
        ["POST *"] = _ => Posted(),
    };

    private static readonly FinishedRun FailedRun = Finished(ReportOf(
        ReportRunStatus.Failed,
        [
            Result(
                "checkout",
                ResultStatus.Failed,
                file: "tests/shop flows/cart.e2e.ts",
                line: 9,
                attempts: [Attempt(AttemptStatus.Failed, error: Error("ASSERTION_FAILED", "no cart"), artifacts: [ArtifactKind.Screenshot])]),
        ]));

    /// <summary>Fakes for the reporter; <c>gitDirs</c> are the <c>.git</c> entries that exist, so the checkout root is where the test puts it.</summary>
    private sealed class Harness
    {
        public Harness(Dictionary<string, string> env, FakeGitHub? fake = null, string[]? gitDirs = null)
        {
            Fake = fake ?? new FakeGitHub(Script);
            Deps = new ReportDeps(
                FakeGitHub.Env(env),
                ReadEvent,
                Fake.Client,
                (file, text, _) =>
                {
                    if (file == "/readonly.md")
                    {
                        throw new IOException("EACCES: permission denied");
                    }

                    Written[file] = Written.GetValueOrDefault(file, "") + text;
                    return Task.CompletedTask;
                },
                file => (gitDirs ?? ["/work/.git"]).Contains(file));
        }

        public FakeGitHub Fake { get; }

        public ReportDeps Deps { get; }

        public Dictionary<string, string> Written { get; } = [];

        public List<Call> Calls => Fake.Calls;

        public string PostedBody => Calls.FirstOrDefault(call => call.Method == "POST")?.BodyText ?? "";

        public Task<IReadOnlyList<ReporterSummaryRow>> Report(FinishedRun run, GitHubOptions? options = null) =>
            GitHubReporter.ReportRunAsync(run, options ?? new GitHubOptions(), Deps, CancellationToken.None);
    }

    private static Dictionary<string, string> Env(params (string Name, string Value)[] extra)
    {
        var env = ActionsEnv();
        foreach (var (name, value) in extra)
        {
            env[name] = value;
        }

        return env;
    }

    private static Dictionary<string, string> Authorized(params (string Name, string Value)[] extra) => Env([("GITHUB_TOKEN", "ghs"), .. extra]);

    private static void AssertRows(IReadOnlyList<ReporterSummaryRow> actual, params string[] texts) =>
        Assert.Equal(texts.Select(text => new ReporterSummaryRow("GitHub", text)), actual);

    [Fact]
    public async Task Posts_nothing_off_GitHub_Actions()
    {
        var d = new Harness(new() { ["CI"] = "1", ["GITLAB_CI"] = "true" });

        AssertRows(await d.Report(FailedRun), "not posted: not running on GitHub Actions");
        Assert.Empty(d.Calls);
        Assert.Empty(d.Written);
    }

    [Fact]
    public async Task Writes_the_job_summary_and_posts_one_comment_with_the_marker_encoded_source_links_and_the_run_link()
    {
        var d = new Harness(Authorized());

        AssertRows(await d.Report(FailedRun), "https://github.com/octo/app/pull/41#issuecomment-5");
        var summary = d.Written["/summary.md"];
        Assert.Contains("### 🔴 e2e: 1 failed", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("<!-- e2e-github", summary, StringComparison.Ordinal);
        Assert.Equal(
            ["GET /user", "GET /repos/octo/app/issues/41/comments", "POST /repos/octo/app/issues/41/comments"],
            d.Calls.Select(call => call.Route));
        var body = d.PostedBody;
        Assert.StartsWith("<!-- e2e-github project=dev.example.shop workflow=e2e job=test -->\n", body, StringComparison.Ordinal);
        Assert.Contains("[tests/shop flows/cart.e2e.ts:9](https://github.com/octo/app/blob/head-sha/tests/shop%20flows/cart.e2e.ts#L9)", body, StringComparison.Ordinal);
        Assert.Contains("Evidence: [screenshot](https://github.com/octo/app/actions/runs/99#artifacts)", body, StringComparison.Ordinal);
        Assert.Contains("[run artifacts](https://github.com/octo/app/actions/runs/99#artifacts)", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Links_sources_under_the_project_path_inside_the_checkout_and_at_the_root_when_the_project_is_the_checkout()
    {
        const string Cart = "tests/shop%20flows/cart.e2e.ts#L9)";
        const string Blob = "(https://github.com/octo/app/blob/head-sha/";

        // The checkout is the workspace (.git at /work); the project is its packages-style child.
        var nested = new Harness(Authorized(("GITHUB_WORKSPACE", "/work")));
        await nested.Report(FailedRun);
        Assert.Contains($"{Blob}app/{Cart}", nested.PostedBody, StringComparison.Ordinal);

        // actions/checkout with `path: app`: the repository root is below the workspace, and the project is that root.
        var checkedOutBelow = new Harness(Authorized(("GITHUB_WORKSPACE", "/work")), gitDirs: ["/work/app/.git"]);
        await checkedOutBelow.Report(FailedRun);
        Assert.Contains($"{Blob}{Cart}", checkedOutBelow.PostedBody, StringComparison.Ordinal);

        // No .git anywhere: the workspace is taken for the checkout.
        var bare = new Harness(Authorized(("GITHUB_WORKSPACE", "/work")), gitDirs: []);
        await bare.Report(FailedRun);
        Assert.Contains($"{Blob}app/{Cart}", bare.PostedBody, StringComparison.Ordinal);

        var root = new Harness(Authorized(("GITHUB_WORKSPACE", "/work/app")));
        await root.Report(FailedRun);
        Assert.Contains($"{Blob}{Cart}", root.PostedBody, StringComparison.Ordinal);

        var outside = new Harness(Authorized(("GITHUB_WORKSPACE", "/elsewhere")));
        await outside.Report(FailedRun);
        Assert.Contains($"{Blob}{Cart}", outside.PostedBody, StringComparison.Ordinal);

        // A project directory may begin with two dots; only a real parent traversal drops the prefix.
        var dotted = new Harness(Authorized(("GITHUB_WORKSPACE", "/work")));
        await dotted.Report(FailedRun with { ProjectRoot = "/work/..app" });
        Assert.Contains($"{Blob}..app/{Cart}", dotted.PostedBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Folds_a_last_failed_rerun_into_the_run_it_selected_from_so_the_comment_shows_the_whole_suite_with_the_recovered_test_flaky()
    {
        var d = new Harness(Authorized());
        var failedAttempt = Attempt(AttemptStatus.Failed, error: Error("ASSERTION_FAILED", "no cart"));
        var firstPass = ReportOf(
            ReportRunStatus.Failed,
            [Result("steady", ResultStatus.Passed, attempts: [Attempt()]), Result("recovers", ResultStatus.Failed, attempts: [failedAttempt])]);
        var rerun = ReportOf(results:
        [
            Result("steady", ResultStatus.Skipped, selected: false, skip: new SkipInfo { Cause = SkipCause.Filtered, Reason = "did not fail in the last run" }),
            Result("recovers", ResultStatus.Passed, attempts: [Attempt()]),
        ]);

        await d.Report(Finished(rerun, firstPass));

        Assert.Contains("### 🟢 e2e: 1 flaky, 1 passed", d.PostedBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_a_test_run_and_an_exploration_in_one_job_as_two_comments_each_updated_in_place()
    {
        var explored = ReportOf(explore: new ReportExplore("Explore checkout", new ExploreBudgets(8, TimeSpan.FromMinutes(10)), ReportExploreEnded.Finished, [], []));

        // One pull request whose comments persist across the job's runs.
        List<(int Id, string Body)> comments = [];
        string CommentUrl(int id) => $"https://github.com/octo/app/pull/41#issuecomment-{id}";
        var fake = new FakeGitHub(new Dictionary<string, Func<Call, HttpResponseMessage>>
        {
            ["GET /repos/octo/app/issues/41/comments"] = _ => Json(
                200,
                comments.Select(comment => new { id = comment.Id, body = comment.Body, user = new { login = "github-actions[bot]", type = "Bot" } }).ToList()),
            ["POST /repos/octo/app/issues/41/comments"] = call =>
            {
                var id = comments.Count + 1;
                comments.Add((id, call.BodyText));
                return Json(201, new { id, html_url = CommentUrl(id) });
            },
            ["PATCH *"] = call =>
            {
                var id = int.Parse(call.Path[(call.Path.LastIndexOf('/') + 1)..], System.Globalization.CultureInfo.InvariantCulture);
                comments[comments.FindIndex(comment => comment.Id == id)] = (id, call.BodyText);
                return Json(200, new { id, html_url = CommentUrl(id) });
            },
        });
        var shared = new Harness(Authorized(), fake);

        foreach (var pass in new[] { 1, 2 })
        {
            await shared.Report(FailedRun);
            await shared.Report(Finished(explored));
            Assert.True(comments.Count == 2, $"pass {pass}");
        }

        Assert.StartsWith("<!-- e2e-github project=dev.example.shop workflow=e2e job=test -->\n### 🔴 e2e: 1 failed", comments[0].Body, StringComparison.Ordinal);
        Assert.StartsWith("<!-- e2e-github project=dev.example.shop workflow=e2e job=test kind=explore -->\n### 🟢 e2e explore: ", comments[1].Body, StringComparison.Ordinal);
        Assert.Equal(
            ["/repos/octo/app/issues/41/comments", "/repos/octo/app/issues/41/comments"],
            fake.Calls.Where(call => call.Method == "POST").Select(call => call.Path));
    }

    [Fact]
    public async Task Adds_the_key_to_the_marker_encoded_so_matrix_replicas_keep_their_own_comments()
    {
        const string Prefix = "<!-- e2e-github project=dev.example.shop workflow=e2e job=test";
        var d = new Harness(Authorized());
        await d.Report(FailedRun, new GitHubOptions { Key = "firefox --> 2" });
        Assert.StartsWith($"{Prefix} key=firefox%20--%3E%202 -->\n", d.PostedBody, StringComparison.Ordinal);

        var blank = new Harness(Authorized());
        await blank.Report(FailedRun, new GitHubOptions { Key = "" });
        Assert.StartsWith($"{Prefix} -->\n", blank.PostedBody, StringComparison.Ordinal);

        // A key that fits is the encoded key itself, byte for byte, so comments already on open pull requests are found.
        var fits = new Harness(Authorized());
        await fits.Report(FailedRun, new GitHubOptions { Key = new string('k', 200) });
        Assert.StartsWith($"{Prefix} key={new string('k', 200)} -->\n", fits.PostedBody, StringComparison.Ordinal);

        // A long key keeps a prefix and a digest of the whole key, the same every run, so the marker stays bounded and findable.
        var longKey = new Harness(Authorized());
        await longKey.Report(FailedRun, new GitHubOptions { Key = new string('k', 5_000) });
        Assert.StartsWith($"{Prefix} key={new string('k', 183)}#622b8b1d5094d382 -->\n", longKey.PostedBody, StringComparison.Ordinal);

        // The cut is on the encoded form and never inside a percent escape, so emoji cannot outgrow the marker.
        var emoji = new Harness(Authorized(("GITHUB_WORKFLOW", string.Concat(Enumerable.Repeat("💥", 300))), ("GITHUB_JOB", new string('ü', 300))));
        await emoji.Report(FailedRun, new GitHubOptions { Key = string.Concat(Enumerable.Repeat("💥", 300)) });
        var marker = Regex.Match(emoji.PostedBody, @"^<!-- e2e-github [^\n]* -->").Value;
        Assert.NotEmpty(marker);
        Assert.True(marker.Length <= 1_024);
        Assert.Matches(@" key=(?:%[0-9A-F]{2})+#[0-9a-f]{16} -->\z", marker);
    }

    [Fact]
    public async Task Keeps_two_long_keys_that_share_their_first_200_characters_on_their_own_comments()
    {
        List<string> markers = [];
        foreach (var key in new[] { $"android-{new string('x', 200)}-A", $"android-{new string('x', 200)}-B" })
        {
            var d = new Harness(Authorized());
            await d.Report(FailedRun, new GitHubOptions { Key = key });
            markers.Add(d.PostedBody.Split('\n', 2)[0]);
        }

        Assert.Matches(@"^<!-- e2e-github .* key=android-x+#[0-9a-f]{16} -->\z", markers[0]);
        Assert.NotEqual(markers[0], markers[1]);

        // B does not take over the comment A posted; it posts its own.
        var comments = new[] { new { id = 1, user = new { login = "github-actions[bot]", type = "Bot" }, body = $"{markers[0]}\n### e2e android A" } };
        var fake = new FakeGitHub(new Dictionary<string, Func<Call, HttpResponseMessage>> { ["GET *"] = _ => Json(200, comments), ["POST *"] = _ => Posted() });
        var other = new Harness(Authorized(), fake);
        await other.Report(FailedRun, new GitHubOptions { Key = $"android-{new string('x', 200)}-B" });
        Assert.Equal(["GET", "GET", "POST"], fake.Calls.Select(call => call.Method));
    }

    [Theory]
    [InlineData("a push", "push", "/event/push.json", "refs/heads/main", "push is not a pull request")]
    [InlineData("an issue_comment on a plain issue", "issue_comment", "/event/issue.json", null, "issue_comment is not a pull request")]
    public async Task On_a_non_pull_request_event_it_writes_the_summary_only_and_says_so(string name, string eventName, string eventPath, string? reference, string reason)
    {
        Assert.NotEmpty(name);
        var env = Authorized(("GITHUB_EVENT_NAME", eventName), ("GITHUB_EVENT_PATH", eventPath));
        if (reference is not null)
        {
            env["GITHUB_REF"] = reference;
        }

        var d = new Harness(env);

        AssertRows(await d.Report(FailedRun), $"not posted: {reason}; written to the job summary");
        Assert.Empty(d.Calls);
        Assert.Contains("### 🔴 e2e", d.Written["/summary.md"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_token_it_names_the_line_to_add_to_the_step_and_drops_the_summary_clause_when_there_is_no_summary_file()
    {
        var d = new Harness(Env());
        AssertRows(await d.Report(FailedRun), "not posted: set GITHUB_TOKEN in the step's env (GITHUB_TOKEN: ${{ github.token }}); written to the job summary");

        var noSummary = Env();
        noSummary.Remove("GITHUB_STEP_SUMMARY");
        var bare = new Harness(noSummary);
        AssertRows(await bare.Report(FailedRun), "not posted: set GITHUB_TOKEN in the step's env (GITHUB_TOKEN: ${{ github.token }})");
        Assert.Empty(bare.Written);
    }

    [Fact]
    public async Task A_summary_that_cannot_be_written_is_its_own_row_and_does_not_stop_the_comment()
    {
        var d = new Harness(Authorized(("GITHUB_STEP_SUMMARY", "/readonly.md")));

        AssertRows(await d.Report(FailedRun), "https://github.com/octo/app/pull/41#issuecomment-5", "job summary not written: EACCES: permission denied");
    }

    [Fact]
    public async Task Lets_a_GitHub_failure_surface_as_the_one_error_the_runner_prints_after_the_summary_is_written()
    {
        var forbidden = new FakeGitHub(new Dictionary<string, Func<Call, HttpResponseMessage>> { ["GET *"] = _ => Json(403, new { }) });
        var d = new Harness(Authorized(), forbidden);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => d.Report(FailedRun));
        Assert.Contains("the token cannot comment on octo/app#41", error.Message, StringComparison.Ordinal);
        Assert.Contains("### 🔴 e2e", d.Written["/summary.md"], StringComparison.Ordinal);
    }
}
