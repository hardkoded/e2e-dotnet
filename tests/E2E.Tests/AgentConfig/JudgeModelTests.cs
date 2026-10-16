// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.AgentAct;

namespace E2E.Tests.AgentConfig;

public sealed class JudgeModelTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "e2e-agent-config-project");

    [Fact]
    public void Is_the_model_unless_one_is_configured_so_judgments_always_have_one_rule()
    {
        var withModel = E2EConfig.Parse("""{ "agents": { "default": { "model": "gpt-4.1-mini" } } }""", Root, _ => null);
        var resolved = ResolvedAgent.From("default", withModel.Agent.CreateOptions());
        Assert.Equal("gpt-4.1-mini", resolved.Judge!.Name);
        Assert.Same(resolved.Model, resolved.Judge);

        Assert.Null(ResolvedAgent.From("default", E2EConfig.Parse("{}", Root, _ => null).Agent.CreateOptions()).Judge);
    }

    [Fact]
    public void Resolves_agent_judge_apart_from_agent_model()
    {
        var config = E2EConfig.Parse("""{ "agents": { "default": { "model": "gpt-4.1-mini", "judge": "o4-mini" } } }""", Root, _ => null);
        var resolved = ResolvedAgent.From("default", config.Agent.CreateOptions());

        Assert.Equal("gpt-4.1-mini", resolved.Model!.Name);
        Assert.Equal("o4-mini", resolved.Judge!.Name);
        Assert.Equal("o4-mini", config.Agent.Judge);
    }

    [Fact]
    public void Uses_the_judge_a_custom_executor_brought()
    {
        var judge = new NamedModel("gpt-5.4");
        var executor = new TestExecutor("custom-brain", (_, _) => throw new InvalidOperationException("not called")) { Model = new NamedModel("gpt-5.4-mini"), Judge = judge };

        var resolved = ResolvedAgent.From("default", new AgentOptions { Executor = executor });

        Assert.Same(judge, resolved.Judge);
        Assert.Equal("gpt-5.4-mini", resolved.Model!.Name);
    }

    [Fact]
    public void Rejects_an_entry_judge_and_an_executor_judge_that_differ()
    {
        var executor = new TestExecutor("custom-brain", (_, _) => throw new InvalidOperationException("not called"))
        {
            Model = new NamedModel("gpt-5.4-mini"),
            Judge = new NamedModel("gpt-5.4"),
        };

        var error = Assert.Throws<ConfigurationException>(() => ResolvedAgent.From("default", new AgentOptions { Executor = executor, Judge = new NamedModel("gpt-5.5") }));

        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Equal("agents.default.judge (gpt-5.5) and the executor's own judge (gpt-5.4) differ; configure the judge in one place", error.Message);
    }
}
