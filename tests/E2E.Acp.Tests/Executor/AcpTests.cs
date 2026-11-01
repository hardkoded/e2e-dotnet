// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Acp.Tests.AcpKit;

namespace E2E.Acp.Tests.Executor;

/// <summary>
/// <c>executor.test.ts</c>: <c>describe('acp')</c>. Upstream builds each step's context by hand; the port runs a real session over a
/// <see cref="DocumentWorld"/>, since the step tools drive the context the runtime builds. Not ported: <c>starts a fresh session for
/// the next member of a serial group</c> (the port has no serial groups) and <c>sends no prompt for a step that ended while its screen
/// was read</c> (it needs a context whose <c>observe</c> a test replaces).
/// </summary>
public sealed class AcpTests
{
    private static object Tap(string name = "Increment") => new { call = "tap", args = new { role = "button", name } };

    private static object Fill(string value) => new { call = "fill", args = new { role = "textbox", name = "Title", value } };

    private static readonly ActOptions Quick = new() { Timeout = TimeSpan.FromSeconds(2) };

    private static string Json(JsonNode? node) => node?.ToJsonString() ?? "";

    private static JsonObject Call(ScriptedAgent agent, string name) => agent.Log().First(entry => entry["call"]?.GetValue<string>() == name);

    [Fact]
    public void Rejects_a_config_without_a_command()
    {
        var error = Assert.Throws<ConfigurationException>(() => AcpExecutors.Acp(new AcpOptions { Command = "" }));
        Assert.Contains("takes { Command, Args }", error.Message);
        Assert.Equal("INVALID_CONFIG", error.Code);
    }

    [Fact]
    public async Task Acts_through_ctx_actions_and_passes_on_done()
    {
        var typed = new List<string>();
        var taps = 0;
        using var agent = new ScriptedAgent(new[] { new[] { Fill("Buy milk"), Tap(), Pass } }, new AcpOptions { Command = "x", Model = "slow" });
        await using var session = await StartAsync(World(typed.Add, () => taps++), agent.Executor);
        await session.App.OpenAsync("/");
        var act = await session.Agent.ActAsync("add a todo");

        Assert.Equal(["Buy milk"], typed);
        Assert.Equal(1, taps);
        Assert.Equal(1, act.ModelCalls);
        Assert.Equal(1, session.ModelCalls);
        Assert.Contains(agent.Log(), entry => entry["config"]?["model"]?.GetValue<string>() == "slow");
        var started = agent.Sessions().Single()["session"]!;
        Assert.Equal("e2e_step", started["server"]!.GetValue<string>());
        // The built-in agent's tools for the target's verbs, plus done.
        var tools = started["tools"]!.AsArray().Select(tool => tool!.GetValue<string>()).Order().ToList();
        Assert.Contains("done", tools);
        Assert.Contains("observe", tools);
        Assert.Contains("tap", tools);
        Assert.Contains("fill", tools);
        Assert.Contains("Tapped", Json(Call(agent, "tap")["result"]), StringComparison.OrdinalIgnoreCase);
        // The step's first screen is whole, in the message.
        Assert.Contains("Increment", agent.Prompts()[0]);
    }

    [Fact]
    public async Task Counts_cached_tokens_into_the_input_when_the_agent_reports_them_apart()
    {
        // The Claude adapter's shape: its total adds the cache to input and output.
        using var agent = new ScriptedAgent(
            new[] { new[] { Pass } },
            env: new Dictionary<string, string> { ["ACP_USAGE"] = """{"totalTokens":24,"inputTokens":5,"outputTokens":5,"cachedReadTokens":10,"cachedWriteTokens":4}""" });
        await using var session = await StartAsync(World(), agent.Executor);
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync("add a todo");

        Assert.Equal(19, session.InputTokens);
        Assert.Equal(5, session.OutputTokens);
    }

