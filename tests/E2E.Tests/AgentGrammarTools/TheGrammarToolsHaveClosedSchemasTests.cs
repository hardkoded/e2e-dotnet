// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Tests.AgentGrammarTools;

// Upstream validates a call against the zod schema of a tool (safeParse). The port's schema is the JSON Schema a provider receives, so these
// tests validate against it (JsonSchemaCheck) and read the refusal text of the closed schema through AgentTools.SchemaTool. The port's
// vocabulary differs from upstream's in names only (type is fill, type_secret is fill_secret, target is role, name, or ref, complete_step
// is done, errorCode is code, and check takes no checked flag but has uncheck). The tests keep upstream's `it` text.
public sealed class TheGrammarToolsHaveClosedSchemasTests
{
    /// <summary>An input each tool accepts; a tool added to the grammar without one fails the vocabulary check.</summary>
    private static readonly Dictionary<string, string> Accepted = new(StringComparer.Ordinal)
    {
        ["observe"] = "{}",
        ["tap"] = """{ "ref": "n1", "modifiers": ["Shift"] }""",
        ["double_tap"] = """{ "ref": "n1" }""",
        ["long_press"] = """{ "ref": "n1" }""",
        ["right_click"] = """{ "ref": "n1" }""",
        ["hover"] = """{ "ref": "n1" }""",
        ["fill"] = """{ "ref": "n1", "value": "ada" }""",
        ["fill_secret"] = """{ "ref": "n1", "secret": "admin" }""",
        ["press"] = """{ "ref": "n1", "key": "Shift+ArrowLeft", "times": 8 }""",
        ["select"] = """{ "ref": "n1", "value": "Pro" }""",
        ["check"] = """{ "ref": "n1" }""",
        ["uncheck"] = """{ "ref": "n1" }""",
        ["clear"] = """{ "ref": "n1" }""",
        ["scroll"] = """{ "direction": "down", "times": 2 }""",
        ["scroll_to"] = """{ "ref": "n1" }""",
        ["drag"] = """{ "ref": "n1", "to": { "ref": "n2" } }""",
        ["upload"] = """{ "ref": "n1", "files": ["fixtures/a.txt"] }""",
        ["navigate"] = """{ "url": "/about" }""",
        ["back"] = "{}",
        ["resize_viewport"] = """{ "width": 390, "height": 844 }""",
        ["screenshot"] = "{}",
        ["tap_at"] = """{ "x": 10, "y": 20 }""",
        ["hover_at"] = """{ "x": 10, "y": 20 }""",
        ["drag_at"] = """{ "x": 10, "y": 20, "to_x": 30, "to_y": 40 }""",
        ["type_at"] = """{ "x": 10, "y": 20, "value": "ada", "replace": true }""",
        ["press_at"] = """{ "x": 10, "y": 20, "key": "Enter" }""",
        ["select_at"] = """{ "x": 10, "y": 20, "value": "Pro" }""",
        ["dismiss_keyboard"] = "{}",
    };

    private const EngineCapabilities Everything = EngineCapabilities.Keyboard | EngineCapabilities.Scroll | EngineCapabilities.History | EngineCapabilities.Resize
        | EngineCapabilities.Upload | EngineCapabilities.TapModifiers;

    private static IReadOnlyList<ModelTool> FullGrammar() =>
        AgentTools.GrammarToolDefinitions(Everything, null, EngineActionKinds.Locator, ["tap", "hover", "dragTo"], dismissKeyboard: true);

    private static JsonSchemaCheck SchemaOf(ModelTool tool) => JsonSchemaCheck.FromText(tool.Parameters.GetRawText());

    private static JsonNode Input(string json) => JsonNode.Parse(json)!;

    private static ModelToolCall Call(string name, JsonNode arguments) =>
        new() { Id = "call", Name = name, Arguments = JsonSerializer.SerializeToElement(arguments) };

