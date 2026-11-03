// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentTraceCache;

/// <summary>
/// Upstream's <c>describe('trace cache: a bare-point tap replays like a coordinate-driven tool')</c>. Upstream runs a project through the
/// runner and reads the step from its report. The port runs the same test body in two sessions through the real web engine over one
/// cache directory.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class TraceCacheABarePointTapReplaysLikeACoordinateDrivenToolTests
{
    // Taps the red pin, drawn at CSS (300, 60) on a canvas the tree does not list. No model.
    private static TestExecutor PinExecutor(Action onRun) =>
        new("pin-executor", async (context, token) =>
        {
            onRun();
            await context.ObserveAsync(cancellationToken: token);
            var tapped = await context.Actions.TapAtAsync(new Point(300, 60), token);
            return Passed(tapped.Summary);
        })
        {
            Version = "test",
        };

    private static async Task<(ActResult Result, IReadOnlyList<StepRecord> Steps)> RunAsync(string directory, string appUrl, Action onRun)
    {
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            AgentExecutor = PinExecutor(onRun),
            BaseUrl = appUrl,
            AppIdentity = "fixture-app",
            Cache = new FileStepCache(directory),
            CacheEnabled = true,
            CacheMode = CacheMode.ReadWrite,
            TestTitle = "picks the red pin",
            ActionTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(30),
            TestTimeout = TimeSpan.FromSeconds(60),
        });
        await session.App.OpenAsync("/canvas");
        var result = await session.Agent.ActAsync("pick the red pin on the map");
        await Expect.That(session.Screen.GetByRole("status")).ToHaveTextAsync("red");
        session.Complete();
        return (result, session.StepRecords);
    }

    // Partial of upstream's "records the point with its viewport and replays it zero-turn on the same-sized viewport": upstream also expects the
    // replayed step to carry a `tapAt` engine event, and the port's replay performs its actions without step events, for every verb.
    [Fact]
    public async Task Partial_records_the_point_with_its_viewport_and_replays_it_zero_turn_on_the_same_sized_viewport()
    {
        var directory = TwoTapCounter.NewDirectory();
        using var pages = await PointTapPages.StartAsync();
        var runs = 0;
        await RunAsync(directory, pages.Url, () => runs++);

        var entry = TwoTapCounter.OnlyEntry(directory);
        var action = Assert.Single(entry.Actions);
        Assert.Equal("tapAt", action.Kind);
        Assert.Equal(new Point(300, 60), action.Point);
        Assert.Equal(new TraceViewport(1280, 720), action.Viewport);
        Assert.Contains(entry.Appeared, anchor => anchor is { Role: "status", Name: "Hit", Text: "red" });

        runs = 0;
        var (result, _) = await RunAsync(directory, pages.Url, () => runs++);

        Assert.Equal(0, runs);
        Assert.Equal("self-finalized", result.Cache?.Mode);
        Assert.Equal(1, result.Cache?.ReplayedActions);
        Assert.Equal(1, result.Cache?.TotalActions);
    }
}
