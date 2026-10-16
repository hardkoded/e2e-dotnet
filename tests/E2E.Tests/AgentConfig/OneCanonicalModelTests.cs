// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentConfig;

// Upstream's model instance, gateway instance, and model string cases have no port: JSON names a model by id
// (see COMPATIBILITY.md, Config). The second assertion of "rejects an entry model and an executor model that differ", which names
// the same id on another provider, has no port: a model here is its name alone.
public sealed class OneCanonicalModelTests
{
    private static TestExecutor Brain(IAgentModel? model = null) => new("custom-brain", (_, _) => throw new InvalidOperationException("not called")) { Version = "1", Model = model };

    [Fact]
    public void Uses_the_model_a_custom_executor_brought_for_the_judgment_tier_too()
    {
        var model = new NamedModel("gpt-5.4-mini");

        var agent = ResolvedAgent.From("default", new AgentOptions { Executor = Brain(model) });

        Assert.Same(model, agent.Model);
        Assert.Same(model, agent.Judge);
    }

    [Fact]
    public void Accepts_the_entry_model_naming_the_same_model_as_the_executor()
    {
        var model = new NamedModel("gpt-5.4-mini");

        var agent = ResolvedAgent.From("default", new AgentOptions { Executor = Brain(model), Model = new NamedModel("gpt-5.4-mini") });

        Assert.Same(model, agent.Model);
    }

    [Fact]
    public void Rejects_an_entry_model_and_an_executor_model_that_differ()
    {
        var error = Assert.Throws<ConfigurationException>(() =>
            ResolvedAgent.From("default", new AgentOptions { Executor = Brain(new NamedModel("gpt-5.4-mini")), Model = new NamedModel("gpt-5.4") }));

        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Equal("agents.default.model (gpt-5.4) and the executor's own model (gpt-5.4-mini) differ; configure the model in one place", error.Message);
    }

    [Fact]
    public void Leaves_a_custom_executor_without_a_model_unconfigured()
    {
        Assert.Null(ResolvedAgent.From("default", new AgentOptions { Executor = Brain() }).Model);
    }
}
