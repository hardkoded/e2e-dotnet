// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Reports;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentScrollSmooth;

/// <summary>
/// Upstream's <c>describe('scroll_to a text on a page that animates its scroll')</c> (<c>agent-scroll-smooth.test.ts</c>): scrolling to a
/// text in a real browser reaches its row when the page keeps scrolling after the swipe resolved. A page read mid-animation counts as
/// landed, and the next swipe adds to the one in flight; the wheel's target accumulates, so no row is skipped, and this pins that on a
/// JS animation and on <c>scroll-behavior: smooth</c>, at 450 ms and at 1.5 s. The page is <c>FixtureApp</c>'s <c>/smooth-scroll</c>.
/// Where upstream reads the failure from the step's engine events, the port reports the failure the step raised.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class ScrollToATextOnAPageThatAnimatesItsScrollTests
{
    private const string Row = "Row 120";

    private static readonly TestExecutor Executor = new("scroll-to-row", async (context, _) =>
    {
        await context.ObserveAsync();
        try
        {
            await context.Actions.ScrollUntilAsync(Row, ScrollDirection.Down);
        }
        catch (TestException exception)
        {
            return Failed(exception.Code + ": " + exception.Message, "STEP_NO_CONCLUSION");
        }

        var screen = await context.ObserveAsync();
        return screen.Text.Contains(Row, StringComparison.Ordinal)
            ? Passed("reached " + Row)
            : Failed(Row + " is not on the screen", "STEP_NO_CONCLUSION");
    })
    {
        Cache = ExecutorCache.Off,
    };

    [Theory]
    [InlineData("none", 450)]
    [InlineData("js", 450)]
    [InlineData("js", 1500)]
    [InlineData("css", 450)]
    public async Task Reaches_the_row_with_smooth_mode_over_ms(string mode, int ms)
    {
        using var site = await FixtureApp.StartAsync();
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            AgentExecutor = Executor,
            BaseUrl = site.Url,
            CacheEnabled = false,
            TestTitle = "reaches the row",
            StepTimeout = TimeSpan.FromSeconds(100),
            TestTimeout = TimeSpan.FromSeconds(120),
        });
        Exception? error = null;
        try
        {
            await session.App.OpenAsync("/smooth-scroll?smooth=" + mode + "&ms=" + ms);
            await session.Agent.ActAsync("reach the row");
        }
        catch (Exception exception)
        {
            error = exception;
        }

        session.Complete(error);
        var step = session.StepRecords.LastOrDefault(candidate => candidate.Api == "agent.act");
        var detail = step is null ? null : string.Join("; ", step.Events.Where(item => item.Kind == StepEventKind.Engine).Select(item => item.Detail));
        Assert.True(error is null, (detail ?? "") + " " + (error is null ? "\"\"" : "\"" + error.Message + "\""));
    }
}
