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
/// Upstream's <c>agent-tap-modifiers.test.ts</c>. The port has no <c>right_click</c> (it has no secondary tap), so the tap family
/// here is <c>tap</c> and <c>double_tap</c>. Its tools do not close their schemas, so a field an engine does not declare is refused
/// when the tool runs, not by the schema. The tools take a role and a name, not a node id, and the prose of a recorded action is
/// <c>DefaultAgent.Describe</c>, which the hand-off uses.
/// </summary>
public sealed class TapModifiersInTheGrammarToolsTests
{
    private static readonly string[] TapFamily = ["tap", "double_tap"];

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

    /// <summary>Partial: the port has no right_click, and its schemas are not closed, so what the schema accepts is checked by the property it declares, and what it refuses by running the tool.</summary>
    [Fact]
    public async Task Partial_offers_modifiers_on_the_tap_family_only_when_the_engine_declares_tapModifiers()
    {
        var holding = AgentTools.CreateGrammarTools(EngineCapabilities.TapModifiers);
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
            ModelResponses.Call("tap", new { role = "button", name = "Save", modifiers = "Shift" }));
        Assert.Empty(engine!.Clicks);
        Assert.Equal(4, results.Count);
        Assert.All(results, result => Assert.Matches("^tap #e\\d+ failed: INVALID_ARGUMENT: .*modifier", result));
    }

    /// <summary>Partial: the port has no right_click, and its schemas are not closed, so the engine that does not declare tapModifiers is shown to refuse the field instead.</summary>
    [Fact]
    public async Task Partial_keeps_modifiers_out_of_the_vocabulary_when_the_engine_does_not_declare_tapModifiers()
    {
        var plain = AgentTools.CreateGrammarTools(EngineCapabilities.None);
        foreach (var name in TapFamily)
        {
            var properties = PropertiesOf(plain, name);
            Assert.True(properties.TryGetProperty("role", out _), name + " still takes a bare tap");
            Assert.False(properties.TryGetProperty("modifiers", out _), name + " refuses the undeclared field");
        }

        var (results, _) = await ActAsync(declared: false, Call("tap", "Save", "Shift"));
        Assert.StartsWith("tap #e", Assert.Single(results), StringComparison.Ordinal);
    }

    /// <summary>Partial: the port has no right_click, so the third call upstream makes without modifiers is a double tap here.</summary>
    [Fact]
    public async Task Partial_threads_modifiers_through_to_the_tap_family_actions()
    {
        var (_, engine) = await ActAsync(
            declared: true,
            Call("tap", "Save", "Shift"),
            Call("double_tap", "Open", "Control"),
            Call("double_tap", "Close"),
            Call("tap", "Close", "Alt", "Meta"));

        Assert.Equal(
            [
                (typeof(LocatorAction.Tap), (KeyModifier[]?)[KeyModifier.Shift]),
                (typeof(LocatorAction.DoubleTap), [KeyModifier.Control]),
                (typeof(LocatorAction.DoubleTap), null),
                (typeof(LocatorAction.Tap), [KeyModifier.Alt, KeyModifier.Meta]),
            ],
            engine!.Clicks.Select(click => (click.GetType(), click is LocatorAction.Tap tap ? tap.Modifiers?.ToArray() : ((LocatorAction.DoubleTap)click).Modifiers?.ToArray())));
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
        store.Write("abc123", recorder.Finalize(StepCacheHarness.ExampleStep, "/", "/", [], [])!);

        var entry = Assert.IsType<CacheEntry>(store.Read("abc123").Entry);

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
