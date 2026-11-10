// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Tests.AgentConfig;

// Upstream's "rejects the names of the tools the harness adds in an agent step, an e2e mcp session, and explore" is ported as
// Partial_ (the port's verdict tool is done, not complete_step; the reserved names are a hand-kept list). Neither has a model
// instance as the entry.
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

    /// <summary>The recursive schema is a JSON Schema whose <c>$ref</c> points at itself, and the circular store is an <see cref="IStepCache"/> that holds a client pointing back at itself.</summary>
    [Fact]
    public void Digests_tools_by_name_and_live_values_by_identity_so_a_recursive_tool_schema_loads()
    {
        var schema = JsonNode.Parse(
            """
            { "type": "object", "properties": { "root": { "$ref": "#/$defs/Node" } },
              "$defs": { "Node": { "type": "object", "properties": { "name": { "type": "string" }, "children": { "type": "array", "items": { "$ref": "#/$defs/Node" } } } } } }
            """)!.AsObject();
        var tree = new AgentTool("walks a tree", schema, false, (_, _) => Task.FromResult("ok"));
        var config = E2EConfig.Parse("{}", Root, _ => null).WithCacheStore(new CircularStore());
        string Of(string name) => ResolvedConfig.ComputeConfigDigest(config, new Dictionary<string, AgentOptions> { ["default"] = new() { Tools = new Dictionary<string, AgentTool> { [name] = tree } } });

        var agent = ResolvedAgent.From("default", new AgentOptions { Tools = new Dictionary<string, AgentTool> { ["tree"] = tree } });
        Assert.Same(tree, Assert.Single(agent.Tools!).Value);
        Assert.Equal(Of("tree"), Of("tree"));
        Assert.NotEqual(Of("tree"), Of("walk"));
    }

    /// <summary>A store whose client points back at itself: JSON cannot clone it.</summary>
    private sealed class CircularStore : IStepCache
    {
        public Config.ResolveConfigTests.Cycle Client { get; } = new();

        public bool Writable => true;

        public CacheLookup Read(string key) => throw new InvalidOperationException("not called");

        public int? Write(string key, CacheEntry entry) => throw new InvalidOperationException("not called");

        public void Delete(string key) => throw new InvalidOperationException("not called");
    }

    /// <summary>
    /// Port of "rejects the names of the tools the harness adds in an agent step, an e2e mcp session, and explore": every name in
    /// upstream's <c>HARNESS_TOOL_NAMES</c> is refused with its surface. The port's verdict tool is <c>done</c>, which is refused
    /// below with the agent's own names. The list is kept by hand, so a new harness tool needs a new entry here.
    /// </summary>
    [Fact]
    public void Rejects_the_names_of_the_tools_the_harness_adds_in_an_agent_step_an_e2e_mcp_session_and_explore()
    {
        var where = new Dictionary<string, string>
        {
            ["complete_step"] = "every agent step",
            ["locate"] = "an e2e mcp session",
            ["run_code"] = "an e2e mcp session",
            ["test_lines"] = "an e2e mcp session",
            ["start_recording"] = "an e2e mcp session",
            ["stop_recording"] = "an e2e mcp session",
            ["report_finding"] = "e2e explore",
        };
        foreach (var (name, surface) in where)
        {
            var error = Assert.Throws<ConfigurationException>(() => ResolvedAgent.From("ux", new AgentOptions { Tools = new Dictionary<string, AgentTool> { [name] = ReadOnlyTool() } }));

            Assert.Equal("INVALID_CONFIG", error.Code);
            Assert.Equal($"agents.ux.tools.{name}: the {name} tool name is reserved for the tool the harness adds in {surface}; rename it", error.Message);
        }

        var agentStep = Assert.Throws<ConfigurationException>(() => ResolvedAgent.From("ux", new AgentOptions { Tools = new Dictionary<string, AgentTool> { ["done"] = ReadOnlyTool() } }));
        Assert.Equal("agents.ux.tools.done: the done tool name is reserved for the agent's own tools", agentStep.Message);
    }

    /// <summary>
    /// Partial port of "rejects a tool defineTool did not build, a tools value that is not an object, and a reserved name". Only the
    /// reserved names are checked here: a tool that was not built with <c>defineTool</c> and a tools value that is not an object
    /// cannot be written in C#. The port has no screenshot tool, so <c>screenshot</c> is not reserved.
    /// </summary>
    [Fact]
    public void Partial_Rejects_a_reserved_tool_name()
    {
        foreach (var name in new[] { "tap", "observe", "done" })
        {
            var error = Assert.Throws<ConfigurationException>(() => ResolvedAgent.From("ux", new AgentOptions { Tools = new Dictionary<string, AgentTool> { [name] = ReadOnlyTool() } }));

            Assert.Equal("INVALID_CONFIG", error.Code);
            Assert.Equal($"agents.ux.tools.{name}: the {name} tool name is reserved for the agent's own tools", error.Message);
        }
    }
}
