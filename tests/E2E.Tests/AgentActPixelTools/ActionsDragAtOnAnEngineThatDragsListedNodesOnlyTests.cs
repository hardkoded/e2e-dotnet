// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentActPixelTools;

/// <summary>
/// Upstream's <c>describe('actions.dragAt on an engine that drags listed nodes only')</c>. The join of two controls by id needs the
/// agent's node <c>drag</c> verb (#861), which the port does not have yet, so only the refusal of a drag the engine cannot take is ported.
/// </summary>
public sealed class ActionsDragAtOnAnEngineThatDragsListedNodesOnlyTests
{
    // Partial of upstream's "joins the two controls by id and says so, and refuses a drag inside one control": the first drag, which joins
    // the Card to Done by id, waits for the node drag verb (#861). A drag from a point to another inside one control is refused here too.
    [Fact]
    public async Task Partial_refuses_a_drag_the_engine_cannot_take_as_UNSUPPORTED_CAPABILITY()
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
        var executor = new TestExecutor(
            "card-dragger",
            async (context, token) =>
            {
                await context.ObserveAsync(cancellationToken: token);
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
        await session.Agent.ActAsync("move the card to done");
        session.Complete();

        Assert.Equal("UNSUPPORTED_CAPABILITY", refused);
        Assert.Empty(engine.Performed);
    }
}
