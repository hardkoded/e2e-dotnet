// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Acp.Tests.Executor.AcpHarness;

namespace E2E.Acp.Tests.Executor;

/// <summary>
/// Ports <c>describe('acp')</c> of tester-army/e2e <c>packages/acp/tests/executor.test.ts</c> (0410fc9c). The scripted agent is the C# program
/// <c>E2E.Acp.Tests.Agent</c>, and the steps run through a real session over the in-memory document engine, since the grammar tools drive the
/// context of a step the runtime opened. The port's verdict tool is <c>done</c> (upstream's <c>complete_step</c>) and takes <c>code</c>.
/// </summary>
[Collection(WorkingDirectoryCollection.Name)]
public sealed class AcpTests : IDisposable
{
    private static readonly object Pass = Call("done", new { status = "passed", summary = "added" });

    private readonly string _directory = Directory.CreateTempSubdirectory("e2e-acp-test-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // The scripted agent may still hold its log open.
        }
    }

    [Fact]
    public void Rejects_a_config_without_a_command()
    {
        var error = Assert.Throws<ConfigurationException>(() => AcpExecutor.Create(""));

        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains("takes a command and its args", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Acts_through_ctx_actions_and_passes_on_done()
    {
        var page = new TodoPage();
        var agent = Scripted(_directory, Turns([Fill("Title", "Buy milk"), Tap("Add"), Pass]), new AcpOptions { Model = "slow" });
        await using var session = await StartAsync(agent.Executor, page.World());

        var result = await session.Agent.ActAsync("add a todo");

        Assert.Equal("added", result.Summary);
        Assert.Equal("Buy milk", page.Title!.Value);
        Assert.Equal(1, page.Adds);
        var step = Assert.Single(session.Steps(), recorded => recorded.Api == "agent.act");
        Assert.Equal("scripted-agent", step.Model?.Provider);
        Assert.Equal("slow", step.Model?.Model);
        var entries = agent.Log();
        Assert.Contains(entries, entry => entry["config"]?["model"]?.GetValue<string>() == "slow");
        var offered = Assert.Single(agent.Sessions())["session"]!;
        Assert.Equal("e2e_step", offered["server"]!.GetValue<string>());
        // The built-in agent's tools for the target's verbs, plus done.
        Assert.Equal(
            ["back", "check", "clear", "done", "double_tap", "fill", "fill_secret", "navigate", "observe", "press", "press_at", "screenshot", "scroll", "scroll_to", "select", "select_at", "tap", "tap_at", "type_at", "uncheck"],
            offered["tools"]!.AsArray().Select(tool => tool!.GetValue<string>()).Order(StringComparer.Ordinal));
        Assert.Contains("Typed into", agent.ResultOf("fill"), StringComparison.Ordinal);
        Assert.Contains("Tapped", agent.ResultOf("tap"), StringComparison.Ordinal);
        // The step's first screen is whole, in the message.
        Assert.Contains("button \"Add\"", agent.Prompts()[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Counts_cached_tokens_into_the_input_when_the_agent_reports_them_apart()
    {
        // The Claude adapter's shape: its total adds the cache to input and output.
        var usage = """{"totalTokens":24,"inputTokens":5,"outputTokens":5,"cachedReadTokens":10,"cachedWriteTokens":4}""";
        var agent = Scripted(_directory, Turns([Pass]), env: new Dictionary<string, string> { ["ACP_USAGE"] = usage });
        await using var session = await StartAsync(agent.Executor, new TodoPage().World());

        await session.Agent.ActAsync("add a todo");

        Assert.Equal(19, session.InputTokens);
        Assert.Equal(5, session.OutputTokens);
    }

    [Fact]
    public async Task Keeps_one_session_per_attempt_and_sends_the_rules_only_once()
    {
        var agent = Scripted(_directory, Turns([Pass], [Pass]));
        await using var session = await StartAsync(agent.Executor, new TodoPage().World());

        await session.Agent.ActAsync("add a todo", new ActOptions { Params = new Dictionary<string, object?> { ["title"] = "Buy milk" } });
        await session.Agent.ActAsync("add another");

        var prompts = agent.Prompts();
        Assert.Equal(2, prompts.Count);
        Assert.Contains("You are an e2e test agent", prompts[0], StringComparison.Ordinal);
        Assert.Contains("Step: add a todo", prompts[0], StringComparison.Ordinal);
        Assert.Contains("Parameters: {\"title\":\"Buy milk\"}", prompts[0], StringComparison.Ordinal);
        Assert.DoesNotContain("You are an e2e test agent", prompts[1], StringComparison.Ordinal);
        Assert.Single(agent.Sessions());
        var pid = Assert.Single(agent.Pids());
        session.Complete();
        await EventuallyAsync(() => Assert.False(IsRunning(pid)));
    }

    [Fact]
    public async Task Partial_Starts_a_fresh_session_for_the_next_member_of_a_serial_group()
    {
        // Partial: the port's attempts keep a memory each (serial members do not share one), so this checks that the next attempt's step
        // starts an agent of its own and that the first attempt's agent stops when its attempt ends.
        var agent = Scripted(_directory, Turns([Pass], [Pass]));
        var world = new TodoPage().World();
        var first = await StartAsync(agent.Executor, world);
        await first.Agent.ActAsync("add a todo");
        first.Complete();
        await first.DisposeAsync();
        await using var second = await StartAsync(agent.Executor, world);

        var result = await second.Agent.ActAsync("add a todo");

        Assert.Equal("added", result.Summary);
        Assert.Equal(2, agent.Sessions().Count);
        await EventuallyAsync(() => Assert.False(IsRunning(agent.Pids()[0])));
    }

    [Fact]
    public async Task Partial_Refuses_actions_that_change_the_screen_on_an_assertion_and_lets_it_read()
    {
        // Partial: upstream lets the assertion hover; the document engine has no pointer, so the read-only tool here is scroll.
        var page = new TodoPage();
        var agent = Scripted(_directory, Turns([Tap("Add"), Call("scroll", new { direction = "down" }), Call("done", new { status = "failed", summary = "no todo" })]));
        await using var session = await StartAsync(agent.Executor, page.World());

        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.AssertAsync("the list shows a todo"));

        // The runner gives a failed assertion its ASSERTION_FAILED code.
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Contains("no todo", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, page.Adds);
        Assert.Contains("this step is an assertion", agent.ResultOf("tap"), StringComparison.Ordinal);
        Assert.DoesNotContain("this step is an assertion", agent.ResultOf("scroll"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Concludes_through_the_built_in_agents_done_blocked_needs_a_code()
    {
        var page = new TodoPage();
        var agent = Scripted(_directory, Turns([
            Call("done", new { status = "blocked", summary = "the app is down" }),
            Call("done", new { status = "blocked", summary = "the app is down", code = "APP_UNREACHABLE" }),
            Tap("Add"),
        ]));
        await using var session = await StartAsync(agent.Executor, page.World());

        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo"));

        Assert.Equal("APP_UNREACHABLE", error.Code);
        Assert.Contains("the app is down", error.Message, StringComparison.Ordinal);
        var answers = agent.Log().Where(entry => entry["call"]?.GetValue<string>() == "done").Select(entry => entry["result"]!.ToJsonString()).ToList();
        Assert.Contains("blocked", answers[0], StringComparison.Ordinal);
        Assert.Contains("isError", answers[0], StringComparison.Ordinal);
        Assert.Contains("Step concluded. Wait for the next instruction.", answers[1], StringComparison.Ordinal);
        // Nothing runs after the verdict.
        Assert.Equal(0, page.Adds);
        Assert.Contains("Finish with done when", agent.Prompts()[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Skips_actions_batched_behind_the_verdict()
    {
        var page = new TodoPage();
        var agent = Scripted(_directory, Turns([new { batch = new[] { Call("tap", new { role = "button", name = "Slow" }), Tap("Add"), Pass } }]));
        await using var session = await StartAsync(agent.Executor, page.World());

        // The slow tap is still running when done concludes; the tap queued behind it.
        var result = await session.Agent.ActAsync("add a todo");

        Assert.Equal("added", result.Summary);
        Assert.Equal(0, page.Adds);
        Assert.Contains(agent.Log(), entry => entry["call"]?.GetValue<string>() == "tap" && entry["result"]!.ToJsonString().Contains("The step has ended.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Rejects_the_agents_own_tools()
    {
        var root = Path.Combine(_directory, "project");
        var agent = Scripted(_directory, Turns([new { own = "Bash: ls ~", kind = "execute" }, Pass]));
        await using var session = await StartAsync(agent.Executor, new TodoPage().World(), debug: true, projectRoot: root);

        await session.Agent.ActAsync("add a todo");

        Assert.Contains(agent.Log(), entry => entry["permission"]?.GetValue<string>() == "Bash: ls ~" && entry["outcome"]!["optionId"]!.GetValue<string>() == "no");
        Assert.Contains("rejected tools of the agent's own: Bash: ls ~", Transcripts(root, session), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stops_a_step_whose_agent_runs_a_tool_of_its_own_without_asking_before_it_acts_on_what_it_read()
    {
        var root = Path.Combine(_directory, "project");
        var page = new TodoPage();
        var agent = Scripted(_directory, Turns([new { ran = "Read ~/.env", kind = "read" }, Fill("Title", "leaked"), Pass]));
        await using var session = await StartAsync(agent.Executor, page.World(), debug: true, projectRoot: root);

        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo"));

        Assert.Equal("POLICY_DENIED", error.Code);
        Assert.Contains("Read ~/.env", error.Message, StringComparison.Ordinal);
        Assert.NotEqual("leaked", page.Title!.Value);
        Assert.Contains(agent.Log(), entry => entry.ContainsKey("cancelled"));
        Assert.Contains("tools of the agent's own that ran without asking: Read ~/.env", Transcripts(root, session), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Starts_a_fresh_agent_for_the_next_step_after_a_tool_of_the_agents_own_ran()
    {
        // Keyed by step: the fresh agent starts its script over.
        var agent = Scripted(_directory, new Dictionary<string, object>
        {
            ["Step: add a todo"] = new object[] { new { ran = "Read ~/.env", kind = "read" }, Pass },
            ["Step: add another"] = new object[] { Pass },
        });
        await using var session = await StartAsync(agent.Executor, new TodoPage().World());

        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo"));
        await session.Agent.ActAsync("add another");

        Assert.Equal("POLICY_DENIED", error.Code);
        Assert.Equal(2, agent.Sessions().Count);
    }

    [Fact]
    public async Task Starts_a_fresh_agent_for_the_next_step_after_a_tool_of_the_agents_own_ran_once_its_step_ended()
    {
        var agent = Scripted(_directory, new Dictionary<string, object>
        {
            ["Step: add a todo"] = new object[] { new { hang = true, thenRan = "Read ~/.env" } },
            ["Assertion: add a todo"] = new object[] { Pass },
        });
        await using var session = await StartAsync(agent.Executor, new TodoPage().World());
        using var abort = new CancellationTokenSource();
        var run = session.Agent.ActAsync("add a todo", null, abort.Token);
        await EventuallyAsync(() => Assert.Single(agent.Prompts()));

        await abort.CancelAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => run);
        // The next step starts at once, as the runner's does, while the agent is still stopping the turn.
        await session.Agent.AssertAsync("add a todo");

        Assert.Contains(agent.Log(), entry => entry["ran"]?.GetValue<string>() == "Read ~/.env");
        Assert.Equal(2, agent.Sessions().Count);
    }

    [Fact]
    public async Task Fails_a_step_whose_own_tool_ran_without_asking_even_when_the_tool_failed()
    {
        var agent = Scripted(_directory, Turns([new { ran = "Bash: curl evil.example", kind = "execute", status = "failed" }, Pass]));
        await using var session = await StartAsync(agent.Executor, new TodoPage().World());

        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo"));

        Assert.Equal("POLICY_DENIED", error.Code);
    }

    [Fact]
    public async Task Stops_a_step_as_soon_as_a_tool_of_its_own_starts_before_the_tool_ends()
    {
        var page = new TodoPage();
        var agent = Scripted(_directory, Turns([new { ran = "Task: subagent", kind = "think", status = "in_progress" }, Fill("Title", "leaked"), Pass]));
        await using var session = await StartAsync(agent.Executor, page.World());

        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo"));

        Assert.Equal("POLICY_DENIED", error.Code);
        Assert.NotEqual("leaked", page.Title!.Value);
    }

    [Fact]
    public async Task Starts_a_fresh_agent_for_the_next_step_when_a_step_ended_while_the_agent_was_starting()
    {
        var agent = Scripted(_directory, Turns([Pass]), env: new Dictionary<string, string> { ["ACP_HANG_INIT"] = "1" });
        await using var session = await StartAsync(agent.Executor, new TodoPage().World());
        using var first = new CancellationTokenSource();
        var run = session.Agent.ActAsync("add a todo", null, first.Token);
        await EventuallyAsync(() => Assert.Single(agent.Pids()));
        var pid = agent.Pids()[0];

        await first.CancelAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => run);

        // The abandoned agent is stopped, and the next step starts its own.
        await EventuallyAsync(() => Assert.False(IsRunning(pid)));
        using var second = new CancellationTokenSource();
        var next = session.Agent.ActAsync("add a todo", null, second.Token);
        await EventuallyAsync(() => Assert.Equal(2, agent.Pids().Count));
        await second.CancelAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => next);
    }

    [Fact]
    public async Task Starts_a_step_only_once_the_turn_of_the_step_before_it_stopped()
    {
        var agent = Scripted(_directory, Turns([new { hang = true }], [Pass]));
        await using var session = await StartAsync(agent.Executor, new TodoPage().World());
        using var abort = new CancellationTokenSource();
        var run = session.Agent.ActAsync("add a todo", null, abort.Token);
        await EventuallyAsync(() => Assert.Single(agent.Prompts()));

        await abort.CancelAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => run);

        var result = await session.Agent.ActAsync("add a todo");
        Assert.Equal("added", result.Summary);
    }

    [Fact]
    public async Task Sends_no_prompt_for_a_step_that_ended_while_its_screen_was_read()
    {
        var agent = Scripted(_directory, Turns([Pass]));
        using var abort = new CancellationTokenSource();
        var world = new TodoPage().World();
        var engine = new HookedEngine(new DocumentEngine(world));
        await using var session = await StartAsync(agent.Executor, world, engine: engine);
        // The first look of the step is the one that ends it.
        engine.OnObserve = () => abort.Cancel();

        await Assert.ThrowsAnyAsync<Exception>(() => session.Agent.ActAsync("add a todo", null, abort.Token));

        // The next step's prompt is the agent's first turn.
        var result = await session.Agent.ActAsync("add a todo");
        Assert.Equal("added", result.Summary);
        Assert.Single(agent.Prompts());
    }

    [Fact]
    public async Task Gives_up_on_a_session_whose_agent_never_stops_the_turn_of_a_step_that_ended()
    {
        var agent = Scripted(_directory, Turns([new { ignoreCancel = true }], [Pass]));
        await using var session = await StartAsync(agent.Executor, new TodoPage().World());
        using var abort = new CancellationTokenSource();
        var run = session.Agent.ActAsync("add a todo", null, abort.Token);
        await EventuallyAsync(() => Assert.Single(agent.Prompts()));

        await abort.CancelAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => run);

        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo"));
        Assert.Equal("MODEL_PROVIDER_FAILED", error.Code);
        Assert.Contains("did not stop", error.Message, StringComparison.Ordinal);
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
        var agent = Scripted(_directory, Turns([.. asks.Select(ask => (object)new { own = ask.Own, kind = ask.Kind, rawInput = ask.RawInput, meta = ask.Meta }), Pass]));
        await using var session = await StartAsync(agent.Executor, new TodoPage().World());

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
        var agent = Scripted(_directory, Turns([call, Pass]));
        await using var session = await StartAsync(agent.Executor, new TodoPage().World());

        var result = await session.Agent.ActAsync("add a todo");

        Assert.Equal("added", result.Summary);
        Assert.Contains(agent.Log(), entry => entry["permission"]?.GetValue<string>() == "e2e_step.tap" && entry["outcome"]!["optionId"]!.GetValue<string>() == "yes");
    }

    [Fact]
    public async Task Keeps_e2es_secret_and_credential_variables_from_the_agent()
    {
        var names = new[] { "E2E_SECRET_STRIPE_KEY", "E2E_USER_ADMIN_PASSWORD", "e2e_secret_lower" };
        foreach (var name in names)
        {
            Environment.SetEnvironmentVariable(name, "value");
        }

        try
        {
            // Named in the executor's own env too, they still do not reach the agent.
            var agent = Scripted(_directory, Turns([Pass]), env: new Dictionary<string, string> { ["E2E_SECRET_OTHER"] = "also-secret", ["E2E_ACP_TEST_KEPT"] = "1" });
            await using var session = await StartAsync(agent.Executor, new TodoPage().World());

            await session.Agent.ActAsync("add a todo");

            var env = Assert.Single(agent.Log(), entry => entry.ContainsKey("env"))["env"]!.AsArray().Select(name => name!.GetValue<string>()).ToList();
            Assert.Contains("E2E_ACP_TEST_KEPT", env);
            Assert.DoesNotContain(env, name => name.StartsWith("E2E_SECRET_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("E2E_USER_", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            foreach (var name in names)
            {
                Environment.SetEnvironmentVariable(name, null);
            }
        }
    }

    [Fact]
    public async Task Fails_with_STEP_NO_CONCLUSION_when_the_turn_ends_without_done()
    {
        var agent = Scripted(_directory, Turns([new { say = "I could not find the button." }]));
        await using var session = await StartAsync(agent.Executor, new TodoPage().World());

        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo"));

        Assert.Equal("STEP_NO_CONCLUSION", error.Code);
        Assert.Contains("I could not find the button.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reports_what_the_agent_said_about_a_turn_it_failed()
    {
        var agent = Scripted(_directory, Turns([new { fail = "Quota exceeded. Check your plan and billing details." }]));
        await using var session = await StartAsync(agent.Executor, new TodoPage().World());

        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo"));

        Assert.Equal("MODEL_PROVIDER_FAILED", error.Code);
        Assert.Contains("Internal error: Quota exceeded.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partial_Rethrows_a_runtime_error_a_tool_hit_after_cancelling_the_turn()
    {
        // The port's action budget answers the agent in text instead of failing the call, so the runtime error is the one the step's
        // onAction hook raises inside the tool: the runner reports it as the step's failure. Partial: that error ends the step, which
        // closes its accounting, so the cancelled turn is not counted as a model call as upstream's is.
        var agent = Scripted(_directory, Turns([Tap("Add"), new { hang = true }]));
        await using var session = await StartAsync(agent.Executor, new TodoPage().World());

        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync(
            "add a todo",
            new ActOptions { OnAction = _ => throw new InvalidOperationException("hook failed") }));

        Assert.Contains("onAction failed: hook failed", error.Message, StringComparison.Ordinal);
        Assert.Contains(agent.Log(), entry => entry.ContainsKey("cancelled"));
    }

    [Fact]
    public async Task Reports_a_model_the_agent_does_not_offer()
    {
        var agent = Scripted(_directory, Turns([Pass]), new AcpOptions { Model = "huge" });
        await using var session = await StartAsync(agent.Executor, new TodoPage().World());

        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo"));

        // The runner reports an INVALID_CONFIG from an executor as POLICY_DENIED.
        Assert.Equal("POLICY_DENIED", error.Code);
        Assert.Contains("does not offer model huge; it offers fast, slow", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partial_Lists_the_tools_once_and_tells_the_agent_which_ones_a_step_does_not_offer()
    {
        // Partial: the document engine offers every step the same tools, so no step lacks a tool the session listed.
        // fill_secret is listed, and a call of it for a secret the step does not declare says so.
        var agent = Scripted(_directory, Turns([Call("fill_secret", new { role = "textbox", name = "Title", secret = "admin.password" }), Pass], [Pass]));
        await using var session = await StartAsync(agent.Executor, new TodoPage().World());

        await session.Agent.ActAsync("add a todo");
        await session.Agent.ActAsync("add a todo");

        var offered = Assert.Single(agent.Sessions())["session"]!["tools"]!.AsArray().Select(tool => tool!.GetValue<string>()).ToList();
        Assert.Contains("fill_secret", offered);
        Assert.DoesNotContain("Not offered in this step", agent.Prompts()[0], StringComparison.Ordinal);
        Assert.Contains("isError", agent.ResultOf("fill_secret"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Launches_the_agent_from_the_project_directory_and_opens_the_session_in_an_empty_one()
    {
        // A relative command resolves against the project, as `npx` finds a locally installed adapter.
        var agent = Scripted(_directory, Turns([Pass]), command: Path.GetRelativePath(Environment.CurrentDirectory, AgentExecutable));
        await using var session = await StartAsync(agent.Executor, new TodoPage().World());

        await session.Agent.ActAsync("add a todo");

        var cwd = Assert.Single(agent.Sessions())["session"]!["cwd"]!.GetValue<string>();
        Assert.StartsWith(Path.Combine(Path.GetTempPath(), "e2e-acp-"), cwd, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gives_each_executor_its_own_session_in_the_same_attempt()
    {
        var first = Scripted(_directory, Turns([Pass]), new AcpOptions { Model = "fast" });
        var second = Scripted(_directory, Turns([Pass]), new AcpOptions { Model = "slow" });
        await using var session = await StartAsync(first.Executor, new TodoPage().World(), agents: new Dictionary<string, AgentOptions> { ["second"] = new() { Executor = second.Executor } });

        await session.Agent.ActAsync("add a todo");
        await session.Agent.ActAsync("add a todo", new ActOptions { Agent = "second" });

        Assert.Single(first.Sessions());
        Assert.Single(second.Sessions());
        Assert.Contains(second.Log(), entry => entry["config"]?["model"]?.GetValue<string>() == "slow");
    }

    [Fact]
    public async Task Partial_Stops_an_agent_that_hangs_at_startup_when_the_attempt_ends()
    {
        // Partial: the port's step ends with its attempt, so the step reports the end of the attempt rather than the agent's start failing.
        var agent = Scripted(_directory, Turns([Pass]), env: new Dictionary<string, string> { ["ACP_HANG_INIT"] = "1" });
        var session = await StartAsync(agent.Executor, new TodoPage().World());
        var run = session.Agent.ActAsync("add a todo");
        await EventuallyAsync(() => Assert.Single(agent.Pids()));
        var pid = agent.Pids()[0];

        session.Complete();

        await Assert.ThrowsAnyAsync<Exception>(() => run);
        await EventuallyAsync(() => Assert.False(IsRunning(pid)));
        await session.DisposeAsync();
    }

    [Fact]
    public async Task Ends_a_step_that_times_out_while_the_agent_is_still_starting()
    {
        var agent = Scripted(_directory, Turns([Pass]), env: new Dictionary<string, string> { ["ACP_HANG_INIT"] = "1" });
        await using var session = await StartAsync(agent.Executor, new TodoPage().World());

        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo", new ActOptions { Timeout = TimeSpan.FromSeconds(1) }));

        Assert.Equal("STEP_TIMEOUT", error.Code);
    }

    [Fact]
    public async Task Reports_an_agent_that_does_not_start()
    {
        var executor = AcpExecutor.Create(Path.Combine(Path.GetTempPath(), "no-such-acp-agent"));
        await using var session = await StartAsync(executor, new TodoPage().World());

        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("add a todo"));

        Assert.Equal("MODEL_PROVIDER_FAILED", error.Code);
    }

    private static object Call(string tool, object? args = null) => new { call = tool, args = args ?? new { } };

    private static object Tap(string name) => Call("tap", new { role = "button", name });

    private static object Fill(string name, string value) => Call("fill", new { role = "textbox", name, value });

    /// <summary>One list of moves per prompt turn.</summary>
    private static object[] Turns(params object[][] turns) => turns;

    /// <summary>What the executor attached under debug, once the session settled.</summary>
    private static string Transcripts(string root, E2ESession session)
    {
        session.Complete();
        return string.Join('\n', Directory.GetFiles(root, "transcript-*.txt", SearchOption.AllDirectories).Select(File.ReadAllText));
    }
}
