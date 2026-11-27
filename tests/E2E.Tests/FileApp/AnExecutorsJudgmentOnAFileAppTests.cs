// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Tests.AgentAct;
using static E2E.Tests.FileApp.FileAppSite;

namespace E2E.Tests.FileApp;

/// <summary>Upstream's <c>describe("an executor's judgment on a file app")</c> (<c>file-app.test.ts</c>).</summary>
[Collection(BrowserCollection.Name)]
public sealed class AnExecutorsJudgmentOnAFileAppTests : IAsyncLifetime
{
    // Judges from one observation, and fails with what the observation was refused for (upstream's `observing`).
    private static readonly TestExecutor Observing = new("observing", async (context, _) =>
    {
        try
        {
            await context.ObserveAsync();
            return new StepVerdict(StepVerdictStatus.Passed, "observed the screen");
        }
        catch (Exception error)
        {
            return new StepVerdict(StepVerdictStatus.Failed, error.Message);
        }
    });

    private FixtureProject _project = null!;
    private RunOutcome _outcome = null!;

    public async Task InitializeAsync()
    {
        _project = CreateProject();
        _outcome = await RunProject.RunExistingAsync(
            _project,
            Options(_project, [new("an executor judgment after the page left the directory", async session =>
                    {
                        await session.App.OpenAsync();
                        await session.Screen.GetByRole("link", "Notes").TapAsync();
                        await session.Browser.WaitForURLAsync(new Regex(@"secret\.html$"));
                        await session.Agent.AssertAsync("the notes are shown");
                    }), new("an executor judgment on the app file", async session =>
                    {
                        await session.App.OpenAsync();
                        await session.Agent.AssertAsync("the page shows Home");
                    })], new Dictionary<string, AgentOptions> { ["default"] = new() { Executor = Observing } })
        );
    }

    public Task DisposeAsync()
    {
        _project.Cleanup();
        return Task.CompletedTask;
    }

    [Fact]
    public void Keeps_no_screenshot_of_the_page_outside_the_directory()
    {
        var attempt = RunProject
            .ResultByTitle(_outcome, "an executor judgment after the page left the directory")
            .Attempts[0];
        Assert.Contains(
            "a file app stays in the directory of the app's file",
            attempt.Error!.Message,
            StringComparison.Ordinal
        );
        Assert.Matches(new Regex(@"secret\.html$"), attempt.Failure!.Url);
        Assert.Null(attempt.Failure.Screen);
        Assert.Null(attempt.Failure.Screenshot);
        Assert.Empty(Screenshots(attempt));
    }

    [Fact]
    public void Keeps_none_of_the_app_file_either()
    {
        var result = RunProject.ResultByTitle(_outcome, "an executor judgment on the app file");
        Assert.Equal(E2E.Reports.AttemptStatus.Passed, result.Attempts[0].Status);
        Assert.Empty(Screenshots(result.Attempts[0]));
    }
}
