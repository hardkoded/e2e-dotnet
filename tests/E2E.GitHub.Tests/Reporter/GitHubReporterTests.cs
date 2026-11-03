// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;
using static E2E.GitHub.Tests.FakeGitHub;
using static E2E.GitHub.Tests.Fixtures;

namespace E2E.GitHub.Tests.Reporter;

/// <summary>.NET-only: how a <see cref="GitHubReporter"/> gets its options, since <see cref="E2EReporterAttribute"/> builds it with no arguments. Upstream's <c>github(options)</c> takes them in the config module.</summary>
public sealed class GitHubReporterTests
{
    private sealed class MatrixReporter() : GitHubReporter(new GitHubOptions { Key = "from code" });

    private static async Task<string> PostedMarker(GitHubReporter reporter, Dictionary<string, string> env)
    {
        var fake = new FakeGitHub(new Dictionary<string, Func<Call, HttpResponseMessage>>
        {
            ["GET *"] = _ => Json(200, Array.Empty<object>()),
            ["POST *"] = _ => Json(201, new { id = 1, html_url = "https://github.com/octo/app/pull/41#issuecomment-1" }),
        });
        var deps = new ReportDeps(FakeGitHub.Env(env), ReadEvent, fake.Client, (_, _, _) => Task.CompletedTask, _ => false);
        await reporter.ReportRunAsync(Finished(ReportOf()), deps, CancellationToken.None);
        return fake.Calls.Single(call => call.Method == "POST").BodyText.Split('\n', 2)[0];
    }

    [Fact]
    public void Is_named_github_and_has_a_public_constructor_with_no_parameters()
    {
        Assert.Equal("github", ((E2E.Reports.Reporter)Activator.CreateInstance(typeof(GitHubReporter))!).Name);
    }

    [Fact]
    public async Task Reads_the_key_from_E2E_GITHUB_KEY_when_it_has_no_options()
    {
        var env = ActionsEnv();
        env["GITHUB_TOKEN"] = "ghs";
        env["E2E_GITHUB_KEY"] = "chromium";

        Assert.Equal("<!-- e2e-github project=dev.example.shop workflow=e2e job=test key=chromium -->", await PostedMarker(new GitHubReporter(), env));
    }

    [Fact]
    public async Task Takes_the_key_from_the_options_a_derived_reporter_passes_over_the_variable()
    {
        var env = ActionsEnv();
        env["GITHUB_TOKEN"] = "ghs";
        env["E2E_GITHUB_KEY"] = "chromium";

        Assert.Equal("<!-- e2e-github project=dev.example.shop workflow=e2e job=test key=from%20code -->", await PostedMarker(new MatrixReporter(), env));
    }
}
