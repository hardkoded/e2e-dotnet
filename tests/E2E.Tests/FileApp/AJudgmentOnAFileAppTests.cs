// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.FileApp.FileAppSite;

namespace E2E.Tests.FileApp;

/// <summary>Upstream's <c>describe('a judgment on a file app')</c> (<c>file-app.test.ts</c>).</summary>
[Collection(BrowserCollection.Name)]
public sealed class AJudgmentOnAFileAppTests : IAsyncLifetime
{
    private readonly List<ModelRequest> _calls = [];
    private FixtureProject _project = null!;
    private RunOutcome _outcome = null!;

    public async Task InitializeAsync()
    {
        var judge = new ScriptedModel(request =>
        {
            _calls.Add(request);
            return ModelResponses.Judgment("holds", "the heading reads Home");
        });
        _project = CreateProject();
        _outcome = await RunProject.RunExistingAsync(
            _project,
            Options(_project, [new("judges from the tree", async session =>
                    {
                        await session.App.OpenAsync();
                        await session.Agent.AssertAsync("the page shows Home");
                    }), new("judges from the tree with vision on", async session =>
                    {
                        await session.App.OpenAsync();
                        await session.Agent.AssertAsync(
                            "the page shows Home",
                            new AssertOptions { Vision = VisionMode.On }
                        );
                    }), new("refuses a judgment from pixels alone", async session =>
                    {
                        await session.App.OpenAsync();
                        await session.Agent.AssertAsync(
                            "the page shows Home",
                            new AssertOptions { Vision = VisionMode.Only }
                        );
                    })], new Dictionary<string, AgentOptions> { ["default"] = new() { Model = judge } })
        );
    }

    public Task DisposeAsync()
    {
        _project.Cleanup();
        return Task.CompletedTask;
    }

    [Fact]
    public void Sends_the_judge_the_tree_and_no_screenshot_with_vision_on_too()
    {
        Assert.Equal(
            E2E.Reports.AttemptStatus.Passed,
            RunProject.ResultByTitle(_outcome, "judges from the tree").Attempts[0].Status
        );
        Assert.Equal(
            E2E.Reports.AttemptStatus.Passed,
            RunProject.ResultByTitle(_outcome, "judges from the tree with vision on").Attempts[0].Status
        );
        Assert.Equal([0, 0], _calls.Select(call => call.Messages.Sum(message => message.Images?.Count ?? 0)));
    }

    [Fact]
    public void Refuses_vision_only_before_any_model_call()
    {
        var attempt = RunProject.ResultByTitle(_outcome, "refuses a judgment from pixels alone").Attempts[0];
        Assert.Equal("POLICY_DENIED", attempt.Error!.Code);
        Assert.Contains("no screenshot of an app opened from a file", attempt.Error.Message, StringComparison.Ordinal);
        Assert.Equal(2, _calls.Count);
    }

    [Fact]
    public void Keeps_no_screenshot_of_the_judged_screen()
    {
        Assert.Empty(Screenshots(_outcome));
    }
}
