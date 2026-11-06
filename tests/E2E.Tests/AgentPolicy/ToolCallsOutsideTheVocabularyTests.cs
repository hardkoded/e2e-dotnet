// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.AgentPolicy;

public sealed class ToolCallsOutsideTheVocabularyTests
{
    // Upstream also reads the step's turns from the run report; the port reads them from the session's step record.
    [Fact]
    public async Task Refuses_a_call_with_a_field_the_schema_does_not_declare_names_the_field_and_runs_nothing_until_the_repaired_call()
    {
        var model = new ScriptedModel(request =>
        {
            var text = string.Join('\n', request.Messages.Select(message => message.Content));
            if (text.Contains("Tapped", StringComparison.Ordinal))
            {
                return ModelResponses.Done("passed", "the counter shows 1");
            }

            return text.Contains("Invalid input for tool tap", StringComparison.Ordinal)
                ? ModelResponses.Tap("button", "Increment")
                : ModelResponses.Call("tap", new { role = "button", name = "Increment", force = true, selector = "#increment" });
        });
        await using var session = await StartAsync(model);
        await session.App.OpenAsync("/");

        var act = await session.Agent.ActAsync("increment the counter once");

        // The refusal is the call's result: it names the tool and the keys, and it is not a policy denial.
        var refusal = CounterPage.ToolResults(model)[0];
        Assert.Contains("Invalid input for tool tap", refusal, StringComparison.Ordinal);
        Assert.Contains("unrecognized_keys", refusal, StringComparison.Ordinal);
        Assert.Contains("\"force\"", refusal, StringComparison.Ordinal);
        Assert.Contains("\"selector\"", refusal, StringComparison.Ordinal);
        Assert.DoesNotContain("POLICY_DENIED", refusal, StringComparison.Ordinal);

        // Only the repaired call reached the app: one action, and its result shows the change.
        Assert.Equal(3, model.CallCount);
        Assert.Matches("changed .*\"Counter\".*1", CounterPage.ToolResults(model)[1]);
        Assert.Equal((1, 3), (act.Actions, act.ModelCalls));

        // The step's turns show the refusal as the model read it.
        var turns = Assert.IsAssignableFrom<IReadOnlyList<StepTurn>>(AgentStep(session).Turns);
        Assert.Contains("[tap] " + refusal.Split('\n')[0], turns[0].Outcome, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Runs_the_valid_call_of_a_batched_turn_refuses_the_decorated_one_and_records_both_in_the_turn()
    {
        var model = new ScriptedModel(request =>
        {
            var text = string.Join('\n', request.Messages.Select(message => message.Content));
            return text.Contains("Tapped", StringComparison.Ordinal)
                ? ModelResponses.Done("passed", "the counter shows 1")
                : CounterPage.Batch(
                    ModelResponses.Tap("button", "Increment"),
                    ModelResponses.Call("tap", new { role = "button", name = "Increment", force = true }));
        });
        await using var session = await StartAsync(model);
        await session.App.OpenAsync("/");

        var act = await session.Agent.ActAsync("increment the counter once");

        // One turn, two results: the plain tap ran and reported the change; the decorated one never did.
        Assert.Equal(2, model.CallCount);
        var results = model.Requests[1].Messages.Where(message => message is { Role: "tool", Name: "tap" }).Select(message => message.Content ?? "").ToList();
        Assert.Equal(2, results.Count);
        var ran = Assert.Single(results, text => text.Contains("Tapped", StringComparison.Ordinal));
        var refused = Assert.Single(results, text => text.Contains("Invalid input for tool tap", StringComparison.Ordinal));
        Assert.Matches("changed .*\"Counter\".*1", ran);
        Assert.Contains("\"force\"", refused, StringComparison.Ordinal);
        Assert.Equal((1, 2), (act.Actions, act.ModelCalls));
        var turn = Assert.IsAssignableFrom<IReadOnlyList<StepTurn>>(AgentStep(session).Turns)[0];
        Assert.Equal(2, turn.Calls.Count);
        Assert.Contains("[tap] Tapped", turn.Outcome, StringComparison.Ordinal);
        Assert.Contains("[tap] " + refused.Split('\n')[0], turn.Outcome, StringComparison.Ordinal);
    }

    // PARTIAL (#1253): upstream also reads the verdict turn from its report, with the note that the code was ignored.
    [Fact]
    public async Task Partial_Drops_the_error_code_a_passed_verdict_carries_and_ends_the_step_on_that_call_with_no_repair_turn()
    {
        var model = new ScriptedModel(request =>
        {
            var text = string.Join('\n', request.Messages.Select(message => message.Content));
            return text.Contains("Tapped", StringComparison.Ordinal)
                ? ModelResponses.Done("passed", "the counter shows 1", "ACTION_FAILED")
                : ModelResponses.Tap("button", "Increment");
        });
        ActResult? act = null;
        var result = await RunAsync(async ctx =>
        {
            await ctx.App.OpenAsync("/");
            act = await ctx.Agent.ActAsync("increment the counter once");
            await Expect.That(ctx.Screen.GetByRole("status")).ToHaveTextAsync("1");
        }, CounterPage.World(), model);

        // One model sends a pass beside ACTION_FAILED about half the time. The code is dropped, never refused.
        Assert.Null(result.Error);
        Assert.Equal(2, model.CallCount);
        Assert.Equal(("the counter shows 1", 2, 1), (act?.Summary, act?.ModelCalls, act?.Actions));
    }

    // PARTIAL (#1253): upstream also checks that the refusal lists the available tools and that the report records the turn. The port's refusal names the tool only.
    [Fact]
    public async Task Partial_Refuses_a_tool_the_step_does_not_offer_and_runs_nothing()
    {
        var model = new ScriptedModel(request =>
        {
            var text = string.Join('\n', request.Messages.Select(message => message.Content));
            if (text.Contains("Tapped", StringComparison.Ordinal))
            {
                return ModelResponses.Done("passed", "the counter shows 1");
            }

            return text.Contains("Unknown tool click", StringComparison.Ordinal)
                ? ModelResponses.Tap("button", "Increment")
                : ModelResponses.Call("click", new { role = "button", name = "Increment" });
        });
        ActResult? act = null;
        var result = await RunAsync(async ctx =>
        {
            await ctx.App.OpenAsync("/");
            act = await ctx.Agent.ActAsync("increment the counter once");
            await Expect.That(ctx.Screen.GetByRole("status")).ToHaveTextAsync("1");
        }, CounterPage.World(), model);

        Assert.Null(result.Error);
        Assert.Equal(3, model.CallCount);
        Assert.Equal(1, act?.Actions);
        Assert.Equal("failed: Unknown tool click.", CounterPage.ToolResults(model)[0]);
    }

    [Fact]
    public async Task Counts_refusals_toward_the_failure_streak_so_a_model_that_never_gets_a_call_past_the_schema_is_stopped()
    {
        var turn = 0;
        var model = new ScriptedModel(request =>
        {
            if (request.Tools.Count == 1 && request.Tools[0].Name == "done")
            {
                return ModelResponses.Done("failed", "could not get a call past the schema");
            }

            // A different undeclared value every turn, so only the failure streak can stop this, never the repeat guard.
            return ModelResponses.Call("tap", new { role = "button", name = "Increment", attempt = ++turn });
        });
        await using var session = await StartAsync(model);
        await session.App.OpenAsync("/");

        var error = await Assert.ThrowsAnyAsync<E2EException>(() => session.Agent.ActAsync("increment the counter once"));

        Assert.Contains("could not get a call past the schema", error.Message, StringComparison.Ordinal);
        // Three refusals warn, five force the verdict; a model this stuck never reaches the turn budget.
        Assert.True(model.CallCount <= 8, "model calls: " + model.CallCount);
        Assert.Contains(model.Requests, request => request.Messages.Any(message => message.Content?.Contains("failed in a row", StringComparison.Ordinal) == true));
        Assert.Equal(["done"], model.Requests[^1].Tools.Select(tool => tool.Name));
    }

    private static StepRecord AgentStep(E2ESession session) => session.StepRecords.Single(step => step.Api == "agent.act");

    private static Task<E2ESession> StartAsync(IAgentModel model) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(CounterPage.World()),
            Model = model,
            BaseUrl = "https://counter.test",
            TestTitle = "vocabulary",
        });
}
