// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Cache;
using E2E.Engine;
using E2E.Internal;
using E2E.Tests.StepCache;
using E2E.Tests.TraceReplay;
using Recorder = E2E.Cache.TraceRecorder;

namespace E2E.Tests.AgentTapModifiers;

/// <summary>
/// Upstream's <c>agent-tap-modifiers.test.ts</c>. The port's tools do not close their schemas, so a field an engine does not declare is refused
/// when the tool runs, not by the schema. The tools take a role and a name, not a node id, and the prose of a recorded action is
/// <c>DefaultAgent.Describe</c>, which the hand-off uses.
/// </summary>
public sealed class TapModifiersInTheGrammarToolsTests
{
    private static readonly string[] TapFamily = ["tap", "double_tap", "right_click"];

    // Each click changes the screen, so the step does not wait out the change window after every action.
    private static DocumentWorld World()
    {
        var clicks = 0;
        return new DocumentWorld().Map("/", page =>
        {
            var count = page.Status("Clicked 0 times");
            void Click() => count.Name = count.Text = $"Clicked {++clicks} times";
            page.Button("Save", Click);
            page.Button("Open", Click);
            page.Button("Close", Click);
        });
    }

    private static JsonElement PropertiesOf(IReadOnlyList<ModelTool> tools, string name) => tools.Single(tool => tool.Name == name).Parameters.GetProperty("properties");

    /// <summary>One act over <see cref="World"/> in which the model calls <paramref name="calls"/> in turn and then ends the step.</summary>
    private static async Task<(IReadOnlyList<string> Results, TapModifiersEngine? Engine)> ActAsync(bool declared, params ModelResponse[] calls)
    {
        TapModifiersEngine? engine = null;
        var next = 0;
        var model = new ScriptedModel(_ => next < calls.Length ? calls[next++] : ModelResponses.Done("passed", "Done."));
        var step = await StepCachePipelineTests.StepAsync(World(), new FileStepCache(CoreTests.TempCache()), "/", null, model, wrap: inner => declared ? engine = new TapModifiersEngine(inner) : inner);
        Assert.Null(step.Error);
        var results = model.Requests[^1].Messages.Where(message => message.Role == "tool").Select(message => message.Content ?? "").Where(content => !content.StartsWith("done", StringComparison.Ordinal)).ToList();
        return (results, engine);
    }

    private static ModelResponse Call(string tool, string name, params string[]? modifiers) =>
        ModelResponses.Call(tool, modifiers is null ? new { role = "button", name } : (object)new { role = "button", name, modifiers });

