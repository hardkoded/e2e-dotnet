// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using GitHubActions = E2E.GitHub.Actions;

namespace E2E.GitHub.Tests.Actions;

/// <summary>Upstream's <c>describe('detectActions')</c> (<c>tests/unit/actions.test.ts</c>): the Actions context, what is read from where, and what counts as not being on Actions.</summary>
public sealed class DetectActionsTests
{
    private static Task<ActionsContext?> Detect(Dictionary<string, string> env) =>
        GitHubActions.DetectActionsAsync(new ActionsDeps(FakeGitHub.Env(env), FakeGitHub.ReadEvent));

    private static Dictionary<string, string> With(params (string Name, string Value)[] extra)
    {
        var env = FakeGitHub.ActionsEnv();
        foreach (var (name, value) in extra)
        {
            env[name] = value;
        }

        return env;
    }

    [Fact]
    public async Task Is_nothing_outside_Actions_and_a_broken_runner_on_Actions_without_its_always_set_variables()
    {
        Assert.Null(await Detect([]));
        Assert.Null(await Detect(new() { ["CI"] = "1", ["GITLAB_CI"] = "true" }));
        Assert.Equal(
            "GITHUB_ACTIONS is set but GITHUB_REPOSITORY is not",
            (await Assert.ThrowsAsync<InvalidOperationException>(() => Detect(new() { ["GITHUB_ACTIONS"] = "true" }))).Message);
        Assert.Equal(
            "GITHUB_ACTIONS is set but GITHUB_JOB is not",
            (await Assert.ThrowsAsync<InvalidOperationException>(() => Detect(With(("GITHUB_JOB", " "))))).Message);
    }

    [Fact]
    public async Task Reads_the_pull_request_and_its_head_from_the_event_payload()
    {
        Assert.Equal(
            new ActionsContext(
                "octo/app",
                "https://github.com",
                "https://api.github.com",
                "https://github.com/octo/app/actions/runs/99",
                "pull_request",
                "e2e",
                "test",
                "head-sha",
                41,
                "t",
                "/summary.md",
                null),
            await Detect(With(("GITHUB_TOKEN", "t"))));
    }

    [Fact]
    public async Task Takes_an_issue_comment_on_a_pull_request_without_a_commit_to_link_and_not_one_on_an_issue()
    {
        var comment = await Detect(With(("GITHUB_EVENT_NAME", "issue_comment"), ("GITHUB_EVENT_PATH", "/event/comment.json")));
        Assert.Equal(7, comment?.PullRequest);

        // GITHUB_SHA is the default branch on this event, not the pull request's head.
        Assert.Null(comment?.Sha);
        var issue = await Detect(With(("GITHUB_EVENT_NAME", "issue_comment"), ("GITHUB_EVENT_PATH", "/event/issue.json")));
        Assert.Null(issue?.PullRequest);
        Assert.Equal("merge-sha", issue?.Sha);
    }

    [Fact]
    public async Task Falls_back_to_the_ref_when_the_payload_is_missing_broken_or_not_about_a_pull_request()
    {
        (string, string)[] Ref(string path, string reference = "refs/pull/12/merge") => [("GITHUB_REF", reference), ("GITHUB_EVENT_PATH", path)];
        Assert.Equal(12, (await Detect(With(Ref("/event/push.json"))))?.PullRequest);
        Assert.Equal(12, (await Detect(With(Ref("/event/broken.json"))))?.PullRequest);
        Assert.Equal(12, (await Detect(With(Ref("/event/missing.json"))))?.PullRequest);
        Assert.Null((await Detect(With(Ref("/event/push.json", "refs/pull/0/merge"))))?.PullRequest);
        var push = await Detect(With(("GITHUB_EVENT_NAME", "push"), ("GITHUB_REF", "refs/heads/main"), ("GITHUB_EVENT_PATH", "/event/push.json")));
        Assert.Null(push?.PullRequest);
        Assert.Equal("merge-sha", push?.Sha);
    }

    [Fact]
    public async Task Reads_GH_TOKEN_when_GITHUB_TOKEN_is_unset_and_honors_Enterprise_Server_URLs()
    {
        var context = await Detect(With(
            ("GH_TOKEN", "gh"),
            ("GITHUB_SERVER_URL", "https://git.corp.example/"),
            ("GITHUB_API_URL", "https://git.corp.example/api/v3/")));
        Assert.Equal("gh", context?.Token);
        Assert.Equal("https://git.corp.example", context?.ServerUrl);
        Assert.Equal("https://git.corp.example/api/v3", context?.ApiUrl);
        Assert.Equal("https://git.corp.example/octo/app/actions/runs/99", context?.RunUrl);
    }
}