    [Fact]
    public async Task Keeps_one_session_per_attempt_and_sends_the_rules_only_once()
    {
        using var agent = new ScriptedAgent(new[] { new[] { Pass }, new[] { Pass } });
        int pid;
        await using (var session = await StartAsync(World(), agent.Executor))
        {
            await session.App.OpenAsync("/");
            await session.Agent.ActAsync("add a todo");
            await session.Agent.ActAsync("add a todo");
            var prompts = agent.Prompts();
            Assert.Equal(2, prompts.Count);
            Assert.Contains("You are an e2e test agent.", prompts[0]);
            Assert.Contains("Step: add a todo", prompts[0]);
            Assert.DoesNotContain("You are an e2e test agent.", prompts[1]);
            Assert.Single(agent.Sessions());
            pid = agent.Log().First(entry => entry.ContainsKey("pid"))["pid"]!.GetValue<int>();
        }

        await EventuallyAsync(() => !IsRunning(pid), "the agent to stop with the attempt");
    }

    [Fact]
    public async Task Refuses_actions_that_change_the_screen_on_an_assertion_PARTIAL()
    {
        // The port has no hover verb, so the half of upstream's `it` that lets an assertion hover has no counterpart.
        using var agent = new ScriptedAgent(
            new[] { new[] { Tap(), new { call = "done", args = new { status = "failed", summary = "no todo" } } } });
        var taps = 0;
        await using var session = await StartAsync(World(onTap: () => taps++), agent.Executor);
        await session.App.OpenAsync("/");
        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.AssertAsync("a todo is listed"));

