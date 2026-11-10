// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using E2E.Tests.AgentAct;

namespace E2E.Tests.Agent;

/// <summary>
/// Judgment-tier tests with scripted models. Upstream runs one project through the runner and reads each result from
/// its report. The port runs each test body in a session.
/// </summary>
public sealed class AgentJudgmentTierTests
{
    private const string Context = "This is the e2e fixture application.";

    [Fact]
    public async Task Spends_one_judgment_while_the_page_it_is_waiting_on_does_not_change()
    {
        // The condition is false and the About page is static, so re-judging could only repeat the same answer.
        var judge = new ScriptedModel(_ => ModelResponses.Done("failed", "no checkout button here"));
        var engine = new ObservationCountingEngine(World());
        var seen = 0;
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/about");
                await Expect.That(ctx.Screen.GetByRole("heading")).ToHaveTextAsync("About");
                seen = engine.Observations;
                await ctx.Agent.WaitForAsync("a checkout button is on the About page", new WaitForOptions { Interval = TimeSpan.FromMilliseconds(100), Timeout = TimeSpan.FromSeconds(3), MaxModelCalls = 4 });
            },
            engine,
            judge: judge);

        Assert.Equal("STEP_TIMEOUT", Assert.IsType<AgentException>(result.Error).Code);
        Assert.Equal(1, judge.CallCount);
        // It kept looking, though: observations cost no model call.
        Assert.True(engine.Observations - seen > 2, "observations while waiting: " + (engine.Observations - seen));
        var step = result.Steps!.Single(candidate => candidate.Api == "agent.waitFor");
        Assert.True(step.Events.Count(item => item.Kind == StepEventKind.Observation) > 2);
    }

    [Fact]
    public async Task Repairs_a_malformed_judgment_with_a_second_model_call()
    {
        // A reasoning model that ran out of output mid-answer: no verdict.
        var judge = new ScriptedModel(request => request.Messages.Count == 1
            ? new ModelResponse { Content = "thinking" }
            : ModelResponses.Judgment("holds", "the Home heading is visible"));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/about");
                await ctx.Agent.AssertAsync("the About heading is visible after a malformed first answer");
            },
            judge: judge);

        Assert.Null(result.Error);
        Assert.Equal(2, judge.CallCount);
        Assert.Equal(2, result.ModelCalls);
        Assert.Contains("Statement: the About heading is visible after a malformed first answer", judge.Requests[1].Messages[0].Content, StringComparison.Ordinal);
        var repair = judge.Requests[1].Messages[^1].Content;
        Assert.Contains("<previous-attempt-rejected>", repair, StringComparison.Ordinal);
        Assert.Contains("previous response: thinking", repair, StringComparison.Ordinal);
        Assert.Contains("validation errors: response is not a JSON object", repair, StringComparison.Ordinal);
        Assert.All(judge.Requests, request => Assert.Empty(request.Tools));
        Assert.All(judge.Requests, request => Assert.Equal("agent-judgment-2", request.SchemaName));
    }

    // Upstream answers in the retired agent-judgment-1 shape on the repair round; the port answers with text that has no verdict at all.
    [Fact]
    public async Task Fails_MODEL_OUTPUT_INVALID_when_the_repair_round_gives_no_verdict_either()
    {
        var judge = new ScriptedModel(_ => new ModelResponse { Content = "thinking" });
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/about");
                await ctx.Agent.AssertAsync("the About heading is visible, answered without a verdict twice");
            },
            judge: judge);

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("MODEL_OUTPUT_INVALID", error.Code);
        Assert.StartsWith("The judge did not answer with an agent-judgment-2 object: ", error.Message, StringComparison.Ordinal);
        // One judgment plus one repair round is the whole budget: no third call, no guessed verdict.
        Assert.Equal(2, judge.CallCount);
        var step = result.Steps!.Single(candidate => candidate.Api == "agent.assert");
        Assert.Matches(@"^b\d+\z", step.ObservationRevision);
    }

    // Upstream also checks the token ceiling and that no temperature is pinned. Those belong to the provider models in the port.
    [Fact]
    public async Task Sends_provider_options_with_the_judgment()
    {
        var judge = new ScriptedModel(_ => ModelResponses.Done("passed", "the observation supports the assertion"));
        var options = new Dictionary<string, JsonElement> { ["fake"] = JsonSerializer.SerializeToElement(new { reasoningEffort = "low" }) };
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/about");
                await ctx.Agent.AssertAsync("the About heading is visible");
            },
            judge: judge,
            providerOptions: options);

        Assert.Null(result.Error);
        Assert.Equal("{\"reasoningEffort\":\"low\"}", judge.Requests[0].ProviderOptions!["fake"].GetRawText());
    }

    [Fact]
    public async Task Fails_the_test_with_ASSERTION_FAILED_on_a_false_judgment()
    {
        var judge = new ScriptedModel(_ => ModelResponses.Done("failed", "the observation shows the Home page, not checkout"));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.AssertAsync("the checkout page is visible");
            },
            judge: judge);

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Contains("Home page, not checkout", error.Message, StringComparison.Ordinal);
    }

    // Upstream also checks the error category, which only its report carries.
    [Fact]
    public async Task Fails_with_ASSERTION_INCONCLUSIVE_and_the_explanation_when_the_screen_shows_too_little_to_judge()
    {
        // A judge that cannot see the evidence must not pass the step: a guess in either direction is how a broken flow stays green.
        var judge = new ScriptedModel(_ => ModelResponses.Done("failed", "no order total or line items are on this screen", "ASSERTION_INCONCLUSIVE"));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.AssertAsync("the order total equals the sum of the line items");
            },
            new ScreenshotEngine(World()),
            judge: judge);

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("ASSERTION_INCONCLUSIVE", error.Code);
        Assert.Contains("no order total", error.Message, StringComparison.Ordinal);
        // The judge read the tree alone on an engine that declares screenshots, but no call of this attempt has received
        // pixels yet: the remedy the docs give is named where the failure is read, as a condition.
        Assert.Contains(Vision.HintUnproven, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Names_vision_outright_once_a_step_of_the_attempt_has_received_pixels()
    {
        var judge = new ScriptedModel(request => request.Messages[0].Content!.Contains("Statement: the Home heading is visible", StringComparison.Ordinal)
            ? ModelResponses.Done("passed", "the Home heading is listed")
            : ModelResponses.Done("failed", "no order total or line items are on this screen", "ASSERTION_INCONCLUSIVE"));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.AssertAsync("the Home heading is visible", new AssertOptions { Vision = VisionMode.On });
                await ctx.Agent.AssertAsync("the order total equals the sum of the line items");
            },
            new ScreenshotEngine(World(), _ => ScreenshotEngine.Png()),
            judge: judge);

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Single(judge.Requests[0].Messages[0].Images!);
        Assert.Equal("ASSERTION_INCONCLUSIVE", error.Code);
        Assert.Contains(Vision.Hint, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("if the engine", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Leaves_the_vision_hint_off_an_inconclusive_judgment_that_already_saw_pixels()
    {
        var judge = new ScriptedModel(_ => ModelResponses.Done("failed", "no order total or line items are on this screen", "ASSERTION_INCONCLUSIVE"));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.AssertAsync("the order total equals the sum of the line items", new AssertOptions { Vision = VisionMode.On });
            },
            new ScreenshotEngine(World(), _ => ScreenshotEngine.Png()),
            judge: judge);

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("ASSERTION_INCONCLUSIVE", error.Code);
        Assert.Contains("no order total", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Vision = VisionMode.On", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Points_a_waitFor_timeout_at_vision_when_its_last_round_was_inconclusive()
    {
        var judge = new ScriptedModel(_ => ModelResponses.Done("failed", "no order total is on the About page", "ASSERTION_INCONCLUSIVE"));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/about");
                await Expect.That(ctx.Screen.GetByRole("heading")).ToHaveTextAsync("About");
                await ctx.Agent.WaitForAsync("the order total on the About page adds up", new WaitForOptions { Interval = TimeSpan.FromMilliseconds(100), Timeout = TimeSpan.FromMilliseconds(1500) });
            },
            new ScreenshotEngine(World()),
            judge: judge);

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("STEP_TIMEOUT", error.Code);
        Assert.Contains("last judgment: no order total is on the About page; " + Vision.HintUnproven, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Routes_every_judgment_to_the_judge_model_never_the_act_model()
    {
        var actor = new ScriptedModel(_ => throw new InvalidOperationException("the act model must not judge"));
        var judge = new ScriptedModel(request =>
        {
            var text = string.Join('\n', request.Messages.Select(message => message.Content));
            return text.Contains("Return data matching", StringComparison.Ordinal)
                ? ModelResponses.Call("extract", new { data = new { counter = "0" } })
                : ModelResponses.Done("passed", "the observation supports the assertion");
        });
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.AssertAsync("the Home heading is visible");
                await ctx.Agent.WaitForAsync("the counter is on screen", new WaitForOptions { Interval = TimeSpan.FromMilliseconds(100) });
                await ctx.Agent.ExtractAsync<CounterReading>("the counter value");
            },
            actor: actor,
            judge: judge);

        Assert.Null(result.Error);
        Assert.Equal(3, judge.CallCount);
        Assert.Equal(0, actor.CallCount);
    }

    [Fact]
    public async Task Shows_the_judge_the_screen_and_the_question_never_the_prior_steps()
    {
        // An assert and then a waitFor. The waitFor's judgment must not carry the assert's statement or explanation:
        // a judge that reads the actor's account of what happened is grading a story, not a screen.
        var judge = new ScriptedModel(_ => ModelResponses.Done("passed", "the observation supports the assertion"));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.AssertAsync("the Home heading is visible");
                await ctx.Agent.WaitForAsync("the Late arrival button exists", new WaitForOptions { Interval = TimeSpan.FromMilliseconds(100) });
            },
            judge: judge);

        Assert.Null(result.Error);
        var polled = judge.Requests[1];
        Assert.Contains("Statement: the Late arrival button exists", polled.Messages[0].Content, StringComparison.Ordinal);
        var everything = polled.System + "\n" + string.Join('\n', polled.Messages.Select(message => message.Content));
        Assert.DoesNotContain("<ledger>", everything, StringComparison.Ordinal);
        Assert.DoesNotContain("the Home heading is visible", everything, StringComparison.Ordinal);
        Assert.DoesNotContain("the observation supports the assertion", everything, StringComparison.Ordinal);
    }

    // Upstream also checks the policy version the system prompt names, which the port does not have.
    [Fact]
    public async Task Puts_application_authored_instructions_after_the_policy_in_a_context_block()
    {
        var judge = new ScriptedModel(_ => ModelResponses.Done("passed", "the observation supports the assertion"));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.AssertAsync("the Home heading is visible");
            },
            judge: judge,
            context: Context);

        Assert.Null(result.Error);
        var system = judge.Requests[0].System;
        var block = "<project-context>\n" + Context + "\n</project-context>";
        Assert.EndsWith(block, system, StringComparison.Ordinal);
        Assert.True(system.Length > block.Length, "the policy comes first");
    }

    [Fact]
    public async Task Sends_the_semantic_tree_with_ids_and_a_secure_field_without_its_value()
    {
        var world = World().Map("/login", page =>
        {
            page.Button("Increment");
            page.Textbox("Password", "hunter2-secret", secure: true);
        });
        var judge = new ScriptedModel(_ => ModelResponses.Done("passed", "the field has a value"));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/login");
                await ctx.Agent.AssertAsync("the password field has a value");
            },
            new ObservationCountingEngine(world),
            judge: judge);

        Assert.Null(result.Error);
        var observation = judge.Requests[0].Messages[0].Content!;
        Assert.Contains("button \"Increment\"", observation, StringComparison.Ordinal);
        Assert.Contains("#e1 button \"Increment\"", observation, StringComparison.Ordinal);
        Assert.Contains("#e2 textbox \"Password\" secure purpose=password", observation, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2-secret", observation, StringComparison.Ordinal);
    }

    /// <summary>.NET-only: the judgment path prints a <c>Screen (route):</c> line before the <c>#id</c> lines (upstream sends the route as the observation's attributes), and the route goes through the redactor.</summary>
    [Fact]
    public async Task Redacts_a_secret_in_the_route_of_the_screen_heading_the_judge_reads()
    {
        var world = World().Map("/reset", page => page.Button("Increment"));
        var judge = new ScriptedModel(_ => ModelResponses.Done("passed", "the reset page is open"));
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(world),
            Judge = judge,
            BaseUrl = "https://app.test",
            TestTitle = "agent judgment route",
            AssertionTimeout = TimeSpan.FromSeconds(2),
            TestTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(5),
        });
        session.Remember([Secret.Create("reset.token", "hunter2-secret", "password")]);
        await session.Context.App.OpenAsync("/reset?token=hunter2-secret");
        await session.Context.Agent.AssertAsync("the reset page is open");
        session.Complete(null);

        var observation = judge.Requests[0].Messages[0].Content!;
        Assert.Contains("Screen (\"/reset?token=<secret:reset.token>\"):\n#", observation, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2-secret", observation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_judgment_reads_the_current_path()
    {
        var judge = new ScriptedModel(request => request.Messages[0].Content!.Contains("Screen (\"/about\"):", StringComparison.Ordinal)
            ? ModelResponses.Done("passed", "the location is /about")
            : ModelResponses.Done("failed", "the judge was not shown the location"));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/about");
                await ctx.Agent.AssertAsync("the URL path is /about");
            },
            judge: judge);

        Assert.Null(result.Error);
    }

    [Fact]
    public async Task A_judgment_keeps_the_origin_of_a_screen_off_the_app()
    {
        // A path on another site would read as the app's own route, so the judge is shown the whole address.
        var engine = new LocationEngine(new DocumentEngine(World()), "https://idp.test/login");
        var judge = new ScriptedModel(request => request.Messages[0].Content!.Contains("Screen (\"https://idp.test/login\"):", StringComparison.Ordinal)
            ? ModelResponses.Done("passed", "the judge read the whole address")
            : ModelResponses.Done("failed", "the origin was lost"));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/about");
                await ctx.Agent.AssertAsync("the screen is at the identity provider's login page");
            },
            engine,
            judge: judge);

        Assert.Null(result.Error);
    }

    [Fact]
    public async Task A_judgment_redacts_a_secret_in_the_address_of_the_screen()
    {
        var engine = new LocationEngine(new DocumentEngine(World()), "https://idp.test/cb?code=hunter2-secret#done");
        var judge = new ScriptedModel(_ => ModelResponses.Done("passed", "the callback page is open"));
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            Judge = judge,
            BaseUrl = "https://app.test",
            TestTitle = "agent judgment address",
            AssertionTimeout = TimeSpan.FromSeconds(2),
            TestTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(5),
        });
        session.Remember([Secret.Create("reset.token", "hunter2-secret", "password")]);
        await session.Context.App.OpenAsync("/about");
        await session.Context.Agent.AssertAsync("the callback page is open");
        session.Complete(null);

        var observation = judge.Requests[0].Messages[0].Content!;
        Assert.Contains("Screen (\"https://idp.test/cb?code=", observation, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2-secret", observation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_judgment_redacts_a_secret_the_address_spells_percent_encoded()
    {
        // The secret holds a space and an @, which the address spells as %20 and %40. The raw value is not in the URL at all.
        var engine = new LocationEngine(new DocumentEngine(World()), "https://idp.test/cb?user=p%40ss%20word#done");
        var judge = new ScriptedModel(_ => ModelResponses.Done("passed", "the callback page is open"));
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            Judge = judge,
            BaseUrl = "https://app.test",
            TestTitle = "agent judgment encoded address",
            AssertionTimeout = TimeSpan.FromSeconds(2),
            TestTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(5),
        });
        session.Remember([Secret.Create("login.password", "p@ss word", "password")]);
        await session.Context.App.OpenAsync("/about");
        await session.Context.Agent.AssertAsync("the callback page is open");
        session.Complete(null);

        var observation = judge.Requests[0].Messages[0].Content!;
        Assert.Contains("Screen (\"https://idp.test/cb?user=", observation, StringComparison.Ordinal);
        Assert.DoesNotContain("p%40ss", observation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("p@ss", observation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Judges_again_when_only_the_URL_changed()
    {
        var engine = new LocationEngine(new DocumentEngine(World().Map("/moves", page => page.Heading("Moves"))), "https://app.test/moves", "https://app.test/moved");
        var judge = new ScriptedModel(request =>
        {
            if (request.Messages[0].Content!.Contains("Screen (\"/moved\"):", StringComparison.Ordinal))
            {
                return ModelResponses.Done("passed", "the URL moved to /moved");
            }

            // The address moves once the first judgment is asked, so only the change can end the wait.
            engine.Moved = true;
            return ModelResponses.Done("failed", "the URL is still /moves");
        });
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/moves");
                await Expect.That(ctx.Screen.GetByRole("heading")).ToHaveTextAsync("Moves");
                await ctx.Agent.WaitForAsync("the URL path is /moved", new WaitForOptions { Interval = TimeSpan.FromMilliseconds(100), Timeout = TimeSpan.FromSeconds(3), MaxModelCalls = 4 });
            },
            engine,
            judge: judge);

        Assert.Null(result.Error);
        Assert.Equal(2, judge.CallCount);
    }

    [Fact]
    public async Task A_vision_only_wait_does_not_judge_again_on_a_URL_it_is_not_shown()
    {
        var engine = new LocationEngine(new ScreenshotEngine(World(), _ => ScreenshotEngine.Png()), "https://app.test/about", "https://app.test/moved");
        var judge = new ScriptedModel(_ =>
        {
            // The address moves after the judgment is asked. A vision-only wait is not shown the address, so the move ends nothing.
            engine.Moved = true;
            return ModelResponses.Done("failed", "the chart has not moved");
        });
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/about");
                await Expect.That(ctx.Screen.GetByRole("heading")).ToHaveTextAsync("About");
                await ctx.Agent.WaitForAsync("the chart moved", new WaitForOptions { Interval = TimeSpan.FromMilliseconds(100), Timeout = TimeSpan.FromMilliseconds(1500), MaxModelCalls = 4, Vision = VisionMode.Only });
            },
            engine,
            judge: judge);

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("STEP_TIMEOUT", error.Code);
        Assert.Equal(1, judge.CallCount);
    }

    [Fact]
    public async Task Partial_leaves_the_location_out_when_the_engine_reports_none()
    {
        // An engine with no location: the judge reads the screen's route in the header, and no <location> is compared.
        var engine = new LocationEngine(new DocumentEngine(World()), null);
        var judge = new ScriptedModel(_ => ModelResponses.Done("passed", "the About page is open"));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/about");
                await ctx.Agent.AssertAsync("the About page is open");
            },
            engine,
            judge: judge);

        Assert.Null(result.Error);
        Assert.Contains("Screen (\"/about\"):", judge.Requests[0].Messages[0].Content!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partial_tells_a_judgment_what_the_location_is_evidence_for()
    {
        // Upstream checks the extraction's sentence too; this port's extract prompt is checked by the extract tests.
        var judge = new ScriptedModel(_ => ModelResponses.Done("passed", "the observation supports the assertion"));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/about");
                await ctx.Agent.AssertAsync("the About heading is visible");
            },
            judge: judge);

        Assert.Null(result.Error);
        Assert.Contains("The screen's location is the address of the screen: it is evidence only for a statement about the address or route, never for or against what the screen shows.", judge.Requests[0].System, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partial_sends_no_location_when_the_tree_is_withheld()
    {
        // Vision Only withholds the tree, so the screen's header, with its location, is not in the request.
        var engine = new ScreenshotEngine(World(), _ => ScreenshotEngine.Png());
        var judge = new ScriptedModel(_ => ModelResponses.Done("passed", "the chart is visible"));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/about");
                await ctx.Agent.AssertAsync("the chart is visible", new AssertOptions { Vision = VisionMode.Only });
            },
            engine,
            judge: judge);

        Assert.Null(result.Error);
        Assert.DoesNotContain("Screen (", judge.Requests[0].Messages[0].Content!, StringComparison.Ordinal);
    }

    private static DocumentWorld World() => CounterPage.World().Map("/about", page => page.Heading("About"));

    private static async Task<CoreTests.Attempt> RunAsync(
        Func<TestContext, Task> body,
        IEngine? engine = null,
        ScriptedModel? actor = null,
        ScriptedModel? judge = null,
        string? context = null,
        IReadOnlyDictionary<string, JsonElement>? providerOptions = null)
    {
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine ?? new DocumentEngine(World()),
            Model = actor,
            Judge = judge,
            AgentContext = context,
            ProviderOptions = providerOptions,
            BaseUrl = "https://app.test",
            TestTitle = "agent judgment tier",
            AssertionTimeout = TimeSpan.FromSeconds(2),
            TestTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(5),
        });
        Exception? error = null;
        try
        {
            await body(session.Context);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        session.Complete(error);
        return new CoreTests.Attempt(error, session.ModelCalls, session.Replayed, session.Missed, Steps: session.StepRecords);
    }

    private sealed class CounterReading
    {
        public required string Counter { get; init; }
    }
}
