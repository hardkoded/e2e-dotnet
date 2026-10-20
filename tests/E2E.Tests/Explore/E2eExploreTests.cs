// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Tests.Agent;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;
using static E2E.Tests.Explore.ExploreHarness;
using Explorer = E2E.Explore;

namespace E2E.Tests.Explore;

/// <summary>
/// <c>e2e explore</c> end to end against the counter page, with one scripted model serving both surfaces: the planner's
/// structured call and the explorer's tool loop. The port has no runner or report here, so these tests read the outcome
/// <see cref="Explorer.ExploreAsync"/> returns where upstream's read the run's report. The tests of <c>--session</c>
/// (runs only the setup, keeps the pixels of a tainted session, an agentic setup, an unknown session) are not ported: the port has no saved sessions.
/// </summary>
public sealed class E2eExploreTests
{
    private static ExploreModel Model(Func<string, PlanAnswer> plan, Func<LoopCall, ModelResponse> loop) => new(plan, loop);

    [Fact]
    public async Task Keeps_a_screenshot_per_finding_when_the_model_reports_two_in_one_turn()
    {
        var model = Model(
            instruction => instruction.Contains("(none yet", StringComparison.Ordinal) ? ExploreModel.Step("Counter", "Look at the counter") : ExploreModel.Finish("Two defects on the home page."),
            call => call.Turn == 1
                ? CounterPage.Batch(
                    ExploreModel.ReportFinding(ExploreModel.CounterFinding()),
                    ExploreModel.ReportFinding(ExploreModel.CounterFinding("Increment button has no accessible description", severity: 2)))
                : ExploreModel.Done("passed", "Looked"));
        var root = TempRoot();
        var outcome = await Explorer.ExploreAsync(Options(root, model.Model, []));

        var record = outcome.Explore;
        Assert.Equal(
            new[] { "Counter starts at 0 with no label of what it counts", "Increment button has no accessible description" }.Order(StringComparer.Ordinal),
            record.Findings.Select(finding => finding.Title).Order(StringComparer.Ordinal));
        var paths = record.Findings.Select(finding => finding.ArtifactId).ToList();
        Assert.All(paths, path => Assert.True(path is not null && File.Exists(Path.Combine(root, path))));
        Assert.Equal(2, paths.Distinct().Count());
        Assert.Equal(["finding-1.png", "finding-2.png"], paths.Select(path => Path.GetFileName(path)![4..]).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Accepts_a_severity_written_as_a_whole_number_with_a_fraction_part()
    {
        var model = Model(
            instruction => instruction.Contains("(none yet", StringComparison.Ordinal) ? ExploreModel.Step("Counter", "Look at the counter") : ExploreModel.Finish("One defect."),
            call => call.Turn == 1
                ? ExploreModel.ReportFinding(new
                {
                    title = "Counter starts at 0 with no label of what it counts",
                    kind = "issue",
                    severity = System.Text.Json.Nodes.JsonNode.Parse("3.0"),
                    expected = "The counter says what it counts",
                    actual = "output \"Counter\" reads 0",
                    reproduction = new[] { "Open the home page" },
                })
                : ExploreModel.Done("passed", "Looked"));
        var outcome = await Explorer.ExploreAsync(Options(TempRoot(), model.Model, []));

        Assert.Equal(3, Assert.Single(outcome.Explore.Findings).Severity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Surfaces_a_cancel_from_the_finding_tool_as_a_cancel_and_not_as_a_refused_evidence_capture(bool refuseAttach)
    {
        var state = new E2E.ExploreState("goal", new ExploreBudgets(4, TimeSpan.FromMinutes(5)));
        var tool = ExploreExecutor.WithFindingTool(new AgentOptions(), state).Tools![ExploreExecutor.FindingToolName];
        var input = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(ExploreModel.CounterFinding()))!;

        // The refusal is an I/O error of the screen or the file system, and the cancel arrives with it.
        using var cts = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tool.RunAsync(input, new AgentToolContext(RefusingContext.Create(refuseAttach, cts), mutates: false), cts.Token));
        Assert.Empty(state.Findings);

        // Without a cancel, the same refusal leaves the finding standing without its evidence.
        await tool.RunAsync(input, new AgentToolContext(RefusingContext.Create(refuseAttach, null), mutates: false), CancellationToken.None);
        Assert.Null(Assert.Single(state.Findings).ArtifactId);
    }

    /// <summary>A step context whose observation, or its screenshot attachment, is refused as the file system and the screen refuse.</summary>
    private sealed class RefusingContext(bool refuseAttach, CancellationTokenSource? cancel) : StepExecutorContext
    {
        public static RefusingContext Create(bool refuseAttach, CancellationTokenSource? cancel) => new(refuseAttach, cancel)
        {
            Step = new ExecutorStep { Kind = ExecutorStepKind.Act, Instruction = "explore" },
            Attempt = new ExecutorAttempt { TestId = "t", AttemptId = "a" },
            Target = new ExecutorTargetInfo { Name = "web", Platform = "web" },
            Budgets = null!,
            Actions = null!,
        };

        public override Task<ExecutorObservation> ObserveAsync(ExecutorObserveOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (refuseAttach)
            {
                return Task.FromResult(new ExecutorObservation("screen") { Pixels = new ExecutorPixels([1], "image/png", 1, 1, 1, 0) });
            }

            cancel?.Cancel();
            throw new IOException("the screen is gone");
        }

        public override Task<string> AttachScreenshotAsync(ExecutorPixels pixels, string label, CancellationToken cancellationToken = default)
        {
            cancel?.Cancel();
            throw new UnauthorizedAccessException("the results directory is read-only");
        }
    }

    // Upstream also reads the report: its schema, the artifact record behind the evidence and the step that produced it, the
    // attempt's timeline, the observation revision, and report.json. The port keeps the record in the outcome and the evidence as a file.
    [Fact]
    public async Task Plans_steps_records_the_finding_with_its_screenshot_and_ends_the_outcome_failed_for_the_issue()
    {
        var planPrompts = new List<string>();
        var model = Model(
            instruction =>
            {
                planPrompts.Add(instruction);
                return planPrompts.Count == 1
                    ? ExploreModel.Step("Counter", "Tap Increment once and check the counter")
                    : ExploreModel.Finish("The counter works but says nothing about what it counts.");
            },
            call => call.Turn switch
            {
                1 => ModelResponses.Tap("button", "Increment"),
                2 => ExploreModel.ReportFinding(ExploreModel.CounterFinding()),
                _ => ExploreModel.Done("passed", "Counter went to 1"),
            });
        var root = TempRoot();
        var outcome = await Explorer.ExploreAsync(Options(root, model.Model, []));

        // The planner saw the goal each time, then the record of the step and its finding.
        Assert.Equal(2, planPrompts.Count);
        Assert.Contains("Goal: Explore the home page and find bugs", planPrompts[0], StringComparison.Ordinal);
        Assert.Contains("1. [passed] Counter — Counter went to 1", planPrompts[1], StringComparison.Ordinal);
        Assert.Contains("- [issue, severity 3] " + "Counter starts at 0 with no label of what it counts", planPrompts[1], StringComparison.Ordinal);
        Assert.Equal(1, outcome.ExitCode);
        Assert.Equal(RunStatus.Failed, outcome.Status);
        var error = Assert.IsType<AgentException>(outcome.Error);
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Contains("exploration found 1 issue(s)", error.Message, StringComparison.Ordinal);

        var record = outcome.Explore;
        Assert.Equal("Explore the home page and find bugs", record.Goal);
        Assert.Equal(new ExploreBudgets(2, TimeSpan.FromMinutes(3)), record.Budgets);
        Assert.Equal(ReportExploreEnded.Finished, record.Ended);
        Assert.Equal("The counter works but says nothing about what it counts.", record.Summary);
        var step = Assert.Single(record.Steps);
        Assert.Equal(1, step.Index);
        Assert.Equal("Counter", step.Title);
        Assert.Equal(ReportExploreStepStatus.Passed, step.Status);
        Assert.Equal("Counter went to 1", step.Summary);
        var finding = Assert.Single(record.Findings);
        Assert.Equal(1, finding.Step);
        Assert.Equal(ReportExploreFindingKind.Issue, finding.Kind);
        Assert.Equal(3, finding.Severity);
        Assert.Equal("Counter starts at 0 with no label of what it counts", finding.Title);
        Assert.Equal("/", finding.Path);

        // The evidence is a screenshot file under the attempt's results, named for the finding.
        Assert.Equal(".e2e/results/" + AttemptReport.ResultSegmentOf(null, "Explore the home page and find bugs") + "/attempt-1/screenshots/001-finding-1.png", finding.ArtifactId);
        Assert.Equal(ScreenshotEngine.Png(), await File.ReadAllBytesAsync(Path.Combine(root, finding.ArtifactId!)));

        // The explorer's loop saw the finding tool beside the grammar.
        Assert.Contains(ExploreExecutor.FindingToolName, model.LoopCalls.First().Tools.Select(tool => tool.Name));

        // The planner's calls carried the goal as trusted context.
        Assert.Contains("Exploration goal: Explore the home page and find bugs", model.PlannerCalls.First().System, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Passes_with_warnings_only_and_offers_the_project_tools_of_the_agents_entry()
    {
        var plans = 0;
        var model = Model(
            _ => ++plans == 1 ? ExploreModel.Step("Menu", "Open the menu") : ExploreModel.Finish("Only polish items."),
            call => call.Turn switch
            {
                1 => ModelResponses.Call("ping", new { }),
                2 => ExploreModel.ReportFinding(ExploreModel.CounterFinding(kind: "warning", severity: 1)),
                _ => ExploreModel.Done("passed", "Menu toggles"),
            });
        var ping = new AgentTool("answers pong", JsonNode.Parse("""{ "type": "object", "properties": {} }""")!.AsObject(), mutates: false, (_, _) => Task.FromResult("pong"));
        var notices = new List<string>();
        var outcome = await Explorer.ExploreAsync(Options(
            TempRoot(),
            model.Model,
            notices,
            agent: new AgentOptions { Model = model.Model, Tools = new Dictionary<string, AgentTool> { ["ping"] = ping }, System = "Project guidance line." }));

        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal(RunStatus.Passed, outcome.Status);
        var first = model.LoopCalls.First().Tools.Select(tool => tool.Name).ToList();
        Assert.Contains("ping", first);
        Assert.Contains(ExploreExecutor.FindingToolName, first);
        Assert.Contains("Project guidance line.", model.LoopCalls.First().System, StringComparison.Ordinal);
        Assert.Contains("pong", model.LoopCalls.ElementAt(1).Messages.Where(message => message.Role == "tool").Select(message => message.Content));
        Assert.Equal(ReportExploreFindingKind.Warning, outcome.Explore.Findings[0].Kind);
        Assert.Equal(1, outcome.Explore.Findings[0].Severity);
        Assert.Empty(notices);
    }

    // Upstream also reads the report and report.json for the secret value.
    [Fact]
    public async Task Redacts_a_registered_secret_value_the_goal_spells_out_from_the_title_artifact_names_outcome_and_model_input()
    {
        const string value = "goal-Secret-5Tq8Wz";
        var plans = 0;
        var model = Model(
            _ => ++plans == 1 ? ExploreModel.Step("Counter", "Look at the counter") : ExploreModel.Finish("One issue."),
            call => call.Turn == 1 ? ExploreModel.ReportFinding(ExploreModel.CounterFinding()) : ExploreModel.Done("passed", "Looked"));
        var root = TempRoot();
        var outcome = await Explorer.ExploreAsync(Options(
            root,
            model.Model,
            [],
            goal: "Explore the home page with key " + value,
            maxSteps: 1,
            config: ExploreHarness.Config(root, "\"secrets\": { \"probe\": \"" + value + "\" }")));

        Assert.Equal("Explore the home page with key <secret:probe>", outcome.Explore.Goal);
        Assert.Contains("Exploration goal: Explore the home page with key <secret:probe>", model.PlannerCalls.First().System, StringComparison.Ordinal);

        // The explorer's loop ran, carrying the goal as context.
        Assert.Contains("Exploration goal: Explore the home page with key <secret:probe>", model.LoopCalls.First().System, StringComparison.Ordinal);
        Assert.Contains(Files(root), file => file.Contains("explore-the-home-page-with-key-", StringComparison.Ordinal));
        Assert.DoesNotContain(value, Everything(model.Model.Requests), StringComparison.Ordinal);
        Assert.DoesNotContain(value, JsonSerializer.Serialize(outcome.Explore), StringComparison.Ordinal);
        Assert.All(Files(root), file => Assert.DoesNotContain(value, file + File.ReadAllText(file), StringComparison.Ordinal));

        // A goal at the ceiling that the markers push past it is refused before anything starts.
        var longGoal = new string('a', 2_000 - value.Length - 1) + " " + value;
        var refused = await Assert.ThrowsAsync<ConfigurationException>(() => Explorer.ExploreAsync(Options(
            root,
            model.Model,
            [],
            goal: longGoal,
            config: ExploreHarness.Config(root, "\"secrets\": { \"a-long-secret-name\": \"" + value + "\" }"))));
        Assert.Equal("INVALID_CONFIG", refused.Code);
        Assert.Equal("the goal must be at most 2000 characters with its secret values redacted, got 2009", refused.Message);
    }

    [Fact]
    public async Task Is_blocked_when_the_agent_explores_nothing_and_replaces_a_custom_executor_with_a_notice()
    {
        var model = Model(_ => ExploreModel.Finish("Nothing here."), _ => ExploreModel.Done("passed", "unused"));
        var custom = new TestExecutor("house-brain", (_, _) => Task.FromResult(Passed("never runs")));
        var notices = new List<string>();
        var outcome = await Explorer.ExploreAsync(Options(TempRoot(), model.Model, notices, agent: new AgentOptions { Model = model.Model, Executor = custom }));

        Assert.Equal(RunStatus.Blocked, outcome.Status);
        Assert.Equal(ReportExploreEnded.Finished, outcome.Explore.Ended);
        Assert.Empty(outcome.Explore.Steps);
        Assert.Empty(outcome.Explore.Findings);
        Assert.Equal("AUTOMATION_UNSUPPORTED", outcome.Error!.Code);
        Assert.Equal(["agents.default is a custom executor (\"house-brain\"); explore runs the built-in agent instead"], notices);
    }

    // Upstream also reads the status of each act step in the attempt's timeline and validates the report.
    [Theory]
    [InlineData("blocked", RunStatus.Blocked, 3)]
    [InlineData("passed", RunStatus.Passed, 0)]
    public async Task Reports_the_run_status_when_the_first_charter_ends_and_the_second_was_blocked(string firstStatus, RunStatus expectedStatus, int exitCode)
    {
        var plans = 0;
        var model = Model(
            _ => ++plans switch
            {
                1 => ExploreModel.Step("Catalog", "Explore the catalog"),
                2 => ExploreModel.Step("Account", "Explore the account"),
                _ => ExploreModel.Finish("Account was unavailable; no product issues were confirmed."),
            },
            _ => plans == 1 && firstStatus == "passed"
                ? ExploreModel.Done("passed", "Catalog is reachable")
                : ExploreModel.Done("blocked", "Required service is unavailable", "ENVIRONMENT_UNAVAILABLE"));
        var outcome = await Explorer.ExploreAsync(Options(TempRoot(), model.Model, []));

        Assert.Equal(expectedStatus, outcome.Status);
        Assert.Equal(exitCode, outcome.ExitCode);
        Assert.Equal(
            [firstStatus == "passed" ? ReportExploreStepStatus.Passed : ReportExploreStepStatus.Blocked, ReportExploreStepStatus.Blocked],
            outcome.Explore.Steps.Select(step => step.Status));
        Assert.Empty(outcome.Explore.Findings);
        if (expectedStatus == RunStatus.Blocked)
        {
            var error = Assert.IsType<AgentException>(outcome.Error);
            Assert.Equal("ENVIRONMENT_UNAVAILABLE", error.Code);
            Assert.Contains("Required service is unavailable", error.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Null(outcome.Error);
        }
    }

    // Upstream also explores the first of several configured targets, with a notice that names it. The port's config declares
    // one target. It also checks that a config that does not resolve is rejected, which it does through the run's own config load.
    [Fact]
    public async Task Explores_the_target_opening_its_app_first_and_rejects_a_config_that_does_not_resolve_before_anything_starts()
    {
        var plans = 0;
        var model = Model(
            _ => ++plans == 1 ? ExploreModel.Step("Look", "Look around") : ExploreModel.Finish("Looked around."),
            _ => ExploreModel.Done("passed", "unused"));
        var root = TempRoot();
        var notices = new List<string>();
        var outcome = await Explorer.ExploreAsync(Options(root, model.Model, notices, goal: "Look around", maxSteps: 1));
        Assert.Equal(RunStatus.Passed, outcome.Status);
        Assert.Empty(notices);

        // The app was open when the explorer looked.
        Assert.Contains("button \"Increment\"", model.LoopCalls.First().Messages[0].Content, StringComparison.Ordinal);

        var configPath = Path.Combine(root, "bad.config.json");
        await File.WriteAllTextAsync(configPath, """{ "targets": [{ "name": "web", "platform": "web" }], "reporters": ["json"] }""");
        var invalid = await Assert.ThrowsAsync<ConfigurationException>(() => Explorer.ExploreAsync(new ExploreOptions { Cwd = root, ConfigPath = configPath, Goal = "Look around" }));
        Assert.Equal("INVALID_CONFIG", invalid.Code);
        Assert.Contains("config key \"reporters\" is not supported", invalid.Message, StringComparison.Ordinal);

        var unknown = await Assert.ThrowsAsync<ConfigurationException>(() => Explorer.ExploreAsync(Options(root, model.Model, [], target: "nope")));
        Assert.Equal("UNKNOWN_TARGET", unknown.Code);
        Assert.Equal("unknown target ID \"nope\"; the config declares \"web\"", unknown.Message);
    }

    // Upstream also reads the agent each step records, like `e2e run --agent`.
    [Fact]
    public async Task Runs_as_the_agent_it_names_built_from_it_keeps_the_other_agents_and_rejects_an_unknown_name_before_anything_starts()
    {
        var plans = 0;
        var seen = new List<IReadOnlyList<string>>();
        var model = Model(
            _ => ++plans == 1 ? ExploreModel.Step("Look", "Look around") : ExploreModel.Finish("Seen everything there was."),
            call =>
            {
                seen.Add(call.ToolNames);
                return ExploreModel.Done("passed", "looked");
            });
        var other = new ScriptedModel(_ => ModelResponses.Done("passed", "not this one"));
        var ping = new AgentTool("answers pong", JsonNode.Parse("""{ "type": "object", "properties": {} }""")!.AsObject(), mutates: false, (_, _) => Task.FromResult("pong"));
        var agents = new Dictionary<string, AgentOptions>
        {
            ["default"] = new() { Model = other },
            ["ux"] = new() { Model = model.Model, Tools = new Dictionary<string, AgentTool> { ["ping"] = ping } },
        };
        var notices = new List<string>();
        var outcome = await Explorer.ExploreAsync(Options(TempRoot(), null, notices, goal: "Look around", maxSteps: 1, agents: agents, agentName: "ux"));

        Assert.Equal(RunStatus.Passed, outcome.Status);
        Assert.Equal(["exploring with agent \"ux\""], notices);

        // The ux agent's model drove every step, the planner included, and its tool is in the explorer's vocabulary.
        Assert.Equal(0, other.CallCount);
        Assert.Contains("ping", seen[0]);
        Assert.Contains(ExploreExecutor.FindingToolName, seen[0]);

        var root = TempRoot();
        var unknown = await Assert.ThrowsAsync<ConfigurationException>(() => Explorer.ExploreAsync(Options(root, model.Model, [], agentName: "nope")));
        Assert.Equal("INVALID_CONFIG", unknown.Code);
        Assert.Equal("unknown agent \"nope\"; configured: default", unknown.Message);
    }

    [Fact]
    public async Task Rejects_an_agent_with_no_model_before_anything_starts_naming_it_a_custom_executor_that_brought_none_included()
    {
        var custom = new TestExecutor("math-brain", (_, _) => Task.FromResult(Passed("never runs")));
        var model = Model(_ => ExploreModel.Finish(""), _ => ExploreModel.Done("passed", "unused"));
        var agents = new Dictionary<string, AgentOptions>
        {
            ["default"] = new() { Model = model.Model },
            ["custom"] = new() { Executor = custom },
        };
        var notices = new List<string>();
        var error = await Assert.ThrowsAsync<AgentException>(() => Explorer.ExploreAsync(Options(TempRoot(), null, notices, agents: agents, agentName: "custom")));

        Assert.Equal("MODEL_UNAVAILABLE", error.Code);
        Assert.Contains("explore runs the built-in agent, which requires a model, and agents.custom has none", error.Message, StringComparison.Ordinal);
        Assert.Contains("agents.custom is a custom executor (\"math-brain\"); explore runs the built-in agent instead", notices);
        Assert.Equal(0, model.Model.CallCount);
    }

    [Fact]
    public async Task Lets_the_explorer_sign_in_with_a_configured_credential_through_fill_secret_never_seeing_the_password()
    {
        var planPrompts = new List<string>();
        string? fillResult = null;
        string? typed = null;
        var model = Model(
            instruction =>
            {
                planPrompts.Add(instruction);
                return planPrompts.Count == 1
                    ? ExploreModel.Step("Sign in", "Fill the password for the ada account")
                    : ExploreModel.Finish("The password field takes the credential.");
            },
            call =>
            {
                if (call.Turn == 1)
                {
                    return ModelResponses.Call("fill_secret", new { role = "textbox", name = "Password", secret = "ada.password" });
                }

                fillResult = call.LastToolResult;
                return ExploreModel.Done("passed", "Filled the password");
            });
        var credentials = new Dictionary<string, UserCredential> { ["ada"] = new("ada@example.test", Secret.Create("password", "bookworm")) };
        var outcome = await Explorer.ExploreAsync(Options(
            TempRoot(),
            model.Model,
            [],
            goal: "Sign in and look around",
            maxSteps: 1,
            engine: ExploreHarness.Engine(WorldWithAPassword(value => typed = value)),
            credentials: credentials));

        Assert.Null(outcome.Error);
        Assert.Equal(RunStatus.Passed, outcome.Status);
        Assert.Contains("- ada (username: ada@example.test)", planPrompts[0], StringComparison.Ordinal);
        Assert.Contains("fill_secret", model.LoopCalls.First().Tools.Select(tool => tool.Name));
        Assert.Contains("\"password\":\"<secret:ada.password> (password)\"", model.LoopCalls.First().Messages[0].Content, StringComparison.Ordinal);
        Assert.Contains("ada.password", fillResult, StringComparison.Ordinal);
        Assert.Equal("bookworm", typed);

        // The plaintext reaches neither the planner nor the explorer.
        Assert.DoesNotContain("bookworm", Everything(model.Model.Requests), StringComparison.Ordinal);
    }

    // Upstream reads the notice from the run's events, and its engine is the web engine, which records video. The port's notice sink is
    // ExploreOptions.Notice, and a scripted engine that declares the video capability stands in for the web engine.
    [Fact]
    public async Task Says_to_pass_video_on_when_a_retry_mode_would_record_nothing_explore_running_once()
    {
        var model = Model(_ => ExploreModel.Finish("Nothing here."), _ => ExploreModel.Done("passed", "unused"));
        var notices = new List<string>();
        await Explorer.ExploreAsync(Options(TempRoot(), model.Model, notices, engine: new RecordingEngine(ExploreHarness.Engine()), video: RecordingMode.OnFirstRetry));

        Assert.Equal(["video: 'on-first-retry' records retries only, and explore runs its goal once, so no videos will be recorded; pass --video on"], notices);
    }

    [Fact]
    public async Task Rejects_a_goal_past_the_ceiling_a_budget_out_of_range_and_a_credential_inventory_that_would_not_fit_a_step_before_anything_starts()
    {
        var root = TempRoot();
        var model = Model(_ => ExploreModel.Finish(""), _ => ExploreModel.Done("passed", "unused"));

        // A config that resolves, so only the option can be what rejects.
        var goal = await Assert.ThrowsAsync<ConfigurationException>(() => Explorer.ExploreAsync(Options(root, model.Model, [], goal: new string('x', 2_001))));
        Assert.Equal("INVALID_CONFIG", goal.Code);
        var steps = await Assert.ThrowsAsync<ConfigurationException>(() => Explorer.ExploreAsync(Options(root, model.Model, [], maxSteps: 13)));
        Assert.Equal("INVALID_CONFIG", steps.Code);
        Assert.Equal("maxSteps must be an integer from 1 through 12, got 13", steps.Message);

        var credentials = Enumerable.Range(0, 400).ToDictionary(
            index => "account-" + index,
            index => new UserCredential(new string('u', 200) + "@example.test", Secret.Create("password", "password")));
        var inventory = await Assert.ThrowsAsync<ConfigurationException>(() => Explorer.ExploreAsync(Options(root, model.Model, [], credentials: credentials)));
        Assert.Equal("INVALID_CONFIG", inventory.Code);
        Assert.Contains("400 account(s) serialize to", inventory.Message, StringComparison.Ordinal);
        Assert.Equal(0, model.Model.CallCount);
    }

    /// <summary>An engine that declares it can record video and runs the attempt as <paramref name="inner"/> does: the attempt is the first, and a retry mode records no first attempt.</summary>
    private sealed class RecordingEngine(IEngine inner) : IEngine
    {
        public string Platform => inner.Platform;

        public string Version => inner.Version;

        public EngineCapabilities Capabilities => inner.Capabilities | EngineCapabilities.Video;

        public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) => inner.StartAsync(options, cancellationToken);
    }
}
