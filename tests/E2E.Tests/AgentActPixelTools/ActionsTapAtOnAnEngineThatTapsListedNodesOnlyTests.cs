// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentActPixelTools;

/// <summary>
/// Upstream's <c>describe('actions.tapAt on an engine that taps listed nodes only')</c>: the popover layout on an engine with node taps
/// and no pointer. A point outside the popover still taps the dismiss control by id, since a bare point is not an option, even though
/// its center is covered.
/// </summary>
public sealed class ActionsTapAtOnAnEngineThatTapsListedNodesOnlyTests
{
    private static BoundingBox Rect(double x, double y, double width, double height) => new(x, y, width, height);

    [Fact]
    public async Task Taps_the_covered_control_by_id_and_says_so()
    {
        var engine = new ListedNodesOnlyEngine(
            "tap",
            [
                new SemanticNode
                {
                    Ref = "root",
                    Rect = Rect(0, 0, 1280, 720),
                    Children =
                    [
                        new SemanticNode { Ref = "dismiss", Role = "button", Name = "Dismiss popover", Rect = Rect(0, 0, 1280, 720) },
                        new SemanticNode
                        {
                            Ref = "dialog",
                            Role = "dialog",
                            Name = "Calendar",
                            Rect = Rect(540, 260, 200, 200),
                            Children = [new SemanticNode { Ref = "day", Role = "button", Name = "15", Rect = Rect(540, 260, 200, 200) }],
                        },
                    ],
                },
            ]);
        string? summary = null;
        var executor = new TestExecutor(
            "outside-tapper",
            async (context, token) =>
            {
                await context.ObserveAsync(cancellationToken: token);
                summary = (await context.Actions.TapAtAsync(new Point(100, 100), token)).Summary;
                return Passed(summary);
            });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions { Engine = engine, AgentExecutor = executor, BaseUrl = "https://fixture.test", CacheEnabled = false, TestTitle = "taps outside the popover" });
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync(
            "dismiss the calendar popover",
            new ActOptions
            {
                OnAction = e =>
                {
                    Assert.IsNotType<TapAtActionEvent>(e);
                    return Task.CompletedTask;
                },
            });
        session.Complete();

        Assert.Equal(["dismiss"], engine.Performed);
        Assert.Matches(new Regex(@"^Tapped \S* ?button ""Dismiss popover""\S*, the control at \(100, 100\)\.$", RegexOptions.None, TimeSpan.FromSeconds(2)), summary);
    }
}