    /// <summary>The accepted input passes; the same input with two undeclared fields fails on exactly those keys.</summary>
    private static void ExpectClosed(ModelTool tool, string accepted)
    {
        var schema = SchemaOf(tool);
        Assert.True(schema.IsValid(Input(accepted)), tool.Name + " accepts " + accepted);
        var decorated = Input(accepted).AsObject();
        decorated["force"] = true;
        decorated["selector"] = "#x";
        Assert.False(schema.IsValid(decorated), tool.Name + " refuses undeclared fields");
        var refusal = AgentTools.SchemaTool(tool, Call(tool.Name, decorated));
        Assert.NotNull(refusal);
        var issues = JsonNode.Parse(refusal[(refusal.IndexOf("Error message: ", StringComparison.Ordinal) + "Error message: ".Length)..])!.AsArray();
        Assert.Equal(["unrecognized_keys"], issues.Select(issue => (string?)issue!["code"]));
        Assert.Equal("Unrecognized keys: \"force\", \"selector\"", (string?)issues[0]!["message"]);
        Assert.Null(AgentTools.SchemaTool(tool, Call(tool.Name, Input(accepted))));
    }

    [Fact]
    public void Offers_the_whole_vocabulary_over_the_full_grammar_and_every_tool_refuses_a_field_it_does_not_declare()
    {
        var tools = FullGrammar();
        Assert.Equal(Accepted.Keys.Order(StringComparer.Ordinal), tools.Select(tool => tool.Name).Order(StringComparer.Ordinal));
        foreach (var tool in tools)
        {
            ExpectClosed(tool, Accepted[tool.Name]);
        }
    }

    private static IReadOnlyList<ModelTool> OverVerbs(params ExecutorVerb[] verbs) => AgentTools.GrammarToolDefinitions(new HashSet<ExecutorVerb>(verbs));

    [Fact]
    public void Scroll_to_takes_a_listed_node_a_text_to_reach_or_a_text_with_the_list_to_page_never_nothing_a_device_gets_the_text_form_alone()
    {
        var scrollTo = FullGrammar().Single(tool => tool.Name == "scroll_to");
        var full = SchemaOf(scrollTo);
        Assert.True(full.IsValid(Input("""{ "text": "Row 4322", "direction": "down", "ref": "n6" }""")));
        Assert.True(full.IsValid(Input("""{ "text": "Row 4322" }""")));
        Assert.False(full.IsValid(Input($$"""{ "text": "{{new string('x', AgentTools.MaxScrollToText + 1)}}" }""")));
        Assert.False(full.IsValid(Input("""{ "text": "Row 4322", "list": "n6" }""")));
        ExpectClosed(scrollTo, """{ "text": "Row 4322" }""");

        var device = OverVerbs(ExecutorVerb.Tap, ExecutorVerb.Scroll, ExecutorVerb.ScrollUntil);
        var scrollToByText = device.Single(tool => tool.Name == "scroll_to");
        var byText = SchemaOf(scrollToByText);
        Assert.True(byText.IsValid(Input("""{ "text": "Row 0512" }""")));
        Assert.True(byText.IsValid(Input("""{ "text": "Row 0512", "ref": "n3" }""")));
        Assert.False(byText.IsValid(Input("""{ "ref": "n1" }""")));
        ExpectClosed(scrollToByText, """{ "text": "Row 0512", "direction": "down" }""");

        var scrollToByNode = OverVerbs(ExecutorVerb.Tap, ExecutorVerb.ScrollTo).Single(tool => tool.Name == "scroll_to");
        var byNode = SchemaOf(scrollToByNode);
        Assert.False(byNode.IsValid(Input("""{ "text": "Row 0512" }""")));
        ExpectClosed(scrollToByNode, """{ "ref": "n1" }""");
    }

    [Fact]
    public void Closes_the_keyboard_only_shapes_too_type_and_press_without_a_target_scroll_without_one()
    {
        var tools = OverVerbs(ExecutorVerb.TypeText, ExecutorVerb.PressKey, ExecutorVerb.Scroll);
        Assert.Equal(
            ["fill", "observe", "press", "press_at", "screenshot", "scroll", "type_at"],
            tools.Select(tool => tool.Name).Order(StringComparer.Ordinal));
        var fill = tools.Single(tool => tool.Name == "fill");
        ExpectClosed(fill, """{ "value": "ada", "replace": true }""");
        ExpectClosed(tools.Single(tool => tool.Name == "press"), """{ "key": "Enter" }""");
        ExpectClosed(tools.Single(tool => tool.Name == "scroll"), """{ "direction": "down" }""");
        // Without the id-addressed verb a target is not a field of fill at all, and neither is it of press or scroll.
        Assert.False(SchemaOf(fill).IsValid(Input("""{ "ref": "n1", "value": "ada" }""")));
        Assert.False(SchemaOf(tools.Single(tool => tool.Name == "press")).IsValid(Input("""{ "ref": "n1", "key": "Enter" }""")));
        Assert.False(SchemaOf(tools.Single(tool => tool.Name == "scroll")).IsValid(Input("""{ "ref": "n1", "direction": "down" }""")));
    }

