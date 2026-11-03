// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentGrammarTools;

// The port's act tools do not close their schemas and have no point verbs, so upstream's closed-schema, keyboard-only,
// point verb, and provider-schema cases have no full port; the vocabulary, scroll_to, and verdict cases are partial. The act loop reads the first done of a turn; upstream's tool result text
// ("the errorCode you sent ... was ignored", "The step already concluded.") is not sent back, because the step ends.
public sealed class TheGrammarToolsHaveClosedSchemasTests
{
    // Partial of upstream's "offers the whole vocabulary over the full grammar, and every tool refuses a field it does not declare":
    // the port has no point verbs and its schemas are not closed, so only the vocabulary is checked.
    [Fact]
    public void Offers_the_node_verbs_and_the_scroll_and_history_tools_their_capabilities_unlock()
    {
        var full = AgentTools.GrammarToolDefinitions(EngineCapabilities.Scroll | EngineCapabilities.History);
        Assert.Equal(
            ["back", "check", "clear", "double_tap", "fill", "fill_secret", "navigate", "observe", "press", "press_at", "screenshot", "scroll", "scroll_to", "select", "select_at", "tap", "tap_at", "type_at", "uncheck"],
            full.Select(tool => tool.Name).Order(StringComparer.Ordinal));

        var bare = AgentTools.GrammarToolDefinitions(EngineCapabilities.None);
        Assert.Equal(
            ["check", "clear", "double_tap", "fill", "fill_secret", "navigate", "observe", "press", "press_at", "screenshot", "select", "select_at", "tap", "tap_at", "type_at", "uncheck"],
            bare.Select(tool => tool.Name).Order(StringComparer.Ordinal));
    }

