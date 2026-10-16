// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Tests.AgentConfig;

// Upstream's "rejects the names of the tools the harness adds in an agent step, an e2e mcp session, and explore" is ported for
// report_finding alone: the port has none of the other tools it names (complete_step, locate, the recording tools). Neither has
// "digests tools by name and live values by identity", a model instance as the entry, or the config digest.
public sealed class TheBuiltInAgentOptionsTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "e2e-agent-config-project");

    private static AgentTool ReadOnlyTool() => new("looks", new JsonObject { ["type"] = "object" }, false, (_, _) => Task.FromResult("ok"));

    /// <summary>The JSON config holds no tools, so they are read from the options the agent is built from.</summary>
    [Fact]
    public void Keeps_system_and_the_agent_tools_and_rejects_a_system_that_is_not_a_string()
    {
        var config = E2EConfig.Parse("""{ "agents": { "default": { "system": "Verify every total." } } }""", Root, _ => null);
        var lookup = ReadOnlyTool();
        var agent = ResolvedAgent.From("default", new AgentOptions { System = config.Agent.System, Tools = new Dictionary<string, AgentTool> { ["lookup"] = lookup } });

        Assert.Equal("Verify every total.", config.Agent.System);
        Assert.Equal("Verify every total.", config.Agent.CreateOptions().System);
        Assert.Equal("Verify every total.", agent.System);
        Assert.Same(lookup, Assert.Single(agent.Tools!).Value);
        var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "agents": { "default": { "system": 5 } } }""", Root, _ => null));
        Assert.Equal("agents.default.system must be a string", error.Message);
    }

    /// <summary>Partial port of "rejects the names of the tools the harness adds in an agent step, an e2e mcp session, and explore": only report_finding exists here, and the list of those tools is not compared with what the surfaces add.</summary>
    [Fact]
    public void Rejects_the_name_of_the_tool_the_harness_adds_in_explore()
    {
        var error = Assert.Throws<ConfigurationException>(() => ResolvedAgent.From("ux", new AgentOptions { Tools = new Dictionary<string, AgentTool> { ["report_finding"] = ReadOnlyTool() } }));

        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Equal("agents.ux.tools.report_finding: the report_finding tool name is reserved for the tool the harness adds in e2e explore; rename it", error.Message);
    }

    /// <summary>Partial port of "rejects a tool defineTool did not build, a tools value that is not an object, and a reserved name": the types leave only the reserved name to check, and the port has no screenshot tool.</summary>
    [Fact]
    public void Rejects_a_reserved_tool_name()
    {
        foreach (var name in new[] { "tap", "observe", "done" })
        {
            var error = Assert.Throws<ConfigurationException>(() => ResolvedAgent.From("ux", new AgentOptions { Tools = new Dictionary<string, AgentTool> { [name] = ReadOnlyTool() } }));

            Assert.Equal("INVALID_CONFIG", error.Code);
            Assert.Equal($"agents.ux.tools.{name}: the {name} tool name is reserved for the agent's own tools", error.Message);
        }
    }
}