    [Fact]
    public void Offers_resize_viewport_only_with_the_resize_verb_in_whole_pixels_within_the_bounds()
    {
        Assert.DoesNotContain(AgentTools.GrammarToolDefinitions(EngineCapabilities.History), tool => tool.Name == "resize_viewport");
        var resize = SchemaOf(AgentTools.GrammarToolDefinitions(EngineCapabilities.Resize).Single(tool => tool.Name == "resize_viewport"));
        Assert.True(resize.IsValid(Input("""{ "width": 1440, "height": 900 }""")));
        Assert.False(resize.IsValid(Input("""{ "width": 390.5, "height": 844 }""")));
        Assert.False(resize.IsValid(Input("""{ "width": 100, "height": 844 }""")));
        Assert.False(resize.IsValid(Input("""{ "width": 390, "height": 5000 }""")));
        Assert.False(resize.IsValid(Input("""{ "width": 390 }""")));
    }

    [Fact]
    public void Closes_complete_step()
    {
        ExpectClosed(AgentTools.CreateVerdictTool().Tool, """{ "status": "passed", "summary": "done" }""");
    }

    [Fact]
    public void Accepts_a_passed_verdict_beside_an_error_code_the_body_records_the_pass_without_the_code_and_says_what_it_dropped()
    {
        // One model attaches ACTION_FAILED to about half of its passes; a schema refusal had it resend the identical call until the turn budget ran out.
        var passed = AgentTools.CreateVerdictTool();
        var schema = SchemaOf(passed.Tool);
        Assert.True(schema.IsValid(Input("""{ "status": "passed", "summary": "done", "code": "ACTION_FAILED" }""")));
        Assert.Equal(
            "Step concluded as passed; the code ACTION_FAILED you sent does not apply to a passed verdict and was ignored.",
            passed.Execute(Call("done", Input("""{ "status": "passed", "summary": "done", "code": "ACTION_FAILED" }"""))).Content);
        Assert.Equal(new StepVerdict(StepVerdictStatus.Passed, "done"), passed.Verdict);
        var failed = AgentTools.CreateVerdictTool();
        Assert.Equal("Step concluded.", failed.Execute(Call("done", Input("""{ "status": "failed", "summary": "done", "code": "ACTION_FAILED" }"""))).Content);
        Assert.Equal(new StepVerdict(StepVerdictStatus.Failed, "done", "ACTION_FAILED"), failed.Verdict);
        Assert.True(schema.IsValid(Input("""{ "status": "blocked", "summary": "done", "code": "ENVIRONMENT_UNAVAILABLE" }""")));
    }

    [Fact]
    public void Keeps_the_first_verdict_a_later_complete_step_in_the_same_turn_cannot_turn_a_failure_into_a_pass()
    {
        var conclusion = AgentTools.CreateVerdictTool();
        Assert.Equal("Step concluded.", conclusion.Execute(Call("done", Input("""{ "status": "failed", "summary": "total is wrong", "code": "ASSERTION_FAILED" }"""))).Content);
        Assert.Equal("The step already concluded.", conclusion.Execute(Call("done", Input("""{ "status": "passed", "summary": "looks fine" }"""))).Content);
        Assert.Equal(new StepVerdict(StepVerdictStatus.Failed, "total is wrong", "ASSERTION_FAILED"), conclusion.Verdict);
    }

