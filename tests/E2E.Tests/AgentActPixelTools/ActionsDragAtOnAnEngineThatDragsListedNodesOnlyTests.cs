// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentActPixelTools;

/// <summary>
/// Upstream's <c>describe('actions.dragAt on an engine that drags listed nodes only')</c>: two points on two listed controls are joined by
/// id, and a drag inside one control is refused, since the engine takes no bare point.
/// </summary>
public sealed class ActionsDragAtOnAnEngineThatDragsListedNodesOnlyTests
{
    [Fact]
    public async Task Joins_the_two_controls_by_id_and_says_so_and_refuses_a_drag_inside_one_control()
    {
        var engine = new ListedNodesOnlyEngine(
            "dragTo",
            [
                new SemanticNode
                {
                    Ref = "root",
                    Rect = new BoundingBox(0, 0, 1280, 720),
                    Children =
                    [
                        new SemanticNode { Ref = "card", Role = "button", Name = "Card", Rect = new BoundingBox(100, 100, 200, 100) },
                        new SemanticNode { Ref = "done", Role = "button", Name = "Done", Rect = new BoundingBox(600, 100, 200, 100) },
                    ],
                },
            ]);
        string? refused = null;
        string? summary = null;
        var executor = new TestExecutor(
            "card-dragger",
            async (context, token) =>
            {
                await context.ObserveAsync(cancellationToken: token);
                summary = (await context.Actions.DragAtAsync(new Point(150, 150), new Point(700, 150), token)).Summary;
                try
                {
                    await context.Actions.DragAtAsync(new Point(120, 120), new Point(280, 180), token);
                }
                catch (E2EException ex)
                {
                    refused = ex.Code;
                }

                return Passed("done");
            });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions { Engine = engine, AgentExecutor = executor, BaseUrl = "https://fixture.test", CacheEnabled = false, TestTitle = "moves the card to done" });
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync(
            "move the card to done",
            new ActOptions
            {
                OnAction = e =>
                {
                    Assert.IsNotType<DragAtActionEvent>(e);
                    return Task.CompletedTask;
                },
            });
        session.Complete();

        Assert.Equal(["card onto done"], engine.Performed);
        Assert.Matches(new Regex(@"^Dragged \S* ?button ""Card""\S*, the control at \(150, 150\), onto \S* ?button ""Done""\S*, the control at \(700, 150\)\.$", RegexOptions.None, TimeSpan.FromSeconds(2)), summary);
        Assert.Equal("UNSUPPORTED_CAPABILITY", refused);
    }
}
