// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Explorer = E2E.Explore;

namespace E2E.Tests.ExploreStepBudgets;

// .NET-only: upstream has no test for this. It reads `maxSteps ?? DEFAULT_STEP_BUDGET` where the config entry may leave the key out;
// the port keeps that "left out" as null, since an int cannot tell an absent key from an explicit 25. The last test is .NET-only too: it checks
// how `WithStepBudgets` joins the signed-in sentence into the context (upstream sets it as the test's `agentContext`).
public sealed class ExploreStepBudgetsTests
{
    private static readonly string Root = Path.GetTempPath();

    [Fact]
    public void Gives_an_agent_that_leaves_the_budgets_out_the_exploration_default_of_40()
    {
        var config = E2EConfig.Parse("""{ "agents": { "default": { "model": "gpt-4.1" } } }""", Root, _ => null);

        var explorer = Explorer.WithStepBudgets(config.Agent.CreateOptions());

        Assert.Equal(40, explorer.MaxSteps);
        Assert.Equal(40, explorer.MaxModelCalls);
    }

    [Fact]
    public void Keeps_budgets_the_agent_sets_to_the_config_default_of_25()
    {
        var config = E2EConfig.Parse("""{ "agents": { "default": { "model": "gpt-4.1", "maxSteps": 25, "maxModelCalls": 25 } } }""", Root, _ => null);

        var explorer = Explorer.WithStepBudgets(config.Agent.CreateOptions());

        Assert.Equal(25, explorer.MaxSteps);
        Assert.Equal(25, explorer.MaxModelCalls);
    }

    [Fact]
    public void Treats_each_budget_on_its_own()
    {
        var config = E2EConfig.Parse("""{ "agents": { "default": { "maxSteps": 25 } } }""", Root, _ => null);

        var explorer = Explorer.WithStepBudgets(config.Agent.CreateOptions());

        Assert.Equal(25, explorer.MaxSteps);
        Assert.Equal(40, explorer.MaxModelCalls);
    }

    [Fact]
    public void Keeps_budgets_set_in_code_to_the_config_default_of_25()
    {
        var explorer = Explorer.WithStepBudgets(new AgentOptions { MaxSteps = 25, MaxModelCalls = 25 });

        Assert.Equal(25, explorer.MaxSteps);
        Assert.Equal(25, explorer.MaxModelCalls);
        Assert.Equal(40, Explorer.WithStepBudgets(new AgentOptions()).MaxSteps);
    }

    [Fact]
    public void Adds_the_signed_in_sentence_to_the_context_of_an_exploration_from_a_session()
    {
        var config = E2EConfig.Parse("""{ "agents": { "default": { "model": "gpt-4.1", "context": "Be brief." } } }""", Root, _ => null);

        var explorer = Explorer.WithStepBudgets(config.Agent.CreateOptions(), "admin");
        var plain = Explorer.WithStepBudgets(config.Agent.CreateOptions());

        Assert.Equal("Be brief.\n" + global::E2E.ExplorePlan.SignedInContext("admin"), explorer.Context);
        Assert.Equal("Be brief.", plain.Context);
    }
}
