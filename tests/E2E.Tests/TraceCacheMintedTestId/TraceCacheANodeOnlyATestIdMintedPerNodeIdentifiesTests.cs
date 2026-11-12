// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.TraceCacheMintedTestId;

/// <summary>
/// Upstream's <c>trace-cache-minted-test-id.test.ts</c>, <c>describe('trace cache: a node only a test id minted per node identifies')</c>:
/// the trace cache on a canvas whose nodes have only a random test id. The first run records the step without that id as an end anchor and
/// the click target by its shape, and the second replays both with no executor call. Upstream runs the suite through its runner; the port
/// runs each test in a session over one cache directory through the real web engine, as
/// <c>TraceCacheTheRecordedEndStateGatesSelfFinalizationTests</c> does. No model is involved: like upstream, a scripted executor taps.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class TraceCacheANodeOnlyATestIdMintedPerNodeIdentifiesTests
{
    // Taps "Add node" or the node box, by the instruction, unless a replayed prefix already did.
    private static TestExecutor GraphExecutor(Action onRun) =>
        new("graph-executor", async (context, token) =>
        {
            onRun();
            var observation = await context.ObserveAsync(cancellationToken: token);
            if ((context.ReplayedPrefix?.ReplayedActions.Count ?? 0) == 0)
            {
                var target = context.Step.Instruction == "add a node" ? "button \"Add node\"" : "box testid=\"rf__node-";
                await context.Actions.TapAsync(TargetFor(observation.Text, target), token);
            }

            return Passed("tapped");
        })
        {
            Version = "test",
        };

    private static async Task<ActResult> RunAsync(string directory, string appUrl, string title, string path, string instruction, string status, Action onRun)
    {
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            AgentExecutor = GraphExecutor(onRun),
            BaseUrl = appUrl,
            AppIdentity = "fixture-app",
            Cache = new FileStepCache(directory),
            CacheEnabled = true,
            CacheMode = CacheMode.ReadWrite,
            TestTitle = title,
            ActionTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(30),
            TestTimeout = TimeSpan.FromSeconds(60),
        });
        await session.App.OpenAsync(path);
        var result = await session.Agent.ActAsync(instruction);
        await Expect.That(session.Screen.GetByRole("status")).ToHaveTextAsync(status);
        session.Complete();
        return result;
    }

    private static async Task<(ActResult Added, ActResult Selected)> RunSuiteAsync(string directory, string appUrl, Action onRun)
    {
        var added = await RunAsync(directory, appUrl, "adds a node", "/graph", "add a node", "Node added", onRun);
        var selected = await RunAsync(directory, appUrl, "selects the node", "/graph?seeded", "select the node", "Node selected", onRun);
        return (added, selected);
    }

    [Fact]
    public async Task Records_the_effect_without_the_minted_id_and_replays_the_next_run_the_click_by_the_ids_shape()
    {
        var directory = ConfigTests.TempCache();
        using var site = await GraphPage.StartAsync();
        var runs = 0;
        await RunSuiteAsync(directory, site.Url, () => runs++);

        Assert.Equal(2, runs);
        var cache = new FileStepCache(directory);
        var entries = Directory.GetFiles(directory).Select(file => cache.Read(Path.GetFileNameWithoutExtension(file)).Entry!).ToList();
        var added = entries.Single(entry => entry.Actions.FirstOrDefault() is { Kind: "tap", Name: "Add node" });
        Assert.Equal(["status|Node added"], added.Appeared.Select(anchor => anchor.Role + "|" + anchor.Name));
        var selected = entries.Single(entry => entry.Actions.FirstOrDefault() is { Kind: "tap", TestId: { } id } && id.StartsWith("rf__node-", StringComparison.Ordinal));
        var target = selected.Actions[0];
        Assert.Equal("box", target.Role);
        Assert.Equal(0, target.Position?.Index);
        Assert.Equal(1, target.Position?.Of);

        runs = 0;
        var (secondAdded, secondSelected) = await RunSuiteAsync(directory, site.Url, () => runs++);

        Assert.Equal(0, runs);
        foreach (var step in new[] { secondAdded, secondSelected })
        {
            Assert.Equal("self-finalized", step.Cache?.Mode);
            Assert.Equal(1, step.Cache?.ReplayedActions);
            Assert.Equal(1, step.Cache?.TotalActions);
        }
    }
}
