// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Mobile.DevFlow.Tests.Provider;

public sealed class AgentDiscoveryTests
{
    private const string StatusRoute = "GET /api/v1/agent/status";

    /// <summary>Long enough that the deadline cannot run out before the first try reaches the fake agent, even on a loaded machine.</summary>
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(1);

    private static (AgentDiscovery Discovery, DevFlowAgentClient Client, FakeAgent Agent) Over(Action<FakeAgent>? betweenTries = null)
    {
        var agent = new FakeAgent();
        var options = new DevFlowAgentsOptions();
        var http = agent.Http;
        var discovery = new AgentDiscovery(http, options)
        {
            Delay = (_, _) =>
            {
                betweenTries?.Invoke(agent);
                return Task.CompletedTask;
            },
        };
        return (discovery, new DevFlowAgentClient(http, FakeAgent.Agent, lease: null), agent);
    }

    [Fact]
    public async Task An_agent_that_accepts_the_request_and_never_answers_is_a_timeout()
    {
        var (discovery, client, agent) = Over();
        _ = agent.ScriptHang("GET", "/api/v1/agent/status");

        var error = await Assert.ThrowsAsync<DevFlowAgentException>(() => discovery.WaitForStatusAsync(client, Deadline, CancellationToken.None));

        Assert.Equal("timeout", error.Code);
        Assert.Contains("did not answer within", error.Message, StringComparison.Ordinal);
        Assert.Single(agent.Sent(StatusRoute));
    }

    [Fact]
    public async Task A_deadline_that_cancels_a_try_after_a_refused_one_is_unreachable()
    {
        // The wait between the two tries turns the agent from refusing to hanging, so the deadline cancels the second try.
        var (discovery, client, agent) = Over(betweenTries: agent => _ = agent.ScriptHang("GET", "/api/v1/agent/status"));
        agent.ScriptRefused("GET", "/api/v1/agent/status");

        var error = await Assert.ThrowsAsync<DevFlowAgentException>(() => discovery.WaitForStatusAsync(client, Deadline, CancellationToken.None));

        Assert.Equal("unreachable", error.Code);
        Assert.Contains("could not be reached", error.Message, StringComparison.Ordinal);
        Assert.Contains("(waited 1 seconds)", error.Message, StringComparison.Ordinal);
        Assert.Equal(2, agent.Sent(StatusRoute).Count());
    }

    [Fact]
    public async Task A_caller_that_cancels_while_the_agent_hangs_is_cancelled_not_timed_out()
    {
        var (discovery, client, agent) = Over();
        var hanging = agent.ScriptHang("GET", "/api/v1/agent/status");
        using var caller = new CancellationTokenSource();

        var wait = discovery.WaitForStatusAsync(client, TimeSpan.FromMinutes(5), caller.Token);
        await await Task.WhenAny(hanging, wait);
        await caller.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        Assert.Single(agent.Sent(StatusRoute));
    }
}