        // The runner gives a failed assertion its ASSERTION_FAILED code.
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Equal(0, taps);
        Assert.Contains("this step is an assertion", Json(Call(agent, "tap")["result"]));
    }

    [Fact]
    public async Task Concludes_through_the_built_in_agents_done_blocked_needs_a_code()
    {
        using var agent = new ScriptedAgent(
            new[]
            {
                new[]
                {
                    new { call = "done", args = (object)new { status = "blocked", summary = "the app is down" } },
                    new { call = "done", args = (object)new { status = "blocked", summary = "the app is down", code = "APP_UNREACHABLE" } },
                    new { call = "tap", args = (object)new { role = "button", name = "Increment" } },
                },
            });
        var taps = 0;
        await using var session = await StartAsync(World(onTap: () => taps++), agent.Executor);
        await session.App.OpenAsync("/");
        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo"));

        Assert.Equal("APP_UNREACHABLE", error.Code);
        var answers = agent.Log().Where(entry => entry["call"]?.GetValue<string>() == "done").Select(entry => Json(entry["result"])).ToList();
        Assert.Contains("Rejected: a blocked verdict requires a code", answers[0]);
        Assert.Contains("Step concluded. Wait for the next instruction.", answers[1]);
        // Nothing runs after the verdict.
        Assert.Equal(0, taps);
        Assert.Contains("Verdict rules:", agent.Prompts()[0]);
    }

    [Fact]
    public async Task Skips_actions_batched_behind_the_verdict()
    {
        using var agent = new ScriptedAgent(new[] { new[] { new { batch = new[] { Fill("Buy milk"), Tap(), Pass } } } });
        var taps = 0;
        // The fill is still running when done concludes; the tap queued behind it.
        await using var session = await StartAsync(World(value => Thread.Sleep(300), () => taps++), agent.Executor);
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync("add a todo");

        Assert.Equal(0, taps);
        Assert.Contains("The step has ended.", Json(Call(agent, "tap")["result"]));
    }

    [Fact]
    public async Task Rejects_the_agents_own_tools()
    {
        using var agent = new ScriptedAgent(new[] { new object[] { new { own = "Bash: ls ~", kind = "execute" }, Pass } });
        await using var session = await StartAsync(World(), agent.Executor);
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync("add a todo");

        Assert.Contains(agent.Log(), entry => entry["permission"]?.GetValue<string>() == "Bash: ls ~" && entry["outcome"]?["optionId"]?.GetValue<string>() == "no");
    }

    [Fact]
    public async Task Stops_a_step_whose_agent_runs_a_tool_of_its_own_without_asking_before_it_acts_on_what_it_read()
    {
        using var agent = new ScriptedAgent(new[] { new object[] { new { ran = "Read ~/.env", kind = "read" }, Fill("leaked"), Pass } });
        var typed = new List<string>();
        await using var session = await StartAsync(World(typed.Add), agent.Executor);
        await session.App.OpenAsync("/");
        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo"));

        Assert.Equal("POLICY_DENIED", error.Code);
        Assert.Contains("Read ~/.env", error.Message);
        Assert.Empty(typed);
        Assert.Contains(agent.Log(), entry => entry["cancelled"]?.GetValue<bool>() == true);
    }

    [Fact]
    public async Task Starts_a_fresh_agent_for_the_next_step_after_a_tool_of_the_agents_own_ran()
    {
        using var agent = new ScriptedAgent(new[] { new object[] { new { ran = "Read ~/.env", kind = "read" }, Pass }, new object[] { Pass } });
        await using var session = await StartAsync(World(), agent.Executor);
        await session.App.OpenAsync("/");
        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo"));
        Assert.Equal("POLICY_DENIED", error.Code);
        // The fresh agent starts its script over, so its first turn runs the tool of its own again.
        await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo"));

        Assert.Equal(2, agent.Sessions().Count);
    }

    [Fact]
    public async Task Starts_a_fresh_agent_for_the_next_step_after_a_tool_of_the_agents_own_ran_once_its_step_ended()
    {
        using var agent = new ScriptedAgent(
            new Dictionary<string, object[]>
            {
                ["Step: add a todo"] = [new { hang = true, thenRan = "Read ~/.env" }],
                ["Assertion: add a todo"] = [Pass],
            });
        await using var session = await StartAsync(World(), agent.Executor);
        await session.App.OpenAsync("/");
        var first = await Assert.ThrowsAnyAsync<Exception>(() => session.Agent.ActAsync("add a todo", Quick));
        Assert.Contains("STEP_TIMEOUT", first is E2EException coded ? coded.Code : first.Message);
        // The next step starts at once, while the agent is still stopping the turn.
        await session.Agent.AssertAsync("add a todo");

        Assert.Contains(agent.Log(), entry => entry["ran"]?.GetValue<string>() == "Read ~/.env");
        Assert.Equal(2, agent.Sessions().Count);
    }

    [Fact]
    public async Task Fails_a_step_whose_own_tool_ran_without_asking_even_when_the_tool_failed()
    {
        using var agent = new ScriptedAgent(new[] { new object[] { new { ran = "Bash: curl evil.example", kind = "execute", status = "failed" }, Pass } });
        await using var session = await StartAsync(World(), agent.Executor);
        await session.App.OpenAsync("/");
        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo"));

        Assert.Equal("POLICY_DENIED", error.Code);
    }

    [Fact]
    public async Task Stops_a_step_as_soon_as_a_tool_of_its_own_starts_before_the_tool_ends()
    {
        using var agent = new ScriptedAgent(new[] { new object[] { new { ran = "Task: subagent", kind = "think", status = "in_progress" }, Fill("leaked"), Pass } });
        var typed = new List<string>();
        await using var session = await StartAsync(World(typed.Add), agent.Executor);
        await session.App.OpenAsync("/");
        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo"));

        Assert.Equal("POLICY_DENIED", error.Code);
        Assert.Empty(typed);
    }

    [Fact]
    public async Task Starts_a_fresh_agent_for_the_next_step_when_a_step_ended_while_the_agent_was_starting()
    {
        using var agent = new ScriptedAgent(new[] { new[] { Pass } }, env: new Dictionary<string, string> { ["ACP_HANG_INIT"] = "1" });
        await using var session = await StartAsync(World(), agent.Executor);
        await session.App.OpenAsync("/");
        var first = await Assert.ThrowsAnyAsync<Exception>(() => session.Agent.ActAsync("add a todo", Quick));
        Assert.Contains("STEP_TIMEOUT", first is E2EException coded ? coded.Code : first.Message);
        var pid = agent.Log().First(entry => entry.ContainsKey("pid"))["pid"]!.GetValue<int>();

        // The abandoned agent is stopped, and the next step starts its own.
        await EventuallyAsync(() => !IsRunning(pid), "the abandoned agent to stop");
        var next = await Assert.ThrowsAnyAsync<Exception>(() => session.Agent.ActAsync("add a todo"));
        Assert.Contains("STEP_TIMEOUT", next is E2EException other ? other.Code : next.Message);
        Assert.Equal(2, agent.Log().Count(entry => entry.ContainsKey("pid")));
    }

    [Fact]
    public async Task Starts_a_step_only_once_the_turn_of_the_step_before_it_stopped()
    {
        using var agent = new ScriptedAgent(new[] { new object[] { new { hang = true } }, new object[] { Pass } });
        await using var session = await StartAsync(World(), agent.Executor);
        await session.App.OpenAsync("/");
        var first = await Assert.ThrowsAnyAsync<Exception>(() => session.Agent.ActAsync("add a todo", Quick));
        Assert.Contains("STEP_TIMEOUT", first is E2EException coded ? coded.Code : first.Message);

        await session.Agent.ActAsync("add a todo");
    }

    [Fact]
    public async Task Gives_up_on_a_session_whose_agent_never_stops_the_turn_of_a_step_that_ended()
    {
        using var agent = new ScriptedAgent(new[] { new object[] { new { ignoreCancel = true } }, new object[] { Pass } });
        await using var session = await StartAsync(World(), agent.Executor);
        await session.App.OpenAsync("/");
        var first = await Assert.ThrowsAnyAsync<Exception>(() => session.Agent.ActAsync("add a todo", Quick));
        Assert.Contains("STEP_TIMEOUT", first is E2EException coded ? coded.Code : first.Message);

        // The step waits up to five seconds for the turn of the step before it to stop.
        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo", new ActOptions { Timeout = TimeSpan.FromSeconds(20) }));
        Assert.Equal("MODEL_PROVIDER_FAILED", error.Code);
        Assert.Contains("did not stop", error.Message);
    }

    [Fact]
    public async Task Allows_only_calls_the_adapter_names_as_one_of_the_step_tools_never_by_text_the_agent_wrote()
    {
        var asks = new (string Own, string? Kind, object? RawInput, object? Meta, bool Allowed)[]
        {
            // Ours, as the Claude adapter, an MCP approval's raw input, and an adapter title name them.
            ("claude ours", null, null, new { claudeCode = new { toolName = "mcp__e2e_step__tap" } }, true),
            ("codex ours", "execute", new { server = "e2e_step", tool = "tap", arguments = new { target = "n1" } }, new { is_mcp_tool_call = true }, true),
            ("subagent named e2e_step: tap", "think", new { server = "e2e_step", tool = "tap" }, null, false),
            ("other ours", "other", new { serverName = "e2e_step", toolName = "tap" }, null, true),
            // A title is text the agent may have written, so it allows nothing.
            ("e2e_step: tap", "other", null, null, false),
            // Not ours, though each names the step server.
            ("claude Read", null, null, new { claudeCode = new { toolName = "Read" } }, false),
            ("claude other server", null, null, new { claudeCode = new { toolName = "mcp__playwright__browser_navigate" } }, false),
            ("playwright: navigate to evil.example?x=e2e_step", "other", null, null, false),
            ("web_search e2e_step leak", "think", null, null, false),
            ("e2e_step: browser_navigate", "other", null, null, false),
            ("Fetch e2e_step: tap", "fetch", null, null, false),
            ("raw input of the agent", "other", new { query = "e2e_step tap", server = "e2e_step", tool = "tap" }, null, false),
            ("codex other server", "execute", new { server = "playwright", tool = "tap", arguments = new { } }, new { is_mcp_tool_call = true }, false),
            ("codex shell", "execute", new { command = "cat ~/.ssh/id_rsa # e2e_step tap" }, null, false),
        };
        var moves = asks.Select(ask => (object)new { own = ask.Own, kind = ask.Kind, rawInput = ask.RawInput, meta = ask.Meta }).Append(Pass).ToArray();
        using var agent = new ScriptedAgent(new[] { moves });
        await using var session = await StartAsync(World(), agent.Executor);
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync("add a todo");

        var outcomes = agent.Log()
            .Where(entry => entry.ContainsKey("permission"))
            .ToDictionary(entry => entry["permission"]!.GetValue<string>(), entry => entry["outcome"]!["optionId"]!.GetValue<string>() == "yes");
        Assert.Equal(asks.ToDictionary(ask => ask.Own, ask => ask.Allowed), outcomes);
    }

    [Fact]
    public async Task Keeps_a_call_allowed_by_its_permission_request_from_counting_as_one_of_its_own_once_it_completes()
    {
        // Named as an MCP call on the request only; the call itself looks like a shell command.
        var call = new
        {
            own = "e2e_step.tap",
            kind = "execute",
            rawInput = new { server = "e2e_step", tool = "tap", arguments = new { target = "n2" } },
            requestMeta = new { is_mcp_tool_approval = true },
            status = "completed",
        };
        using var agent = new ScriptedAgent(new[] { new object[] { call, Pass } });
        await using var session = await StartAsync(World(), agent.Executor);
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync("add a todo");

        Assert.Contains(agent.Log(), entry => entry["permission"]?.GetValue<string>() == "e2e_step.tap" && entry["outcome"]?["optionId"]?.GetValue<string>() == "yes");
    }

    [Fact]
    public async Task Keeps_e2es_secret_and_credential_variables_from_the_agent()
    {
        var names = new[] { ("E2E_SECRET_STRIPE_KEY", "sk_test_value"), ("E2E_USER_ADMIN_PASSWORD", "hunter2"), ("e2e_secret_lower", "lower") };
        var before = names.ToDictionary(pair => pair.Item1, pair => Environment.GetEnvironmentVariable(pair.Item1));
        try
        {
            foreach (var (name, value) in names)
            {
                Environment.SetEnvironmentVariable(name, value);
            }

            // Named in the executor's own env too, they still do not reach the agent.
            using var agent = new ScriptedAgent(
                new[] { new[] { Pass } },
                env: new Dictionary<string, string> { ["E2E_SECRET_OTHER"] = "also-secret", ["E2E_TELEMETRY_DISABLED"] = "1" });
            await using var session = await StartAsync(World(), agent.Executor);
            await session.App.OpenAsync("/");
            await session.Agent.ActAsync("add a todo");

            var env = agent.Log().First(entry => entry.ContainsKey("env"))["env"]!.AsArray().Select(name => name!.GetValue<string>()).ToList();
            Assert.Contains("E2E_TELEMETRY_DISABLED", env);
            Assert.DoesNotContain(env, name => name.StartsWith("E2E_SECRET_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("E2E_USER_", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            foreach (var (name, value) in before)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    [Fact]
    public async Task Fails_with_STEP_NO_CONCLUSION_when_the_turn_ends_without_done()
    {
        using var agent = new ScriptedAgent(new[] { new object[] { new { say = "I could not find the button." } } });
        await using var session = await StartAsync(World(), agent.Executor);
        await session.App.OpenAsync("/");
        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo"));

        Assert.Equal("STEP_NO_CONCLUSION", error.Code);
        Assert.Contains("I could not find the button.", error.Message);
    }

    [Fact]
    public async Task Reports_what_the_agent_said_about_a_turn_it_failed()
    {
        using var agent = new ScriptedAgent(new[] { new object[] { new { fail = "Quota exceeded. Check your plan and billing details." } } });
        await using var session = await StartAsync(World(), agent.Executor);
        await session.App.OpenAsync("/");
        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo"));

        Assert.Equal("MODEL_PROVIDER_FAILED", error.Code);
        Assert.Contains("Internal error: Quota exceeded.", error.Message);
    }

    [Fact]
    public async Task Reports_a_model_the_agent_does_not_offer()
    {
        using var agent = new ScriptedAgent(new[] { new[] { Pass } }, new AcpOptions { Command = "x", Model = "huge" });
        await using var session = await StartAsync(World(), agent.Executor);
        await session.App.OpenAsync("/");
        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo"));

        // The runtime reports a configuration error of an executor as POLICY_DENIED, with the INVALID_CONFIG error as its cause.
        Assert.Equal("INVALID_CONFIG", Assert.IsType<ConfigurationException>(error.InnerException).Code);
        Assert.Contains("does not offer model huge; it offers fast, slow", error.Message);
    }

    [Fact]
    public async Task Lists_the_tools_once_and_tells_the_agent_which_ones_a_step_does_not_offer_PARTIAL()
    {
        // The port's grammar has no screenshot tool and lists fill_secret for every step, so the upstream checks of `type_secret` and
        // of a screenshot reaching the agent as an MCP image have no counterpart; the tools are listed once, at the first step.
        using var agent = new ScriptedAgent(new[] { new[] { Pass }, new[] { Pass } });
        await using var session = await StartAsync(World(), agent.Executor);
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync("add a todo");
        await session.Agent.ActAsync("add a todo");

        Assert.Single(agent.Sessions());
        Assert.Contains("fill_secret", agent.Sessions()[0]["session"]!["tools"]!.AsArray().Select(tool => tool!.GetValue<string>()));
    }

    [Fact]
    public async Task Launches_the_agent_from_the_project_directory_and_opens_the_session_in_an_empty_one()
    {
        // A relative command resolves against the project, as `npx` finds a locally installed adapter.
        using var agent = new ScriptedAgent(new[] { new[] { Pass } }, new AcpOptions { Command = "x", Args = [Path.GetRelativePath(Directory.GetCurrentDirectory(), AcpKit.Agent)] });
        await using var session = await StartAsync(World(), agent.Executor);
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync("add a todo");

        var cwd = agent.Sessions().Single()["session"]!["cwd"]!.GetValue<string>();
        Assert.StartsWith(Path.Combine(Path.GetTempPath(), "e2e-acp-"), cwd);
    }

    [Fact]
    public async Task Gives_each_executor_its_own_session_in_the_same_attempt()
    {
        using var first = new ScriptedAgent(new[] { new[] { Pass } }, new AcpOptions { Command = "x", Model = "fast" });
        using var second = new ScriptedAgent(new[] { new[] { Pass } }, new AcpOptions { Command = "x", Model = "slow" });
        await using var session = await StartAsync(World(), first.Executor);
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync("add a todo");
        // A second executor in the same attempt shares its memory, which the key keeps apart.
        Assert.Single(first.Sessions());
        Assert.Empty(second.Sessions());
        await using var other = await StartAsync(World(), second.Executor);
        await other.App.OpenAsync("/");
        await other.Agent.ActAsync("add a todo");
        Assert.Single(second.Sessions());
        Assert.Contains(second.Log(), entry => entry["config"]?["model"]?.GetValue<string>() == "slow");
    }

    [Fact]
    public async Task Stops_an_agent_that_hangs_at_startup_when_the_attempt_ends()
    {
        using var agent = new ScriptedAgent(new[] { new[] { Pass } }, env: new Dictionary<string, string> { ["ACP_HANG_INIT"] = "1" });
        var session = await StartAsync(World(), agent.Executor);
        await session.App.OpenAsync("/");
        var run = session.Agent.ActAsync("add a todo");
        await EventuallyAsync(() => agent.Log().Any(entry => entry.ContainsKey("pid")), "the agent to start");
        var pid = agent.Log().First(entry => entry.ContainsKey("pid"))["pid"]!.GetValue<int>();
        await session.DisposeAsync();

        await Assert.ThrowsAnyAsync<Exception>(() => run);
        await EventuallyAsync(() => !IsRunning(pid), "the agent to stop");
    }

    [Fact]
    public async Task Ends_a_step_that_times_out_while_the_agent_is_still_starting()
    {
        using var agent = new ScriptedAgent(new[] { new[] { Pass } }, env: new Dictionary<string, string> { ["ACP_HANG_INIT"] = "1" });
        await using var session = await StartAsync(World(), agent.Executor, TimeSpan.FromSeconds(1));
        await session.App.OpenAsync("/");
        var error = await Assert.ThrowsAnyAsync<Exception>(() => session.Agent.ActAsync("add a todo"));

        Assert.Contains("STEP_TIMEOUT", error is E2EException coded ? coded.Code : error.Message);
    }

    [Fact]
    public async Task Reports_an_agent_that_does_not_start()
    {
        var executor = AcpExecutors.Acp(new AcpOptions { Command = Path.Combine(Path.GetTempPath(), "no-such-acp-agent") });
        await using var session = await StartAsync(World(), executor);
        await session.App.OpenAsync("/");
        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo"));

        Assert.Equal("MODEL_PROVIDER_FAILED", error.Code);
    }
}