    // Part of upstream's "offers the whole vocabulary over the full grammar": the pointer verbs follow the action kinds the engine declares,
    // where upstream's follow the verbs its target lists, and drag names its drop target in a second argument.
    [Fact]
    public void Offers_each_pointer_verb_only_for_the_action_kind_the_engine_declares()
    {
        string[] pointer = ["drag", "hover", "long_press", "right_click"];
        static IEnumerable<string> Offered(IReadOnlyCollection<string>? kinds) =>
            AgentTools.GrammarToolDefinitions(EngineCapabilities.None, null, kinds).Select(tool => tool.Name).Where(name => name is "drag" or "hover" or "long_press" or "right_click").Order(StringComparer.Ordinal);

        Assert.Equal(pointer, Offered(EngineActionKinds.Locator));
        Assert.Equal(["drag", "hover", "long_press"], Offered(["tap", "doubleTap", "longPress", "hover", "dragTo"]));
        Assert.Equal(["right_click"], Offered(["secondaryTap"]));
        Assert.Empty(Offered([]));
        Assert.Empty(Offered(null));

        var drag = AgentTools.GrammarToolDefinitions(EngineCapabilities.None, null, EngineActionKinds.Locator).Single(tool => tool.Name == "drag");
        Assert.Equal(["to"], drag.Parameters.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.True(drag.Parameters.GetProperty("properties").GetProperty("to").GetProperty("properties").TryGetProperty("ref", out _));
        Assert.True(AgentTools.IsReserved("drag"));
    }

    // Partial of upstream's "closes the keyboard-only shapes too: type and press without a target, scroll without one": the port's tool is
    // fill, not type, its schemas are not closed, and it always offers the node and point verbs beside the keyboard ones, so this checks the
    // shapes the keyboard changes: fill takes replace and needs no target, type_at takes replace, and without a keyboard neither does.
    [Fact]
    public void Partial_Offers_fill_without_a_target_and_a_replace_flag_only_when_the_engine_has_a_keyboard()
    {
        var keyboard = AgentTools.CreateGrammarTools(EngineCapabilities.Keyboard | EngineCapabilities.Scroll);
        var fill = keyboard.Single(tool => tool.Name == "fill").Parameters;
        Assert.True(fill.GetProperty("properties").TryGetProperty("replace", out _));
        Assert.Equal(["value"], fill.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.True(keyboard.Single(tool => tool.Name == "type_at").Parameters.GetProperty("properties").TryGetProperty("replace", out _));
        Assert.Contains("keyboard", keyboard.Single(tool => tool.Name == "press_at").Description, StringComparison.Ordinal);

        var bare = AgentTools.CreateGrammarTools(EngineCapabilities.Scroll);
        Assert.False(bare.Single(tool => tool.Name == "fill").Parameters.GetProperty("properties").TryGetProperty("replace", out _));
        Assert.False(bare.Single(tool => tool.Name == "type_at").Parameters.GetProperty("properties").TryGetProperty("replace", out _));
    }

    // Partial of upstream's scroll_to "it": the port checks the text ceiling and the declared fields, not the closed shape.
    [Fact]
    public void Declares_the_text_of_scroll_to_with_a_ceiling_beside_a_target_and_a_direction()
    {
        var scrollTo = AgentTools.GrammarToolDefinitions(EngineCapabilities.Scroll).Single(tool => tool.Name == "scroll_to");
        var properties = scrollTo.Parameters.GetProperty("properties");
        Assert.Equal(AgentTools.MaxScrollToText, properties.GetProperty("text").GetProperty("maxLength").GetInt32());
        Assert.True(properties.TryGetProperty("direction", out _));
        Assert.True(properties.TryGetProperty("ref", out _));
    }

    // Partial of upstream's "accepts a passed verdict beside an error code": the port sends no tool result text that says what it dropped.
    [Fact]
    public void Records_a_passed_verdict_beside_an_error_code_without_the_code()
    {
        var verdict = AgentTools.CreateVerdictTool();

        var passed = verdict.Decide(Done("passed", "ACTION_FAILED"));
        Assert.True(passed.Done);
        Assert.Equal("passed", passed.Status);
        Assert.Equal("done", passed.Summary);
        Assert.Null(passed.Code);

        var failed = verdict.Decide(Done("failed", "ACTION_FAILED"));
        Assert.Equal("failed", failed.Status);
        Assert.Equal("ACTION_FAILED", failed.Code);

        var blocked = verdict.Decide(Done("blocked", "ENVIRONMENT_UNAVAILABLE"));
        Assert.Equal("blocked", blocked.Status);
        Assert.Equal("ENVIRONMENT_UNAVAILABLE", blocked.Code);
    }

    [Fact]
    public async Task Ends_the_step_with_the_code_of_a_verdict_beside_a_passed_one_dropped()
    {
        var passed = await RunAsync(ModelResponses.Done("passed", "done", "ACTION_FAILED"));
        Assert.Null(passed.Error);
        Assert.Equal("done", passed.Summary);

        var failed = await RunAsync(ModelResponses.Done("failed", "done", "ACTION_FAILED"));
        var error = Assert.IsType<AgentException>(failed.Error);
        Assert.Equal("ACTION_FAILED", error.Code);
        Assert.Equal("agent.act failed: done", error.Message);

        var blocked = await RunAsync(ModelResponses.Done("blocked", "done", "ENVIRONMENT_UNAVAILABLE"));
        var blockedError = Assert.IsType<AgentException>(blocked.Error);
        Assert.Equal("ENVIRONMENT_UNAVAILABLE", blockedError.Code);
        Assert.True(blockedError.Blocked);
    }

    // Partial of upstream's "keeps the first verdict: a later complete_step in the same turn cannot turn a failure into a pass":
    // the port's verdict tool keeps no state and sends no result text ("Step concluded.", "The step already concluded."), so only
    // the outcome of the step is checked.
    [Fact]
    public async Task Partial_Keeps_the_first_verdict_a_later_done_in_the_same_turn_cannot_turn_a_failure_into_a_pass()
    {
        var turn = new ModelResponse
        {
            ToolCalls =
            [
                ModelResponses.Done("failed", "total is wrong", "ASSERTION_FAILED").ToolCalls[0],
                ModelResponses.Done("passed", "looks fine").ToolCalls[0],
            ],
        };

        var result = await RunAsync(turn);

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Equal("agent.act failed: total is wrong", error.Message);
    }

    // Partial of upstream's "refuses a blocked verdict without a blocking error code and leaves the step open for a real one": the
    // port's verdict tool keeps no concluded() state to read and words its refusal by its own code names, so the open step is
    // checked through the calls the loop makes next.
    [Fact]
    public async Task Partial_Refuses_a_blocked_verdict_without_a_blocking_error_code_and_leaves_the_step_open_for_a_real_one()
    {
        var model = new ScriptedModel(request => request.Messages.Count(message => message.Role == "tool") switch
        {
            0 => ModelResponses.Done("blocked", "could not tell"),
            1 => ModelResponses.Done("blocked", "the total is wrong", "ASSERTION_FAILED"),
            2 => ModelResponses.Done("blocked", "too slow", "STEP_TIMEOUT"),
            _ => ModelResponses.Done("blocked", "no login", "AUTH_CREDENTIAL_UNAVAILABLE"),
        });

        var result = await RunAsync(model);

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("AUTH_CREDENTIAL_UNAVAILABLE", error.Code);
        Assert.Equal("agent.act blocked: no login", error.Message);
        Assert.True(error.Blocked);
        Assert.Equal(4, model.CallCount);
        var rejections = model.Requests[3].Messages.Where(message => message.Role == "tool").Select(message => message.Content).Take(3).ToArray();
        Assert.Equal(3, rejections.Length);
        Assert.All(rejections, content => Assert.StartsWith("failed: a blocked verdict requires a code naming what blocked you", content, StringComparison.Ordinal));
    }

    private static ModelToolCall Done(string status, string code) => ModelResponses.Done(status, "done", code).ToolCalls[0];

    private static Task<Outcome> RunAsync(ModelResponse response) => RunAsync(new ScriptedModel(_ => response));

    private static async Task<Outcome> RunAsync(ScriptedModel model)
    {
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(BillingWorld.Create()),
            Model = model,
            BaseUrl = "https://billing.test",
            TestTitle = "verdicts",
        });
        await session.App.OpenAsync("/settings/billing");
        try
        {
            var result = await session.Agent.ActAsync("upgrade");
            session.Complete();
            return new Outcome(null, result.Summary);
        }
        catch (AgentException error)
        {
            session.Complete(error);
            return new Outcome(error, null);
        }
    }

    private sealed record Outcome(AgentException? Error, string? Summary);
}