    // The port words the refusal "failed: a blocked verdict requires a code naming what blocked you", where upstream's is "Rejected: a blocked
    // verdict requires errorCode": the port's field is code, and its tool results lead with "failed: " when the call did not run.
    [Fact]
    public void Refuses_a_blocked_verdict_without_a_blocking_error_code_and_leaves_the_step_open_for_a_real_one()
    {
        var conclusion = AgentTools.CreateVerdictTool();
        Assert.StartsWith("failed: a blocked verdict requires a code", conclusion.Execute(Call("done", Input("""{ "status": "blocked", "summary": "could not tell" }"""))).Content, StringComparison.Ordinal);
        Assert.StartsWith("failed: ", conclusion.Execute(Call("done", Input("""{ "status": "blocked", "summary": "the total is wrong", "code": "ASSERTION_FAILED" }"""))).Content, StringComparison.Ordinal);
        Assert.False(conclusion.Concluded);
        Assert.Equal("Step concluded.", conclusion.Execute(Call("done", Input("""{ "status": "blocked", "summary": "no login", "code": "AUTH_CREDENTIAL_UNAVAILABLE" }"""))).Content);
        Assert.Equal(new StepVerdict(StepVerdictStatus.Blocked, "no login", "AUTH_CREDENTIAL_UNAVAILABLE"), conclusion.Verdict);
    }

    // .NET-only: the grammar is offered by engine capability and by the action kinds the engine declares, where upstream's follows the verbs
    // its target lists, so the vocabulary each of them unlocks is checked here.
    [Fact]
    public void Offers_the_node_verbs_and_the_scroll_and_history_tools_their_capabilities_unlock()
    {
        var full = AgentTools.GrammarToolDefinitions(EngineCapabilities.Scroll | EngineCapabilities.History, null, EngineActionKinds.Locator);
        Assert.Equal(
            ["back", "check", "clear", "double_tap", "drag", "drag_at", "fill", "fill_secret", "hover", "hover_at", "long_press", "navigate", "observe", "press", "press_at", "right_click", "screenshot", "scroll", "scroll_to", "select", "select_at", "tap", "tap_at", "type_at", "uncheck"],
            full.Select(tool => tool.Name).Order(StringComparer.Ordinal));

        // An engine that declares no action kind has no node verb: only what its capabilities unlock is left.
        var bare = AgentTools.GrammarToolDefinitions(EngineCapabilities.None);
        Assert.Equal(["navigate", "observe", "screenshot"], bare.Select(tool => tool.Name).Order(StringComparer.Ordinal));

        // dismiss_keyboard comes with an engine that can hide its keyboard, and takes no input.
        var device = AgentTools.GrammarToolDefinitions(EngineCapabilities.Keyboard, dismissKeyboard: true);
        Assert.Empty(device.Single(tool => tool.Name == "dismiss_keyboard").Parameters.GetProperty("properties").EnumerateObject());
        Assert.DoesNotContain(AgentTools.GrammarToolDefinitions(EngineCapabilities.Keyboard), tool => tool.Name == "dismiss_keyboard");
        Assert.DoesNotContain(AgentTools.GrammarToolDefinitions(EngineCapabilities.None, dismissKeyboard: true), tool => tool.Name == "dismiss_keyboard");
        Assert.True(AgentTools.IsReserved("dismiss_keyboard"));
    }

