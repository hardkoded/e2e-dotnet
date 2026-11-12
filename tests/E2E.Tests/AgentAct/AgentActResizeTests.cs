// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Engine;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentAct;

[Collection(BrowserCollection.Name)]
public sealed class AgentActResizeTests
{
    [Fact]
    public async Task Resizes_the_page_through_the_engine_refuses_a_size_out_of_bounds_and_records_the_action()
    {
        using var site = await TinySite.StartAsync("<!doctype html><title>Home</title><h1>Home</h1>");
        var executor = new TestExecutor("resizer", async (context, _) =>
        {
            if (!context.Target.Verbs.Contains(ExecutorVerb.Resize))
            {
                return Failed("the web target offers no resize verb");
            }

            var refused = "accepted";
            try
            {
                await context.Actions.ResizeAsync(new TraceViewport(50, 844));
            }
            catch (E2EException ex)
            {
                refused = ex.Code;
            }

            await context.Actions.ResizeAsync(new TraceViewport(390, 844));
            var viewport = (await context.ObserveAsync()).Viewport!;
            return Passed("out of bounds: " + refused + "; viewport: " + viewport.Width.ToString(CultureInfo.InvariantCulture) + "x" + viewport.Height.ToString(CultureInfo.InvariantCulture));
        })
        {
            Version = "test",
        };
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            AgentExecutor = executor,
            BaseUrl = site.Url,
            TestTitle = "executor resizes the viewport",
            ActionTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(30),
            TestTimeout = TimeSpan.FromSeconds(60),
        });

        Exception? error = null;
        try
        {
            await session.App.OpenAsync("/");
            await session.Agent.ActAsync("check the layout on a phone");
        }
        catch (Exception ex)
        {
            error = ex;
        }

        session.Complete(error);
        Assert.Null(error);
        var step = Assert.Single(session.StepRecords, candidate => candidate.Api == "agent.act");
        Assert.Contains("out of bounds: INVALID_ARGUMENT; viewport: 390x844", step.Explanation, StringComparison.Ordinal);
        Assert.Equal(1, step.Metrics!.ActionSteps);
        Assert.Equal(["resize"], step.Events.Where(item => item.Kind == StepEventKind.Engine).Select(item => item.Name));
        Assert.Equal(new StepViewport(390, 844, 1), step.Viewport);
    }
}