    /// <summary>Partial: upstream parses each tool's zod schema; the port reads the property its JSON schema declares, and checks the refusal by running the tool.</summary>
    [Fact]
    public async Task Partial_offers_modifiers_on_the_tap_family_only_when_the_engine_declares_tapModifiers()
    {
        var holding = AgentTools.GrammarToolDefinitions(EngineCapabilities.TapModifiers, null, EngineActionKinds.Locator);
        foreach (var name in TapFamily)
        {
            var modifiers = PropertiesOf(holding, name).GetProperty("modifiers");
            Assert.Equal("array", modifiers.GetProperty("type").GetString());
            Assert.Equal(["Shift", "Control", "Alt", "Meta", "ControlOrMeta"], modifiers.GetProperty("items").GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
        }

        // The rest of the grammar never takes modifiers, even on a holding engine.
        foreach (var tool in holding.Where(tool => !TapFamily.Contains(tool.Name)))
        {
            Assert.False(tool.Parameters.GetProperty("properties").TryGetProperty("modifiers", out _), tool.Name + " refuses modifiers");
        }

        // A modifier that is unknown, in another case, repeated, or not a list is refused before the engine is touched.
        var (results, engine) = await ActAsync(
            declared: true,
            Call("tap", "Save", "shift"),
            Call("tap", "Save", "Bogus"),
            Call("tap", "Save", "Shift", "Shift"),
            ModelResponses.Call("tap", new { role = "button", name = "Save", modifiers = "Shift" }),
            Call("right_click", "Save", "shift"));
        Assert.Empty(engine!.Clicks);
        Assert.Equal(5, results.Count);
        Assert.All(results, result => Assert.Matches("^(tap|right_click) #e\\d+ failed: INVALID_ARGUMENT: .*modifier", result));
    }

    /// <summary>Partial: upstream parses each tool's zod schema, so it covers right_click too; the port runs the tools through the loop, and the document engine offers no right_click, so only tap and double_tap are run.</summary>
    [Fact]
    public async Task Partial_keeps_modifiers_out_of_the_vocabulary_when_the_engine_does_not_declare_tapModifiers()
    {
        var plain = AgentTools.GrammarToolDefinitions(EngineCapabilities.None, null, EngineActionKinds.Locator);
        foreach (var name in TapFamily)
        {
            var properties = PropertiesOf(plain, name);
            Assert.True(properties.TryGetProperty("role", out _), name + " still takes a bare tap");
            Assert.False(properties.TryGetProperty("modifiers", out _), name + " refuses the undeclared field");
        }

        // The loop's closed schema refuses the field the same way upstream's parse does.
        string[] offered = ["tap", "double_tap"];
        var (results, _) = await ActAsync(declared: false, [.. offered.Select(name => Call(name, "Save", "Shift"))]);
        Assert.Equal(offered.Length, results.Count);
        Assert.All(results, result =>
        {
            Assert.StartsWith("failed: Invalid input for tool ", result, StringComparison.Ordinal);
            Assert.Contains("\"code\":\"unrecognized_keys\"", result, StringComparison.Ordinal);
            Assert.Contains("\"modifiers\"", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Threads_modifiers_through_to_the_tap_family_actions()
    {
        var (_, engine) = await ActAsync(
            declared: true,
            Call("tap", "Save", "Shift"),
            Call("double_tap", "Open", "Control"),
            Call("right_click", "Close"),
            Call("right_click", "Save", "Alt"),
            Call("tap", "Close", "Alt", "Meta"));

        Assert.Equal(
            [
                (typeof(LocatorAction.Tap), (KeyModifier[]?)[KeyModifier.Shift]),
                (typeof(LocatorAction.DoubleTap), [KeyModifier.Control]),
                (typeof(LocatorAction.SecondaryTap), null),
                (typeof(LocatorAction.SecondaryTap), [KeyModifier.Alt]),
                (typeof(LocatorAction.Tap), [KeyModifier.Alt, KeyModifier.Meta]),
            ],
            engine!.Clicks.Select(click => (click.GetType(), click switch
            {
                LocatorAction.Tap tap => tap.Modifiers?.ToArray(),
                LocatorAction.DoubleTap doubleTap => doubleTap.Modifiers?.ToArray(),
                _ => ((LocatorAction.SecondaryTap)click).Modifiers?.ToArray(),
            })));
    }

    [Fact]
    public void Says_what_keys_a_tap_held_in_its_prose()
    {
        var held = DefaultAgent.Describe(new RecordedAction { Kind = "tap", Role = "button", Name = "Save", Modifiers = ["Shift", "Control"] });
        Assert.Contains("with Shift+Control held", held);

        var plain = DefaultAgent.Describe(new RecordedAction { Kind = "tap", Role = "button", Name = "Save" });
        Assert.DoesNotContain("held", plain);
    }

    [Fact]
    public void Keeps_modifiers_through_the_trace_round_trip()
    {
        var recorder = new Recorder(() => Redactor.None);
        var save = new SemanticNode { Ref = "n1", Role = "button", Name = "Save" };
        recorder.Record(new RecordedAction { Kind = "tap", Role = save.Role, Name = save.Name, Modifiers = ["Shift"] });
        recorder.Record("tap", save);
        var store = new FileStepCache(CoreTests.TempCache());
        store.Write(StepCacheHarness.Key, recorder.Finalize(StepCacheHarness.ExampleStep, "/", "/", [], [])!);

        var entry = Assert.IsType<CacheEntry>(store.Read(StepCacheHarness.Key).Entry);

        Assert.Equal(2, entry.Actions.Count);
        Assert.Equal(["Shift"], entry.Actions[0].Modifiers);
        Assert.Null(entry.Actions[1].Modifiers);
    }

    /// <summary>Partial: a stored <c>null</c> reads as no modifiers, since the port's reader does not tell a null from an absent field.</summary>
    [Fact]
    public async Task Partial_rejects_a_trace_whose_modifiers_are_malformed_or_on_a_verb_that_cannot_hold_them()
    {
        static async Task<string?> ReasonAsync(RecordedAction action)
        {
            var entry = new CacheEntry { Schema = FileStepCache.SchemaVersion, Actions = [action], Appeared = [], Gone = [] };
            var session = StepCacheHarness.MakeSession(StepCacheHarness.FakeContext(() => new CacheLookup { Entry = entry }), StepCacheHarness.MakeHost(["/"], [StepCacheHarness.Node("n1", "button", "Save")]));
            await session.BeginAsync();
            return session.CacheInfo?.Reason;
        }

        Assert.NotEqual("invalid-entry", await ReasonAsync(new RecordedAction { Kind = "tap", Role = "button", Name = "Save", Modifiers = ["Shift"] }));
        foreach (var modifiers in new[] { Array.Empty<string>(), ["shift"], ["Shift", "Shift"] })
        {
            Assert.Equal("invalid-entry", await ReasonAsync(new RecordedAction { Kind = "tap", Role = "button", Name = "Save", Modifiers = [.. modifiers] }));
        }

        Assert.Equal("invalid-entry", await ReasonAsync(new RecordedAction { Kind = "scrollTo", Role = "button", Name = "Save", Modifiers = ["Shift"] }));
        Assert.Equal("invalid-entry", await ReasonAsync(new RecordedAction { Kind = "hover", Role = "button", Name = "Save", Modifiers = ["Shift"] }));
        Assert.NotEqual("invalid-entry", await ReasonAsync(new RecordedAction { Kind = "secondaryTap", Role = "button", Name = "Save", Modifiers = ["Shift"] }));
    }

    [Fact]
    public async Task Replays_a_recorded_tap_with_the_keys_it_held()
    {
        var host = TestHost.MakeHost();
        var action = new RecordedAction { Kind = "tap", Role = "button", Name = "Upgrade", Modifiers = ["Shift"] };

        var outcome = await Replay.ReplayTrace(host, [action]);

        Assert.True(outcome.Completed);
        Assert.Equal(1, outcome.Executed);
        Assert.Equal([KeyModifier.Shift], Assert.Single(host.Clicks)!.Modifiers);
    }
}
