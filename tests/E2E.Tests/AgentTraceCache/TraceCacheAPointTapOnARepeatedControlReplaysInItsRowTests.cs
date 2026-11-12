// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentTraceCache;

/// <summary>
/// Upstream's <c>describe('trace cache: a point tap on a repeated control replays in its row')</c>, run in two sessions over one cache
/// directory through the real web engine.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class TraceCacheAPointTapOnARepeatedControlReplaysInItsRowTests
{
    // Taps the center of the "Reserve now" button in the Offer A row, found in the tree, as a bare point. No model.
    private static TestExecutor OfferPointExecutor(Action onRun, List<PointTapResult> results) =>
        new("offer-point-executor", async (context, token) =>
        {
            onRun();
            var observation = await context.ObserveAsync(new ExecutorObserveOptions { Tree = true }, token);
            var nodes = observation.Tree!.SelectMany(Flatten).ToList();
            var row = nodes.FindIndex(node => node.Text == "Offer A" || node.Name == "Offer A");
            var rect = nodes.Skip(row + 1).First(node => node.Role == "button" && node.Name == "Reserve now").Rect!;
            var tapped = await context.Actions.TapAtAsync(new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2), token);
            results.Add(tapped);
            return Passed(tapped.Summary);
        })
        {
            Version = "test",
        };

    private static IEnumerable<ExecutorNode> Flatten(ExecutorNode node) => [node, .. (node.Children ?? []).SelectMany(Flatten)];

    private static async Task<(ActResult Result, IReadOnlyList<StepRecord> Steps)> RunAsync(string directory, string appUrl, Action onRun, List<PointTapResult> results)
    {
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            AgentExecutor = OfferPointExecutor(onRun, results),
            BaseUrl = appUrl,
            AppIdentity = "fixture-app",
            Cache = new FileStepCache(directory),
            CacheEnabled = true,
            CacheMode = CacheMode.ReadWrite,
            TestTitle = "reserves offer A at a point",
            ActionTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(30),
            TestTimeout = TimeSpan.FromSeconds(60),
        });
        await session.App.OpenAsync("/repeats?still=1");
        var result = await session.Agent.ActAsync("reserve offer A");
        await Expect.That(session.Screen.GetByRole("status", "Picked")).ToHaveTextAsync("A");
        session.Complete();
        return (result, session.StepRecords);
    }

    [Fact]
    public async Task Names_the_control_it_tapped_records_its_row_and_replays_into_that_row_after_the_rows_reverse()
    {
        var directory = TwoTapCounter.NewDirectory();
        using var pages = await PointTapPages.StartAsync();
        var results = new List<PointTapResult>();
        var runs = 0;
        await RunAsync(directory, pages.Url, () => runs++, results);

        Assert.NotNull(results[0].Target);
        var entry = TwoTapCounter.OnlyEntry(directory);
        var action = Assert.Single(entry.Actions);
        Assert.Equal("tapAt", action.Kind);
        var place = action.Under?.Target;
        Assert.Equal(("button", "Reserve now", "Offer A"), (place?.Role, place?.Name, place?.Within));

        // The b variant lists the offers C, B, A: the recorded point now sits in Offer C's row.
        pages.Variant = "b";
        runs = 0;
        var (result, _) = await RunAsync(directory, pages.Url, () => runs++, results);
        pages.Variant = null;

        Assert.Equal(0, runs);
        Assert.Equal("self-finalized", result.Cache?.Mode);
        Assert.Equal(1, result.Cache?.ReplayedActions);
        Assert.Equal(1, result.Cache?.TotalActions);
    }
}
