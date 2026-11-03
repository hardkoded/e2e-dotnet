// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Reports;
using static E2E.GitHub.Tests.FakeGitHub;
using static E2E.GitHub.Tests.Fixtures;

namespace E2E.GitHub.Tests.Reporter;

/// <summary>.NET-only: how a <see cref="GitHubReporter"/> gets its options, since the config builds it with no arguments. Upstream's <c>github(options)</c> takes them in the config module.</summary>
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

    private static Dictionary<string, string> Authorized()
    {
        var env = ActionsEnv();
        env["GITHUB_TOKEN"] = "ghs";
        env["E2E_GITHUB_KEY"] = "chromium";
        return env;
    }

    private static JsonElement Parse(string text) => JsonDocument.Parse(text).RootElement.Clone();

    [Fact]
    public void Is_named_github_and_has_a_public_constructor_with_no_parameters()
    {
        Assert.Equal("github", ((E2E.Reports.Reporter)Activator.CreateInstance(typeof(GitHubReporter))!).Name);
        Assert.IsAssignableFrom<IConfigurableReporter>(new GitHubReporter());
    }

    [Fact]
    public async Task Reads_the_key_from_E2E_GITHUB_KEY_when_it_has_no_options()
    {
        Assert.Equal("<!-- e2e-github project=dev.example.shop workflow=e2e job=test key=chromium -->", await PostedMarker(new GitHubReporter(), Authorized()));
    }

    [Fact]
    public async Task Takes_the_key_from_the_options_a_derived_reporter_passes_over_the_variable()
    {
        Assert.Equal("<!-- e2e-github project=dev.example.shop workflow=e2e job=test key=from%20code -->", await PostedMarker(new MatrixReporter(), Authorized()));
    }

    [Fact]
    public async Task Configure_takes_the_key_of_the_config_entry_over_the_variable()
    {
        var reporter = new GitHubReporter();
        reporter.Configure(Parse("""{ "key": "firefox" }"""));

        Assert.Equal("<!-- e2e-github project=dev.example.shop workflow=e2e job=test key=firefox -->", await PostedMarker(reporter, Authorized()));
    }

    [Fact]
    public async Task Configure_with_no_key_leaves_the_variable_in_charge()
    {
        var reporter = new GitHubReporter();
        reporter.Configure(Parse("{}"));

        Assert.Equal("<!-- e2e-github project=dev.example.shop workflow=e2e job=test key=chromium -->", await PostedMarker(reporter, Authorized()));
    }

    [Fact]
    public void Configure_rejects_an_unknown_option_naming_it_and_not_its_value()
    {
        var error = Assert.Throws<ArgumentException>(() => new GitHubReporter().Configure(Parse("""{ "token": "ghs_secret" }""")));

        Assert.Contains("\"token\"", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ghs_secret", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "key": 7 }""")]
    [InlineData("""{ "key": null }""")]
    [InlineData("""{ "key": ["ghs_secret"] }""")]
    public void Configure_rejects_a_key_that_is_not_a_string_without_echoing_it(string options)
    {
        var error = Assert.Throws<ArgumentException>(() => new GitHubReporter().Configure(Parse(options)));

        Assert.Contains("\"key\" must be a string", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ghs_secret", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Configure_rejects_options_that_are_not_an_object()
    {
        Assert.Throws<ArgumentException>(() => new GitHubReporter().Configure(Parse("\"chromium\"")));
    }
}
