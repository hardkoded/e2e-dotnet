// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentTraceCache;

/// <summary>
/// Upstream's <c>describe('trace cache: the recorded end state gates self-finalization')</c>, run in two sessions over one cache
/// directory through the real web engine. Upstream's <c>startPath</c>, <c>endPath</c> and <c>endAnchors</c> are the entry's
/// <c>Route</c>, <c>EndRoute</c> and <c>Appeared</c>.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class TraceCacheTheRecordedEndStateGatesSelfFinalizationTests
{
    // Taps "Save marker" once (unless a replayed prefix already did) and checks the marker. No model.
    private static TestExecutor SaveMarkerExecutor(Action onRun) =>
        new("save-marker-executor", async (context, token) =>
        {
            onRun();
            var observation = await context.ObserveAsync(cancellationToken: token);
            if ((context.ReplayedPrefix?.ReplayedActions.Count ?? 0) == 0)
            {
                await context.Actions.TapAsync(TargetFor(observation.Text, "button \"Save marker\""), token);
                observation = await context.ObserveAsync(cancellationToken: token);
            }

            return Regex.IsMatch(observation.Text, "\"Marker\"[^\n]*text=\"saved\"")
                ? Passed("the marker reads saved")
                : Failed("the marker was not saved");
        })
        {
            Version = "test",
        };

    private static async Task<ActResult> RunAsync(string directory, string appUrl, Action onRun)
    {
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            AgentExecutor = SaveMarkerExecutor(onRun),
            BaseUrl = appUrl,
            AppIdentity = "fixture-app",
            Cache = new FileStepCache(directory),
            CacheEnabled = true,
            CacheMode = CacheMode.ReadWrite,
            TestTitle = "saves the marker",
            ActionTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(30),
            TestTimeout = TimeSpan.FromSeconds(60),
        });
        await session.App.OpenAsync("/storage");
        var result = await session.Agent.ActAsync("save the marker");
        await Expect.That(session.Screen.GetByLabel("Marker")).ToHaveTextAsync("saved");
        session.Complete();
        return result;
    }

    [Fact]
    public async Task Records_the_effect_of_a_same_path_mutation_as_end_anchors_and_replays_on_them()
    {
        var directory = TwoTapCounter.NewDirectory();
        using var pages = await TraceCacheStatePages.StartAsync();
        var runs = 0;
        await RunAsync(directory, pages.Url, () => runs++);

        Assert.Equal(1, runs);
        var entry = TwoTapCounter.OnlyEntry(directory);
        Assert.Equal("/storage", entry.Route);
        Assert.Equal("/storage", entry.EndRoute);
        Assert.Contains(entry.Appeared, anchor => anchor is { Role: "status", Name: "Marker", Text: "saved" });

        runs = 0;
        var second = await RunAsync(directory, pages.Url, () => runs++);

        Assert.Equal(0, runs);
        Assert.Equal("self-finalized", second.Cache?.Mode);
        Assert.Equal(1, second.Cache?.ReplayedActions);
        Assert.Equal(1, second.Cache?.TotalActions);
    }
}
