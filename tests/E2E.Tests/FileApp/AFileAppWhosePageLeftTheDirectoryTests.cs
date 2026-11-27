// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using static E2E.Tests.FileApp.FileAppSite;

namespace E2E.Tests.FileApp;

/// <summary>Upstream's <c>describe('a file app whose page left the directory')</c> (<c>file-app.test.ts</c>).</summary>
[Collection(BrowserCollection.Name)]
public sealed class AFileAppWhosePageLeftTheDirectoryTests : IAsyncLifetime
{
    private FixtureProject _project = null!;
    private RunOutcome _outcome = null!;

    public async Task InitializeAsync()
    {
        _project = CreateProject();
        _outcome = await RunProject.RunExistingAsync(
            _project,
            Options(_project, [new("a failure after the page left the directory", async session =>
                    {
                        await session.App.OpenAsync();
                        await session.Screen.GetByRole("link", "Notes").TapAsync();
                        await session.Browser.WaitForURLAsync(new Regex(@"secret\.html$"));
                        throw new InvalidOperationException("failed on the page outside");
                    })])
        );
    }

    public Task DisposeAsync()
    {
        _project.Cleanup();
        return Task.CompletedTask;
    }

    [Fact]
    public void Keeps_where_the_failure_was_and_nothing_of_the_screen_no_tree_no_screenshot_no_trace_screen()
    {
        var attempt = RunProject.ResultByTitle(_outcome, "a failure after the page left the directory").Attempts[0];
        Assert.Contains("failed on the page outside", attempt.Error!.Message, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"secret\.html$"), attempt.Failure!.Url);
        Assert.Null(attempt.Failure.Screen);
        Assert.Null(attempt.Failure.Screenshot);
        Assert.Null(attempt.Failure.Nodes);
        Assert.Empty(Screenshots(attempt));
        var kept = RunProject
            .ContentsUnder(Path.Combine(_project.Dir, ".e2e"))
            .Where(file => file.Content.Contains("TOP-SECRET-NOTES", StringComparison.Ordinal))
            .Select(file => file.File);
        Assert.Empty(kept);
    }
}
