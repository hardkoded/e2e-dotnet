// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;

namespace E2E.Tests.Debug;

/// <summary>.NET-only: upstream prints the run's phases from the runner. The port prints them when each session is released.</summary>
public sealed class SessionDebugTests
{
    [Fact]
    public async Task Prints_the_phases_of_the_session_and_of_the_agent_step_when_debug_is_on()
    {
        using var output = new StringWriter();
        await using (var session = await StartAsync(debug: true, output))
        {
            await session.App.OpenAsync("/");
            await session.Agent.ActAsync("increment the counter once");
            session.Complete();
        }

        var text = output.ToString();
        foreach (var phase in new[] { "session.launch", "test.body", "session.close", "agent.observe", "agent.model", "agent.action" })
        {
            Assert.Contains(phase, text, StringComparison.Ordinal);
        }

        Assert.StartsWith("[e2e debug] phase timings (wall ", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Prints_nothing_when_debug_is_off()
    {
        using var output = new StringWriter();
        await using (var session = await StartAsync(debug: false, output))
        {
            await session.App.OpenAsync("/");
            await session.Agent.ActAsync("increment the counter once");
            session.Complete();
        }

        Assert.Equal("", output.ToString());
    }

    private static Task<E2ESession> StartAsync(bool debug, TextWriter output) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(CounterPage.World()),
            Model = new ScriptedModel(request => request.Messages.Any(message => message.Role == "tool")
                ? ModelResponses.Done("passed", "tapped Increment")
                : ModelResponses.Tap("button", "Increment")),
            BaseUrl = "https://counter.test",
            TestTitle = "debug",
            Debug = debug,
            DebugOutput = output,
            CacheEnabled = false,
        });
}
