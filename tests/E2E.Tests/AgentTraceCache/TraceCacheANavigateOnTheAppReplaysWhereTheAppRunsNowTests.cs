// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;
using WebFixtureApp = E2E.Tests.WebPlatform.FixtureApp;

namespace E2E.Tests.AgentTraceCache;

/// <summary>
/// Upstream runs a project through the runner against two fixture apps and reads the step from its report. The port runs the same
/// test body in two sessions through the real web engine, one on each app.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class TraceCacheANavigateOnTheAppReplaysWhereTheAppRunsNowTests
{
    private const string AppIdentity = "fixture-app";

    [Fact]
    public async Task Partial_Records_an_absolute_URL_on_the_app_as_its_path_and_replays_it_on_another_origin_of_the_app()
    {
        var directory = TwoTapCounter.NewDirectory();
        var recording = 0;
        using var recordedOn = await WebFixtureApp.StartAsync();
        using var replayedOn = await WebFixtureApp.StartAsync();
        Assert.NotEqual(recordedOn.Url, replayedOn.Url);
        await RunAsync(directory, recordedOn.Url, () => recording++, strict: false);

        Assert.Equal(1, recording);
        var entry = TwoTapCounter.OnlyEntry(directory);
        var action = Assert.Single(entry.Actions);
        Assert.Equal(("navigate", "/about"), (action.Kind, action.Url));
        Assert.Equal(("/", "/about"), (entry.Route, entry.EndRoute));

        var replays = 0;
        var result = await RunAsync(directory, replayedOn.Url, () => replays++, strict: true);

        Assert.Equal(0, replays);
        Assert.Equal("self-finalized", result.Cache?.Mode);
        Assert.Equal(1, result.Cache?.ReplayedActions);
        Assert.Equal(1, result.Cache?.TotalActions);
    }

    /// <summary>Navigates to the About link's href from the node tree, an absolute URL on the app.</summary>
    private static async Task<ActResult> RunAsync(string directory, string appUrl, Action onRun, bool strict)
    {
        var executor = new TestExecutor("about-link-executor", async (context, token) =>
        {
            onRun();
            var observation = await context.ObserveAsync(new ExecutorObserveOptions { Tree = true }, token);
            var href = ExecutorTree.FindNode(observation.Tree, node => node.Role == "link" && node.Name == "About")?.Attributes?["href"];
            if (href is null)
            {
                return Failed("no About link");
            }

            await context.Actions.NavigateAsync(href, token);
            return Passed($"opened {href}");
        })
        {
            Version = "test",
        };
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            AgentExecutor = executor,
            BaseUrl = appUrl,
            AppIdentity = AppIdentity,
            Cache = new FileStepCache(directory),
            CacheEnabled = true,
            CacheMode = CacheMode.ReadWrite,
            CacheStrict = strict,
            TestTitle = "opens the about page",
            ActionTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(30),
            TestTimeout = TimeSpan.FromSeconds(60),
        });
        await session.App.OpenAsync();
        var result = await session.Agent.ActAsync("open the About page");
        await Expect.That(session.Screen.GetByRole("heading", "About")).ToBeVisibleAsync();
        session.Complete();
        return result;
    }
}
