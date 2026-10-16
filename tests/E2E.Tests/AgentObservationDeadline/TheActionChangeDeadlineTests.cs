// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentObservationDeadline;

/// <summary>The action's change window includes device capture time and delays before observation.</summary>
public sealed class TheActionChangeDeadlineTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(700);

    private static DocumentWorld World() => new DocumentWorld().Map("/", page => page.Paragraph("End of list"));

    [Fact]
    public async Task Counts_time_spent_in_the_capture_before_checking_stability()
    {
        var engine = new SlowCaptureEngine(World(), Delay);
        var model = new ScriptedModel(request =>
            request.Messages.Any(message => message.Role == "tool")
                ? ModelResponses.Done("passed", "the list has not changed")
                : ModelResponses.Call("scroll", new { direction = "down" }));
        await using var session = await StartAsync(engine, model: model);
        await session.App.OpenAsync("/");
        var result = await session.Agent.ActAsync("scroll once and inspect the result");
        session.Complete();

        Assert.Equal("the list has not changed", result.Summary);
        Assert.Equal(2, engine.Captures);
    }

    [Fact]
    public async Task Counts_time_spent_in_the_executor_before_checking_stability()
    {
        var engine = new SlowCaptureEngine(World(), TimeSpan.Zero);
        var executor = new TestExecutor("delayed-observer", async (context, _) =>
        {
            await context.ObserveAsync();
            await context.Actions.ScrollAsync(null, ScrollDirection.Down);
            await Task.Delay(Delay);
            var screen = await context.ObserveAsync();
            Assert.Contains("End of list", screen.Text, StringComparison.Ordinal);
            return Passed("the list has not changed");
        });
        await using var session = await StartAsync(engine, executor: executor);
        await session.App.OpenAsync("/");
        var result = await session.Agent.ActAsync("scroll once and inspect the result");
        session.Complete();

        Assert.Equal("the list has not changed", result.Summary);
        Assert.Equal(2, engine.Captures);
    }

    private static Task<E2ESession> StartAsync(SlowCaptureEngine engine, IAgentModel? model = null, IStepExecutor? executor = null) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            Model = model,
            AgentExecutor = executor,
            BaseUrl = "https://app.test",
            CacheEnabled = false,
            TestTitle = "observes after an unchanged scroll",
        });
}
