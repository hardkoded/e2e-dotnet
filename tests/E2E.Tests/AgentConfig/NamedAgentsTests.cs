// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentConfig;

// Upstream's `--agent` is E2E_AGENT here (E2ESessionOptions.Agent), one name.
public sealed class NamedAgentsTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "e2e-agent-config-project");

    [Fact]
    public void Always_has_a_default_agent_the_built_in_one_without_a_model_even_when_only_others_are_named()
    {
        var config = E2EConfig.Parse("""{ "agents": { "ux": { "model": "gpt-4.1-mini" } } }""", Root, _ => null);

        Assert.Null(config.Agent.Model);
        Assert.Null(config.Agent.CreateModel());
        Assert.Equal("gpt-4.1-mini", config.Agents["ux"].Model);
        // A custom brain on another agent leaves the default one built in. The config file cannot name an executor, so it comes with the options.
        var ux = ResolvedAgent.From("ux", new AgentOptions { Executor = new AgentAct.TestExecutor("ux-brain", (_, _) => throw new InvalidOperationException("not called")) });
        Assert.Equal("ux-brain", ux.Executor!.Name);
        Assert.Null(ResolvedAgent.From("default", config.Agent.CreateOptions()).Executor);
    }

    // Upstream's `--agent` is CliOverrides.Agents, the resolveConfig(raw, { cli }) option.
    [Fact]
    public void Rejects_an_unknown_agent_before_anything_starts_naming_the_configured_ones()
    {
        var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(
            """{ "agents": { "default": {}, "ux": {} } }""", Root, _ => null, new CliOverrides { Agents = ["uxx"] }));

        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Matches("unknown agent \"uxx\"; configured: default, ux; did you mean \"ux\"\\?", error.Message);
    }

    [Fact]
    public async Task Resolves_every_named_agent_and_runs_with_default_unless_E2E_AGENT_picks_another()
    {
        const string json = """{ "agents": { "default": { "model": "gpt-4.1-mini" }, "ux": { "model": "gpt-4.1", "context": "Review the UX." } } }""";
        var config = E2EConfig.Parse(json, Root, _ => null);
        Assert.Equal(["default", "ux"], config.Agents.Keys.ToArray());
        Assert.Equal(["default"], config.AgentNames);
        Assert.Equal("gpt-4.1-mini", config.Agent.Model);
        var picked = E2EConfig.Parse(json, Root, _ => null, new CliOverrides { Agents = ["ux"] });
        Assert.Equal(["ux"], picked.AgentNames);
        Assert.Equal("gpt-4.1", picked.Agent.Model);
        Assert.Equal("Review the UX.", picked.Agent.Context);
        Assert.Equal(["ux"], E2EConfig.Parse(json, Root, _ => null, new CliOverrides { Agents = ["ux", "ux"] }).AgentNames);
        // E2E_AGENT is the port's `--agent`.
        var fromEnvironment = E2EConfig.Parse(json, Root, name => name == "E2E_AGENT" ? " ux " : null);
        Assert.Equal(["ux"], fromEnvironment.AgentNames);
        Assert.Equal("gpt-4.1", fromEnvironment.Agent.Model);
        Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(json, Root, name => name == "E2E_AGENT" ? "uxx" : null));

        // The model each agent runs shows in its verdict. E2E_AGENT reaches the session as E2ESessionOptions.Agent.
        Assert.Equal("done by house", await RunAsync(null, "house", "ux-brain"));
        Assert.Equal("done by ux-brain", await RunAsync("ux", "house", "ux-brain"));
        Assert.Equal("done by house", await RunAsync("default", "house", "ux-brain"));
    }

    private static async Task<string> RunAsync(string? agent, string house, string ux)
    {
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(BillingWorld.Create()),
            Model = Signing(house),
            BaseUrl = "https://billing.test",
            CacheEnabled = false,
            TestTitle = "named agents",
            Agents = new Dictionary<string, AgentOptions> { ["ux"] = new() { Model = Signing(ux), Context = "Review the UX." } },
            Agent = agent,
        });
        await session.App.OpenAsync("/settings/billing");
        return (await session.Agent.ActAsync("open billing")).Summary;
    }

    // A model that signs its act verdict, so the result shows which one ran. A judgment answers "ready".
    private static ScriptedModel Signing(string name)
    {
        return new ScriptedModel(request => request.Tools.Any(tool => tool.Name == "tap")
            ? ModelResponses.Done("passed", "done by " + name)
            : ModelResponses.Done("passed", "ready"));
    }

    /// <summary>A live model is code, so the digest is computed from the options the agents are built from (<see cref="ResolvedConfig.ComputeConfigDigest"/>'s <c>agents</c>).</summary>
    [Fact]
    public void Digests_every_named_agent_so_changing_one_changes_the_digest_and_a_live_model_is_reduced_to_its_identity()
    {
        var config = E2EConfig.Parse("{}", Root, _ => null);
        string Of(Dictionary<string, AgentOptions> agents) => ResolvedConfig.ComputeConfigDigest(config, agents);
        var baseline = Of(new() { ["default"] = new() { Model = new NamedModel("gpt-5.4-mini") }, ["ux"] = new() { Context = "a" } });
        var changed = Of(new() { ["default"] = new() { Model = new NamedModel("gpt-5.4-mini") }, ["ux"] = new() { Context = "b" } });
        Assert.NotEqual(baseline, changed);
        var first = Of(new() { ["ux"] = new() { Model = new NamedModel("gpt-5.4-mini") } });
        var again = Of(new() { ["ux"] = new() { Model = new NamedModel("gpt-5.4-mini") } });
        Assert.Equal(first, again);
    }

    [Fact]
    public void Rejects_the_removed_agent_key_with_the_replacement_bad_names_and_a_non_object_agents()
    {
        Assert.Contains("unknown config key \"agent\"; agents are named", Invalid("""{ "agent": { "model": "gpt-4.1-mini" } }"""), StringComparison.Ordinal);
        Assert.Contains("agent name \"u x\"", Invalid("""{ "agents": { "u x": {} } }"""), StringComparison.Ordinal);
        // An agent's name is an artifact path segment, so "." and ".." are out.
        Assert.Contains("agent name \"..\"", Invalid("""{ "agents": { "..": {} } }"""), StringComparison.Ordinal);
        Assert.Contains("agent name \".\"", Invalid("""{ "agents": { ".": {} } }"""), StringComparison.Ordinal);
        Assert.True(E2EConfig.Parse("""{ "agents": { "v1.2": {} } }""", Root, _ => null).Agents.ContainsKey("v1.2"));
        Assert.Contains("agents must be an object of agents by name", Invalid("""{ "agents": [] }"""), StringComparison.Ordinal);
    }

    private static string Invalid(string json)
    {
        var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(json, Root, _ => null));
        Assert.Equal("INVALID_CONFIG", error.Code);
        return error.Message;
    }
}
