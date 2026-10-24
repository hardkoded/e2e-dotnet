// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentEntryPrompt;

public sealed class TheBuiltInAgentFromItsAgentsEntryTests
{
    [Fact]
    public async Task Puts_system_and_context_in_the_act_prompt()
    {
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "done"));
        await using var session = await StartAsync(model, system: "Be careful.", context: "Plans are called tiers.");

        await session.Agent.ActAsync("open billing");

        var system = model.Requests[0].System;
        Assert.Contains("Be careful.", system, StringComparison.Ordinal);
        Assert.Contains("Project context:\nPlans are called tiers.", system, StringComparison.Ordinal);
        session.Complete();
    }

    [Fact]
    public async Task Joins_the_context_with_the_test_level_agentContext()
    {
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "done"));
        await using var session = await StartAsync(model, context: "Plans are called tiers.", testContext: "Billing lives under Settings.");

        await session.Agent.ActAsync("open billing");

        Assert.Contains("Project context:\nPlans are called tiers.\nBilling lives under Settings.", model.Requests[0].System, StringComparison.Ordinal);
        session.Complete();
    }

    [Fact]
    public async Task Opens_the_context_with_the_test_title_path()
    {
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "done"));
        // The port's test title is one string that stands for upstream's title path, so it arrives already joined. The file makes the
        // session a test, as upstream's `test` does.
        await using var session = await StartAsync(model, context: "Plans are called tiers.", testContext: "Billing lives under Settings.", file: "BillingTests", title: "billing > upgrades the plan");

        await session.Agent.ActAsync("open billing");

        Assert.Contains(
            "Project context:\nTest: billing > upgrades the plan\nPlans are called tiers.\nBilling lives under Settings.",
            model.Requests[0].System,
            StringComparison.Ordinal);
        session.Complete();
    }

    [Fact]
    public async Task Gives_a_judgment_the_title_too()
    {
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "shown"));
        await using var session = await StartAsync(model, file: "BillingTests", title: "billing > upgrades the plan");

        await session.Agent.AssertAsync("the plan is upgraded");

        Assert.Contains("<project-context>\nTest: billing > upgrades the plan\n</project-context>", model.Requests[0].System, StringComparison.Ordinal);
        session.Complete();
    }

    /// <summary>
    /// .NET-only: upstream's standalone attempt (an <c>e2e mcp</c> session) passes no <c>test</c> to its fixtures, and the port's
    /// session says the same with no <c>TestFile</c>. Upstream has no test for the absence.
    /// </summary>
    [Fact]
    public async Task Opens_no_title_line_for_a_session_with_no_test_file()
    {
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "done"));
        await using var session = await StartAsync(model, context: "Plans are called tiers.", title: "e2e mcp session 1");

        await session.Agent.ActAsync("open billing");
        await session.Agent.AssertAsync("the plan is shown");

        Assert.Equal(2, model.Requests.Count);
        Assert.All(model.Requests, request => Assert.DoesNotContain("Test: ", request.System, StringComparison.Ordinal));
        Assert.Contains("Project context:\nPlans are called tiers.", model.Requests[0].System, StringComparison.Ordinal);
        Assert.Contains("<project-context>\nPlans are called tiers.\n</project-context>", model.Requests[1].System, StringComparison.Ordinal);
        session.Complete();
    }

    [Fact]
    public async Task Builds_each_agent_from_its_own_entry_another_agents_system_never_leaks_in()
    {
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "done"));
        await using var session = await StartAsync(
            model,
            system: "Be careful.",
            agents: new Dictionary<string, AgentOptions> { ["ux"] = new() { Model = model, System = "Review the layout." } });

        await session.Agent.ActAsync("open billing", new ActOptions { Agent = "ux" });

        Assert.Contains("Review the layout.", model.Requests[0].System, StringComparison.Ordinal);
        Assert.DoesNotContain("Be careful.", model.Requests[0].System, StringComparison.Ordinal);
        session.Complete();
    }

    private static async Task<E2ESession> StartAsync(ScriptedModel model, string? system = null, string? context = null, Dictionary<string, AgentOptions>? agents = null, string? testContext = null, string? file = null, string title = "entry prompt")
    {
        var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(BillingWorld.Create()),
            Model = model,
            AgentSystem = system,
            AgentContext = context,
            TestAgentContext = testContext,
            Agents = agents,
            BaseUrl = "https://billing.test",
            TestFile = file,
            TestTitle = title,
        });
        await session.App.OpenAsync("/settings/billing");
        return session;
    }
}
