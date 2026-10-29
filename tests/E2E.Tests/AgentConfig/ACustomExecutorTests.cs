// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentConfig;

/// <summary>
/// The agents entry that names a custom brain. Not ported, because the port has no such feature or the types leave nothing to
/// reject: "digests a custom agent by name and version, deterministically" (the port has no config digest), "ignores a context
/// member on the executor, string or not" (an <see cref="IStepExecutor"/> has no such member), "rejects a bare StepExecutor, pointing
/// at { executor }" and "rejects maxTurns on createToolLoopExecutor, naming maxModelCalls" (an entry is an <see cref="AgentOptions"/>,
/// and <see cref="ToolLoopExecutorOptions"/> has no such option).
/// </summary>
public sealed class ACustomExecutorTests
{
    private static TestExecutor Brain() => new("custom-brain", (_, _) => throw new InvalidOperationException("not called")) { Version = "1" };

    private static AgentTool ReadOnlyTool() => new("looks something up", new JsonObject { ["type"] = "object" }, false, (_, _) => Task.FromResult("found"));

    [Fact]
    public void Keeps_the_options_at_defaults_beside_it()
    {
        var agent = ResolvedAgent.From("default", new AgentOptions { Executor = Brain() });

        Assert.Equal("custom-brain", agent.Executor!.Name);
        Assert.Null(agent.Model);
        Assert.Equal(E2EDefaults.MaxSteps, agent.MaxSteps);
        Assert.Null(agent.Tools);
    }

    [Fact]
    public void Accepts_the_model_judge_context_and_budgets_alongside_it()
    {
        var model = new NamedModel("gpt-5.4-mini");
        var judge = new NamedModel("gpt-5.4");

        var agent = ResolvedAgent.From("default", new AgentOptions
        {
            Executor = Brain(),
            Model = model,
            Judge = judge,
            Context = "Plans are tiers.",
            MaxModelCalls = 40,
            JudgmentTimeout = TimeSpan.FromSeconds(60),
            MaxInputTokens = 8_000,
        });

        Assert.Equal("custom-brain", agent.Executor!.Name);
        Assert.Equal("Plans are tiers.", agent.Context);
        Assert.Equal(40, agent.MaxModelCalls);
        Assert.Equal(TimeSpan.FromSeconds(60), agent.JudgmentTimeout);
        Assert.Equal(8_000, agent.MaxInputTokens);
        Assert.Same(model, agent.Model);
        Assert.Same(judge, agent.Judge);
    }

    [Fact]
    public void Rejects_system_and_tools_which_belong_to_the_built_in_agent()
    {
        var system = Assert.Throws<ConfigurationException>(() => ResolvedAgent.From("default", new AgentOptions { Executor = Brain(), System = "Be careful." }));
        var tools = Assert.Throws<ConfigurationException>(() =>
            ResolvedAgent.From("ux", new AgentOptions { Executor = Brain(), Tools = new Dictionary<string, AgentTool> { ["lookup"] = ReadOnlyTool() } }));

        Assert.Equal("INVALID_CONFIG", system.Code);
        Assert.Equal(
            "agents.default.system is an option of the built-in agent, and agents.default.executor replaces it: a custom executor brings its own prompt; drop system or drop executor",
            system.Message);
        Assert.Equal(
            "agents.ux.tools is an option of the built-in agent, and agents.ux.executor replaces it: a custom executor brings its own tools; drop tools or drop executor",
            tools.Message);
    }

    /// <summary>Partial: the types keep any value that is no executor out, so the one thing left to reject is an executor with no name.</summary>
    [Fact]
    public void Partial_Rejects_an_executor_value_that_is_not_a_StepExecutor()
    {
        var error = Assert.Throws<ConfigurationException>(() => ResolvedAgent.From("default", new AgentOptions { Executor = new TestExecutor(" ", (_, _) => throw new InvalidOperationException("not called")) }));

        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains("agents.default.executor must be a StepExecutor", error.Message, StringComparison.Ordinal);
    }
}

/// <summary>A model that is only a name, which must never be called.</summary>
internal sealed class NamedModel(string name) : IAgentModel
{
    public string Name => name;

    public Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException("not called");
}
