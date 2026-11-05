// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentActPixelTools;

/// <summary>Upstream's <c>describe('actions.tapAt from a custom executor')</c>, on the web engine over the canvas page.</summary>
[Collection(BrowserCollection.Name)]
public sealed class ActionsTapAtFromACustomExecutorTests
{
    /// <summary>
    /// An executor that fires a point tap and a node tap without awaiting the first: both queue in call order, so the point is
    /// hit-tested against the screen it was named on and lands before the node tap.
    /// </summary>
    [Fact]
    public async Task Runs_the_hit_test_and_the_tap_in_call_order_and_reports_a_bare_point_as_such()
    {
        using var site = await CanvasPages.StartAsync();
        PointTapResult? point = null;
        var racer = new TestExecutor(
            "racer",
            async (context, token) =>
            {
                var observation = await context.ObserveAsync(cancellationToken: token);
                var reset = Regex.Match(observation.Text, @"#(\S+) button ""Reset""", RegexOptions.None, TimeSpan.FromSeconds(2)).Groups[1].Value;
                if (reset.Length == 0)
                {
                    return new StepVerdict(StepVerdictStatus.Failed, "no reset button on screen");
                }

                var tapAt = context.Actions.TapAtAsync(new Point(300, 60), token);
                var tap = context.Actions.TapAsync(new ExecutorTarget(reset), null, token);
                await Task.WhenAll(tapAt, tap);
                point = await tapAt;
                return point.Target is null ? Passed(point.Summary) : new StepVerdict(StepVerdictStatus.Failed, point.Summary);
            });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            AgentExecutor = racer,
            BaseUrl = site.Url,
            CacheEnabled = false,
            TestTitle = "a point tap issued alongside a node tap lands first",
            ActionTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(30),
            TestTimeout = TimeSpan.FromSeconds(60),
        });
        await session.App.OpenAsync("/canvas");
        await session.Agent.ActAsync("pick the red pin, then reset");
        await Expect.That(session.Screen.GetByRole("status")).ToHaveTextAsync("reset");
        session.Complete();

        var step = session.StepRecords.Single(candidate => candidate.Api == "agent.act");
        Assert.Equal(["tapAt", "tap"], step.Events.Where(item => item.Kind == StepEventKind.Engine).Select(item => item.Name));
        Assert.Equal(0, step.Metrics?.ModelCalls);
        Assert.Null(step.Model);
    }
}
