// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.TestAgentOptions;

// .NET-only: upstream's resolveOptions ("pins a test to a configured agent, innermost wins, and rejects a name agents does
// not define", "concatenates agentContext outer-to-inner with newlines") reads a collection of describe blocks, which the
// port has not. These tests read the attributes of a test class instead, so they cover the same rules only in part.
public sealed class AgentPinTests
{
    [Fact]
    public void The_method_pin_wins_over_the_class_pin_and_a_class_with_none_is_unpinned()
    {
        var method = typeof(Buyer).GetMethod(nameof(Buyer.AsTheAdmin))!;

        Assert.Equal("admin", E2E.TestAgentOptions.Agent(typeof(Buyer), method));
        Assert.Equal("buyer", E2E.TestAgentOptions.Agent(typeof(Buyer), typeof(Buyer).GetMethod(nameof(Buyer.AsTheBuyer))));
        Assert.Null(E2E.TestAgentOptions.Agent(typeof(Unpinned), null));
    }

    [Fact]
    public void An_enclosing_class_pins_its_nested_class_and_the_nested_pin_wins()
    {
        Assert.Equal("buyer", E2E.TestAgentOptions.Agent(typeof(Buyer.Plain), null));
        Assert.Equal("admin", E2E.TestAgentOptions.Agent(typeof(Buyer.Refunds), null));
    }

    [Fact]
    public void Concatenates_agentContext_outer_to_inner_with_newlines()
    {
        var leaf = typeof(Outer.Inner).GetMethod(nameof(Outer.Inner.Leaf))!;

        Assert.Equal("outer ctx\nleaf ctx", E2E.TestAgentOptions.AgentContext(typeof(Outer), typeof(Outer).GetMethod(nameof(Outer.Leaf))));
        Assert.Equal("outer ctx\ninner ctx\nleaf ctx", E2E.TestAgentOptions.AgentContext(typeof(Outer.Inner), leaf));
        Assert.Null(E2E.TestAgentOptions.AgentContext(typeof(Unpinned), null));
    }

    [Fact]
    public async Task A_call_that_names_no_agent_uses_the_pinned_agent_and_a_call_that_names_one_wins()
    {
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "done"));
        await using var session = await StartAsync(model, agent: "buyer");

        await session.Agent.ActAsync("open billing");
        await session.Agent.ActAsync("open billing", new ActOptions { Agent = "admin" });
        await session.Agent.ActAsync("open billing", new ActOptions { Agent = "default" });

        Assert.Contains("You are a buyer.", model.Requests[0].System, StringComparison.Ordinal);
        Assert.Contains("You are an admin.", model.Requests[1].System, StringComparison.Ordinal);
        Assert.DoesNotContain("You are a buyer.", model.Requests[2].System, StringComparison.Ordinal);
        Assert.DoesNotContain("You are an admin.", model.Requests[2].System, StringComparison.Ordinal);
        session.Complete();
    }

    [Fact]
    public async Task Rejects_a_pin_that_agents_does_not_define()
    {
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "done"));

        var error = await Assert.ThrowsAsync<TestException>(() => StartAsync(model, agent: "buyr"));

        Assert.Equal("INVALID_ARGUMENT", error.Code);
        Assert.Contains("unknown agent \"buyr\"; configured: default, buyer, admin", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Appends_the_test_context_to_every_agent_after_its_own()
    {
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "done"));
        await using var session = await StartAsync(model, agent: "buyer", testContext: "The workspace is Acme Trial.");

        await session.Agent.ActAsync("open billing");

        Assert.Contains("Project context:\nPlans are tiers.\nThe workspace is Acme Trial.", model.Requests[0].System, StringComparison.Ordinal);
        session.Complete();
    }

    private static async Task<E2ESession> StartAsync(ScriptedModel model, string? agent = null, string? testContext = null)
    {
        var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(BillingWorld.Create()),
            Model = model,
            Agents = new Dictionary<string, AgentOptions>
            {
                ["buyer"] = new() { Model = model, System = "You are a buyer.", Context = "Plans are tiers." },
                ["admin"] = new() { Model = model, System = "You are an admin." },
            },
            Agent = agent,
            TestAgentContext = testContext,
            BaseUrl = "https://billing.test",
            TestTitle = "agent pin",
        });
        await session.App.OpenAsync("/settings/billing");
        return session;
    }

    [Agent("buyer")]
    private class Buyer
    {
        [Agent("admin")]
        public void AsTheAdmin()
        {
        }

        public void AsTheBuyer()
        {
        }

        public class Plain
        {
        }

        [Agent("admin")]
        public class Refunds
        {
        }
    }

    private class Unpinned
    {
    }

    [AgentContext("outer ctx")]
    private class Outer
    {
        [AgentContext("leaf ctx")]
        public void Leaf()
        {
        }

        [AgentContext("inner ctx")]
        public class Inner
        {
            [AgentContext("leaf ctx")]
            public void Leaf()
            {
            }
        }
    }
}
