// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using E2E.Tests.AgentActPixelTools;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentAssertExplore;

/// <summary>
/// Upstream's <c>agent-assert-explore.test.ts</c> (integration and unit): opt-in exploration of an assertion by scrolling. The port runs
/// them over its document engine with a generated screenshot (<see cref="PixelsEngine"/>), not a browser, as its other agent tests do.
/// Not ported as written: the cases that pass a non-boolean <c>explore</c> or a <c>null</c> budget, which C# types rule out, and the
/// <c>vision: false</c> case, which C# cannot tell from the default (<see cref="VisionMode.Off"/>). The port's verdict tool is <c>done</c>
/// and takes <c>code</c>, and its screenshot rides a user turn after the tool results.
/// </summary>
public sealed class AgentAssertExploreTests
{
    private const string Statement = "the counter shows zero";

    private sealed record Run(Exception? Error, IReadOnlyList<StepRecord> Steps, PixelsEngine Engine);

    /// <summary>One assertion against the counter page, with caller-supplied options.</summary>
    private static async Task<Run> AssertAsync(
        AssertOptions? options,
        IAgentModel? model = null,
        IStepExecutor? executor = null,
        DocumentWorld? world = null,
        string path = "/",
        int? maxSteps = null,
        int? maxModelCalls = null,
        string? system = null,
        Action<PixelsEngine>? setup = null)
    {
        var engine = new PixelsEngine(new DocumentEngine(world ?? CounterPage.World()));
        setup?.Invoke(engine);
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            Model = model,
            AgentExecutor = executor,
            AgentSystem = system,
            MaxSteps = maxSteps ?? E2EDefaults.MaxSteps,
            MaxModelCalls = maxModelCalls ?? E2EDefaults.MaxModelCalls,
            BaseUrl = "https://billing.test",
            TestTitle = "explores",
            ActionTimeout = TimeSpan.FromMilliseconds(300),
            TestTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(5),
        });
        Exception? error = null;
        try
        {
            await session.Context.App.OpenAsync(path);
            await session.Context.Agent.AssertAsync(Statement, options);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        session.Complete(error);
        return new Run(error, session.StepRecords, engine);
    }

    private static string? CodeOf(Run run) => (run.Error as E2EException)?.Code;

    private static int Images(ModelRequest request) => request.Messages.Sum(message => message.Images?.Count ?? 0);

    private static string LastToolResult(ModelRequest request) => request.Messages.Last(message => message.Role == "tool").Content ?? "";

    [Fact]
    public async Task Routes_built_in_exploring_asserts_through_a_scroll_only_loop_with_pixels_alone()
    {
        var model = new ScriptedModel(request => request.Messages.Any(message => message.Role == "tool")
            ? ModelResponses.Done("passed", "observed across viewports")
            : ModelResponses.Call("scroll", new { direction = "down" }));
        var run = await AssertAsync(new AssertOptions { Explore = true, Vision = VisionMode.Only }, model);

        Assert.Null(run.Error);
        Assert.Equal(2, model.CallCount);
        Assert.Equal(["done", "observe", "screenshot", "scroll", "scroll_to"], model.Requests[0].Tools.Select(tool => tool.Name).Order(StringComparer.Ordinal));
        Assert.True(Images(model.Requests[0]) > 0);
        Assert.DoesNotContain("Increment", model.Requests[0].Messages[0].Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Completed steps", model.Requests[0].Messages[0].Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Increment", LastToolResult(model.Requests[1]), StringComparison.Ordinal);
        var step = run.Steps.Single(each => each.Api == "agent.assert");
        Assert.Equal(1, step.Metrics?.ActionSteps);
        Assert.Null(step.Cache);
    }

    [Theory]
    [InlineData(false, 2, null)]
    [InlineData(false, null, 2)]
    public async Task Partial_Rejects_invalid_exploration_options(bool explore, int? maxSteps, int? maxModelCalls)
    {
        var executor = new TestExecutor("unused", (_, _) => throw new InvalidOperationException("must not run"));
        var run = await AssertAsync(new AssertOptions { Explore = explore, MaxSteps = maxSteps, MaxModelCalls = maxModelCalls }, executor: executor);

        Assert.Equal("INVALID_ARGUMENT", CodeOf(run));
    }

    [Fact]
    public async Task Maps_exhausted_exploration_to_ASSERTION_INCONCLUSIVE()
    {
        var executor = new TestExecutor("exhaust", async (context, token) =>
        {
            await context.Actions.ScrollAsync(null, ScrollDirection.Down, 1, token);
            try
            {
                await context.Actions.ScrollAsync(null, ScrollDirection.Down, 1, token);
            }
            catch (AgentException)
            {
                // A hard stop must override a success verdict.
            }

            return Passed("must not pass");
        });
        var run = await AssertAsync(new AssertOptions { Explore = true, MaxSteps = 1 }, executor: executor);

        Assert.Equal("ASSERTION_INCONCLUSIVE", CodeOf(run));
    }

    [Theory]
    [InlineData("navigate")]
    [InlineData("tool")]
    public async Task Denies_custom_executor_mutations_through(string action)
    {
        var executed = false;
        var executor = new TestExecutor("mutation-probe", async (context, token) =>
        {
            try
            {
                if (action == "navigate")
                {
                    await context.Actions.NavigateAsync("/checkout", token);
                }
                else
                {
                    await context.Budgets.RunToolAsync("write", true, () =>
                    {
                        executed = true;
                        return Task.FromResult(0);
                    });
                }
            }
            catch (AgentException)
            {
                // A caught policy failure must still fail the step.
            }

            return Passed("must not pass");
        });
        var run = await AssertAsync(new AssertOptions(), executor: executor);

        Assert.Equal("POLICY_DENIED", CodeOf(run));
        Assert.False(executed);
    }

    [Fact]
    public async Task Treats_a_built_in_loop_without_a_verdict_at_its_model_call_ceiling_as_inconclusive()
    {
        var model = new ScriptedModel(_ => ModelResponses.Call("observe", new { }));
        var run = await AssertAsync(new AssertOptions { Explore = true, MaxModelCalls = 1 }, model);

        Assert.Equal("ASSERTION_INCONCLUSIVE", CodeOf(run));
    }

    [Fact]
    public async Task Caps_the_default_exploration_budgets_at_configured_limits()
    {
        var budgets = new List<int>();
        var executor = new TestExecutor("budget-probe", (context, _) =>
        {
            budgets = [context.Budgets.MaxActions, context.Budgets.MaxModelCalls];
            return Task.FromResult(Passed("observed"));
        });
        var run = await AssertAsync(new AssertOptions { Explore = true }, executor: executor, maxSteps: 2, maxModelCalls: 3);

        Assert.Null(run.Error);
        Assert.Equal([2, 3], budgets);
    }

    [Fact]
    public async Task Allows_scrolling_a_listed_node_into_view()
    {
        var executor = new TestExecutor("scroll-to-probe", async (context, token) =>
        {
            var observation = await context.ObserveAsync(null, token);
            await context.Actions.ScrollToAsync(TargetFor(observation.Text, "button \"Increment\""), token);
            return Passed("node reached");
        });
        var run = await AssertAsync(new AssertOptions { Explore = true }, executor: executor);

        Assert.Null(run.Error);
    }

    [Fact]
    public async Task Retains_every_viewport_image_for_assertions_spanning_more_than_four_screenshots()
    {
        var turn = 0;
        var model = new ScriptedModel(_ => turn++ < 5
            ? ModelResponses.Call("scroll", new { direction = turn % 2 == 1 ? "down" : "up" })
            : ModelResponses.Done("passed", "combined all viewport evidence"));
        var run = await AssertAsync(new AssertOptions { Explore = true, Vision = VisionMode.Only, MaxModelCalls = 8 }, model);

        Assert.Null(run.Error);
        Assert.Equal(6, model.CallCount);
        Assert.True(Images(model.Requests[^1]) >= 6);
    }

    [Fact]
    public async Task Fails_inconclusively_instead_of_dropping_evidence_after_context_overflow()
    {
        var model = new ScriptedModel(request =>
        {
            if (request.Messages.All(message => message.Role != "tool"))
            {
                return ModelResponses.Call("scroll", new { direction = "down" });
            }

            Assert.Equal(2, Images(request));
            throw new AgentException("CONTEXT_OVERFLOW", "The model request exceeded the context window: context_length_exceeded", blocked: true);
        });
        var run = await AssertAsync(new AssertOptions { Explore = true, Vision = VisionMode.Only }, model);

        Assert.Equal("ASSERTION_INCONCLUSIVE", CodeOf(run));
        Assert.Equal(2, model.CallCount);
    }

    [Fact]
    public async Task Partial_Keeps_a_noninteractive_assertion_tree_only_by_default()
    {
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "read semantic evidence"));
        var world = new DocumentWorld().Map("/canvas-bare", page => page.Status("Chart"));
        var run = await AssertAsync(new AssertOptions { Explore = true }, model, world: world, path: "/canvas-bare");

        Assert.Null(run.Error);
        Assert.Equal(0, Images(model.Requests[0]));
        Assert.DoesNotContain(model.Requests[0].Tools, tool => tool.Name == "screenshot");
    }

    [Theory]
    [InlineData(VisionMode.Off, false)]
    [InlineData(VisionMode.On, true)]
    [InlineData(VisionMode.Only, true)]
    public async Task Lets_a_vision_capable_custom_executor_choose_tree_only_or_pixel_evidence_with(VisionMode vision, bool explore)
    {
        var text = "";
        ExecutorPixels? treeOnlyPixels = null;
        ExecutorPixels? requestedPixels = null;
        var executor = new TestExecutor("custom-vision", async (context, token) =>
        {
            var observation = await context.ObserveAsync(new ExecutorObserveOptions { Pixels = false }, token);
            text = observation.Text;
            treeOnlyPixels = observation.Pixels;
            requestedPixels = (await context.ObserveAsync(new ExecutorObserveOptions { Pixels = true }, token)).Pixels;
            return Passed("read the supplied observation");
        })
        {
            Vision = true,
        };
        var run = await AssertAsync(new AssertOptions { Explore = explore, Vision = vision }, executor: executor);

        Assert.Null(run.Error);
        Assert.Contains("Increment", text, StringComparison.Ordinal);
        Assert.Null(treeOnlyPixels);
        Assert.NotNull(requestedPixels);
    }

    [Theory]
    [InlineData("budget")]
    [InlineData("timeout")]
    public async Task Maps_a_thrown_assertion_hard_stop_to_inconclusive(string stop)
    {
        var executor = new TestExecutor("throwing-hard-stop", async (context, token) =>
        {
            if (stop == "budget")
            {
                await context.Actions.ScrollAsync(null, ScrollDirection.Down, 1, token);
                await context.Actions.ScrollAsync(null, ScrollDirection.Down, 1, token);
            }
            else
            {
                await Task.Delay(Timeout.Infinite, token);
            }

            return Passed("must not pass");
        });
        var run = await AssertAsync(new AssertOptions { Explore = true, MaxSteps = 1, Timeout = stop == "timeout" ? TimeSpan.FromMilliseconds(100) : null }, executor: executor);

        Assert.Equal("ASSERTION_INCONCLUSIVE", CodeOf(run));
    }

    [Fact]
    public async Task Maps_vouched_exhaustion_without_a_recorded_hard_stop_to_inconclusive()
    {
        var executor = new TestExecutor("self-accounted-exhaustion", (context, _) =>
        {
            context.Budgets.RecordModelCall(null);
            throw new AgentException("STEP_BUDGET_EXHAUSTED", "the model-call budget was used");
        });
        var run = await AssertAsync(new AssertOptions { Explore = true, MaxModelCalls = 1 }, executor: executor);

        Assert.Equal("ASSERTION_INCONCLUSIVE", CodeOf(run));
    }

    [Fact]
    public async Task Appends_the_agents_entrys_system_to_the_exploring_assertions_rules()
    {
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "Ready is shown"));
        var run = await AssertAsync(new AssertOptions { Explore = true }, model, system: "Plans are called tiers.");

        Assert.Null(run.Error);
        Assert.Contains("You are judging exactly one assertion", model.Requests[0].System, StringComparison.Ordinal);
        Assert.Contains("Plans are called tiers.", model.Requests[0].System, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Drops_an_inconclusive_code_an_act_verdict_sends_so_the_act_fails_with_its_own_code()
    {
        var model = new ScriptedModel(_ => ModelResponses.Done("failed", "no Save button", "ASSERTION_INCONCLUSIVE"));
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new PixelsEngine(new DocumentEngine(CounterPage.World())),
            Model = model,
            BaseUrl = "https://billing.test",
            TestTitle = "explores",
        });
        await session.Context.App.OpenAsync("/");
        var error = await Assert.ThrowsAsync<AgentException>(() => session.Context.Agent.ActAsync("save the form"));

        Assert.Equal("ACTION_FAILED", error.Code);
        Assert.Equal(1, model.CallCount);
    }

    [Fact]
    public async Task Refuses_fallback_pixels_when_a_built_in_exploring_assertion_requests_semantic_evidence()
    {
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "must not see pixels"));
        var run = await AssertAsync(new AssertOptions { Explore = true, Screenshot = false }, model, setup: engine => (engine.TreeUnavailable, engine.IgnoresPixelFallback) = (true, true));

        Assert.Equal("POLICY_DENIED", CodeOf(run));
        Assert.Equal(0, model.CallCount);
    }

    [Theory]
    [InlineData("opening", "POLICY_DENIED", 0)]
    [InlineData("later", "ASSERTION_INCONCLUSIVE", 2)]
    public async Task Handles_a_vision_only_exploring_assertion_whose_look_has_no_pixels(string look, string code, int modelCalls)
    {
        var results = new List<string>();
        var model = new ScriptedModel(request =>
        {
            if (request.Messages.All(message => message.Role != "tool"))
            {
                return ModelResponses.Call("observe", new { });
            }

            results.Add(LastToolResult(request));
            return ModelResponses.Done("failed", "no pixels to judge", "ASSERTION_INCONCLUSIVE");
        });
        // The opening look has no pixels, or the engine loses them after it, as upstream's fake engine does after the model's first turn.
        var run = await AssertAsync(
            new AssertOptions { Explore = true, Vision = VisionMode.Only },
            model,
            setup: engine => engine.ShotsBeforeLoss = look == "opening" ? 0 : 1);

        Assert.Equal(code, CodeOf(run));
        Assert.Equal(modelCalls, model.CallCount);
        if (look == "later")
        {
            // A later look goes back to the model as the tool's error, without the tree.
            Assert.Contains("vision: 'only' requires available pixel evidence", results.Single(), StringComparison.Ordinal);
            Assert.DoesNotContain("Counter", results.Single(), StringComparison.Ordinal);
        }
    }
}
