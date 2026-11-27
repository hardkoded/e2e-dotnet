// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using static E2E.Tests.FileApp.FileAppSite;

namespace E2E.Tests.FileApp;

/// <summary>
/// Upstream's <c>describe('a file app')</c> (<c>file-app.test.ts</c>): a target whose app is a file on disk, in a real browser through the
/// runner: the file opens with no server, navigation reaches the files beside it, and a file outside its directory or a directory is refused.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class AFileAppTests : IAsyncLifetime
{
    private static readonly string[] Refused =
    [
        "../secret.html",
        "/etc/hosts",
        "file:///etc/hosts",
        "..%2fsecret.html",
    ];

    private FixtureProject _project = null!;
    private RunOutcome _outcome = null!;

    public async Task InitializeAsync()
    {
        _project = CreateProject();
        List<InMemoryTest> tests =
        [
            new("opens the file and a page beside it", async session =>
            {
                await session.App.OpenAsync();
                await Expect.That(session.Screen.GetByRole("heading", "Home")).ToBeVisibleAsync();
                await session.App.OpenAsync("pages/about.html");
                await Expect.That(session.Screen.GetByRole("heading", "About")).ToBeVisibleAsync();
                await session.Screen.GetByRole("link", "Home").TapAsync();
                await Expect.That(session.Screen.GetByRole("heading", "Home")).ToBeVisibleAsync();
                Assert.Matches(new Regex(@"^file:.*/site/index\.html$"), await session.Browser.UrlAsync());
            }),
            .. Refused
                .Concat(["pages"])
                .Select(target => new InMemoryTest(
                    "app.open refuses " + System.Text.Json.JsonSerializer.Serialize(target),
                    async session =>
                    {
                        await session.App.OpenAsync();
                        await session.App.OpenAsync(target);
                    }
                )),
            new("browser.goto refuses a file outside the directory", async session =>
            {
                await session.App.OpenAsync();
                await session.Browser.GotoAsync("../secret.html");
            }),
            new("a failure after the page left the directory", async session =>
            {
                await session.App.OpenAsync();
                await session.Screen.GetByRole("link", "Notes").TapAsync();
                await session.Browser.WaitForURLAsync(new Regex(@"secret\.html$"));
                throw new InvalidOperationException("failed on the page outside");
            }),
        ];
        _outcome = await RunProject.RunExistingAsync(_project, Options(_project, tests));
    }

    public Task DisposeAsync()
    {
        _project.Cleanup();
        return Task.CompletedTask;
    }

    [Fact]
    public void Opens_the_file_with_no_server_and_the_pages_in_its_directory()
    {
        var result = RunProject.ResultByTitle(_outcome, "opens the file and a page beside it");
        Assert.True(result.Attempts[^1].Status == E2E.Reports.AttemptStatus.Passed, result.Attempts[^1].Error?.Message);
    }

    [Fact]
    public void Keeps_the_screen_text_of_a_failure_in_the_directory_and_no_screenshot()
    {
        var attempt = RunProject.ResultByTitle(_outcome, "browser.goto refuses a file outside the directory").Attempts[
            0
        ];
        Assert.Matches(new Regex(@"index\.html$"), attempt.Failure!.Url);
        Assert.NotNull(attempt.Failure.Screen);
        Assert.Null(attempt.Failure.Screenshot);
        Assert.Empty(Screenshots(_outcome));
    }

    [Fact]
    public void Reports_no_origin_for_the_target()
    {
        var target = Assert.Single(_outcome.Report.Run.Targets);
        Assert.Null(target.BaseOrigin);
    }

    [Fact]
    public void Refuses_a_file_outside_the_directory_however_it_is_spelled_and_a_directory()
    {
        foreach (var target in Refused)
        {
            var error = RunProject
                .ResultByTitle(_outcome, "app.open refuses " + System.Text.Json.JsonSerializer.Serialize(target))
                .Attempts[0]
                .Error!;
            Assert.Equal("POLICY_DENIED", error.Code);
            Assert.Contains("stays in the directory of the app's file", error.Message, StringComparison.Ordinal);
        }

        var directory = RunProject.ResultByTitle(_outcome, "app.open refuses \"pages\"").Attempts[0].Error!;
        Assert.Equal("POLICY_DENIED", directory.Code);
        Assert.Contains("opens a file, not a directory", directory.Message, StringComparison.Ordinal);
        Assert.Equal(
            "POLICY_DENIED",
            RunProject
                .ResultByTitle(_outcome, "browser.goto refuses a file outside the directory")
                .Attempts[0]
                .Error!.Code
        );
    }
}