    // .NET-only: the node verbs follow the action kinds the engine declares (upstream's KINDS_BY_VERB), and the point verbs that act on a
    // listed control follow the node verb they fall back to. Upstream gates tap on tap, double_tap on doubleTap, type and type_secret on fill,
    // press on press, select on selectOption, and check on both check and uncheck; the port's clear follows clear and uncheck follows check.
    [Fact]
    public void Offers_each_node_verb_only_for_the_action_kinds_the_engine_declares()
    {
        static string[] Offered(EngineCapabilities capabilities, params string[] kinds) =>
            [.. AgentTools.GrammarToolDefinitions(capabilities, null, kinds).Select(tool => tool.Name).Where(name => name is not ("observe" or "navigate" or "screenshot")).Order(StringComparer.Ordinal)];

        Assert.Equal(["tap", "tap_at"], Offered(EngineCapabilities.None, "tap"));
        Assert.Equal(["double_tap"], Offered(EngineCapabilities.None, "doubleTap"));
        Assert.Equal(["fill", "fill_secret", "type_at"], Offered(EngineCapabilities.None, "fill"));
        Assert.Equal(["press", "press_at"], Offered(EngineCapabilities.None, "press"));
        Assert.Equal(["select", "select_at"], Offered(EngineCapabilities.None, "selectOption"));
        Assert.Equal(["clear"], Offered(EngineCapabilities.None, "clear"));

        // A check sets a state rather than flipping one, so it needs both directions.
        Assert.Empty(Offered(EngineCapabilities.None, "check"));
        Assert.Empty(Offered(EngineCapabilities.None, "uncheck"));
        Assert.Equal(["check", "uncheck"], Offered(EngineCapabilities.None, "check", "uncheck"));

        // Kinds that unlock no node verb unlock none (a focus is no verb).
        Assert.Empty(Offered(EngineCapabilities.None, "focus", "scrollIntoView", "setInputFiles"));
        Assert.Empty(Offered(EngineCapabilities.None));

        // A keyboard takes fill and press without a target, with or without the kinds, and the point tools that type and press.
        Assert.Equal(["fill", "press", "press_at", "type_at"], Offered(EngineCapabilities.Keyboard));

        // With both kinds and the keyboard, the target is optional and a call without one acts on the focused field; with fill alone
        // the schema carries a target too; with the keyboard alone it carries none.
        static bool HasTarget(ModelTool tool) => tool.Parameters.GetProperty("properties").TryGetProperty("ref", out _);
        Assert.True(HasTarget(AgentTools.GrammarToolDefinitions(EngineCapabilities.Keyboard, null, ["fill", "press"]).Single(tool => tool.Name == "fill")));
        Assert.True(HasTarget(AgentTools.GrammarToolDefinitions(EngineCapabilities.Keyboard, null, ["fill", "press"]).Single(tool => tool.Name == "press")));
        Assert.True(HasTarget(AgentTools.GrammarToolDefinitions(EngineCapabilities.None, null, ["fill"]).Single(tool => tool.Name == "fill")));
        Assert.False(HasTarget(AgentTools.GrammarToolDefinitions(EngineCapabilities.Keyboard, null, ["tap"]).Single(tool => tool.Name == "fill")));

        // The same set decides the verbs of the executor's target, and a call to a verb the engine did not unlock is refused as unknown.
        Assert.Equal(
            ["DoubleTap", "Navigate", "Press", "Tap"],
            ActContext.VerbsOf(EngineCapabilities.None, ["tap", "doubleTap", "press", "check", "focus"], []).Select(verb => verb.ToString()).Order(StringComparer.Ordinal));
    }

    // .NET-only: the pointer verbs follow the action kinds the engine declares, where upstream's follow the verbs its target lists,
    // and drag names its drop target in a second argument.
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

        // The point verbs that fall back to a node verb are offered with it, and say the bare point cannot be taken.
        var points = AgentTools.GrammarToolDefinitions(EngineCapabilities.None, null, ["tap", "hover", "dragTo"], []).ToDictionary(tool => tool.Name);
        Assert.Contains("which this engine cannot do", points["hover_at"].Description, StringComparison.Ordinal);
        Assert.Contains("which this engine cannot do", points["drag_at"].Description, StringComparison.Ordinal);
        var bareToo = AgentTools.GrammarToolDefinitions(EngineCapabilities.None, null, ["tap", "hover", "dragTo"], ["tap", "hover", "dragTo"]).ToDictionary(tool => tool.Name);
        Assert.DoesNotContain("which this engine cannot do", bareToo["hover_at"].Description, StringComparison.Ordinal);
        Assert.DoesNotContain("which this engine cannot do", bareToo["drag_at"].Description, StringComparison.Ordinal);
        Assert.DoesNotContain(AgentTools.GrammarToolDefinitions(EngineCapabilities.None, null, ["tap"], []), tool => tool.Name is "hover_at" or "drag_at");
    }

    // .NET-only: the ceiling of the times a press repeats (upstream's accepted input for press carries times: 8, and nothing checks the ceiling).
    [Fact]
    public void Declares_the_times_of_press_up_to_the_ceiling()
    {
        var times = AgentTools.GrammarToolDefinitions(EngineCapabilities.None, null, EngineActionKinds.Locator).Single(tool => tool.Name == "press").Parameters.GetProperty("properties").GetProperty("times");
        Assert.Equal(AgentTools.MaxPressTimes, times.GetProperty("maximum").GetInt32());
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

    // .NET-only: the same through the tool loop, which ends the step at the first verdict of a turn.
    [Fact]
    public async Task Ends_the_step_at_the_first_verdict_of_a_turn()
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

    // .NET-only: the same through the tool loop, whose next calls show the step stayed open.
    [Fact]
    public async Task Leaves_the_step_open_after_a_blocked_verdict_without_a_blocking_code_until_a_real_one()
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
