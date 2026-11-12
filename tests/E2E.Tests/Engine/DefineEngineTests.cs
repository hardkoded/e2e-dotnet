// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.Helpers;

namespace E2E.Tests.Engine;

/// <summary>
/// Upstream's <c>describe('defineEngine')</c> (<c>engine.test.ts</c>). The cases are the port's <see cref="EngineManifest.DefineEngine"/> over
/// <see cref="EngineSpec"/>. Upstream checks an object literal's keys and a value's JavaScript type at runtime; a C# record has the fields it
/// has, so those cases are skipped (listed in the PR) or partial (the part that a typed record can still express is checked).
/// </summary>
public sealed class DefineEngineTests
{
    private static EngineSpec Toy() => new()
    {
        Name = "toy",
        Version = "1.0.0",
        SpiVersion = 1,
        Observe = (_, _) => Task.FromResult(Snapshot.Of([Snapshot.Node("n1", "Save"), Snapshot.Node("n2", "Cancel")])),
    };

    private static EngineSpec Bare() => new() { Name = "toy", Version = "1", SpiVersion = 1 };

    [Fact]
    public void Computes_the_capability_set_from_declared_members_perform_is_the_actions_capability()
    {
        var handle = EngineManifest.DefineEngine(Toy() with { Actions = ["tap", "fill"], Perform = (_, _, _) => Task.CompletedTask });

        Assert.Equal(["actions", "observation"], handle.Capabilities.Order(StringComparer.Ordinal));
        Assert.Equal(["tap", "fill"], handle.Actions);
        Assert.True(EngineManifest.IsEngineHandle(handle));
    }

    /// <summary>
    /// Partial: upstream also asserts the handle's arrays are frozen. A C# list cannot be frozen; the handle keeps its own copy of the kind list,
    /// which this checks by changing the declared list after the definition.
    /// </summary>
    [Fact]
    public void Partial_Computes_the_capability_set_from_declared_members_and_keeps_its_own_copy_of_the_action_list()
    {
        var declared = new List<string> { "tap", "fill" };
        var handle = EngineManifest.DefineEngine(Toy() with { Actions = declared, Perform = (_, _, _) => Task.CompletedTask });

        declared.Add("press");

        Assert.Equal(["tap", "fill"], handle.Actions);
    }

    [Fact]
    public void Rejects_perform_locate_and_performAt_without_observe_their_refs_are_observation_refs()
    {
        var perform = Assert.Throws<ConfigurationException>(() => EngineManifest.DefineEngine(Bare() with { Actions = EngineActionKinds.Locator, Perform = (_, _, _) => Task.CompletedTask }));
        Assert.Contains("perform without observe", perform.Message, StringComparison.Ordinal);

        var locate = Assert.Throws<ConfigurationException>(() => EngineManifest.DefineEngine(Bare() with { Locate = (_, _) => Task.FromResult<IReadOnlyList<EngineNode>>([]) }));
        Assert.Contains("locate without observe", locate.Message, StringComparison.Ordinal);

        var performAt = Assert.Throws<ConfigurationException>(() => EngineManifest.DefineEngine(Bare() with { PointerActions = ["tap"], PerformAt = (_, _, _) => Task.CompletedTask }));
        Assert.Contains("performAt without observe", performAt.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Requires_the_action_list_with_perform_and_perform_with_the_action_list()
    {
        var withoutActions = Assert.Throws<ConfigurationException>(() => EngineManifest.DefineEngine(Toy() with { Perform = (_, _, _) => Task.CompletedTask }));
        Assert.Contains("perform without actions: list the action kinds", withoutActions.Message, StringComparison.Ordinal);

        var withoutPerform = Assert.Throws<ConfigurationException>(() => EngineManifest.DefineEngine(Toy() with { Actions = ["tap"] }));
        Assert.Contains("actions without perform", withoutPerform.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Partial: upstream also refuses a non-boolean <c>tapModifiers</c>, which a C# <c>bool?</c> cannot hold; the other rules are checked.
    /// </summary>
    [Fact]
    public void Partial_Carries_tapModifiers_onto_the_handle_only_with_perform()
    {
        Assert.True(EngineManifest.DefineEngine(Toy() with { Actions = ["tap"], Perform = (_, _, _) => Task.CompletedTask, TapModifiers = true }).TapModifiers);
        Assert.Null(EngineManifest.DefineEngine(Toy() with { Actions = ["tap"], Perform = (_, _, _) => Task.CompletedTask, TapModifiers = false }).TapModifiers);

        var error = Assert.Throws<ConfigurationException>(() => EngineManifest.DefineEngine(Toy() with { TapModifiers = true }));
        Assert.Contains("tapModifiers without perform", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Partial: upstream also refuses a string and a number in the list, which a typed <c>IReadOnlyList&lt;string&gt;</c> cannot hold; the
    /// unknown kind, the repeated kind, and the empty list are checked.
    /// </summary>
    [Fact]
    public void Partial_Closes_the_action_list_known_kinds_each_once_at_least_one()
    {
        ConfigurationException Define(IReadOnlyList<string> actions) =>
            Assert.Throws<ConfigurationException>(() => EngineManifest.DefineEngine(Toy() with { Actions = actions, Perform = (_, _, _) => Task.CompletedTask }));

        Assert.Contains("actions names unknown kind \"click\"; expected one of tap, doubleTap", Define(["tap", "click"]).Message, StringComparison.Ordinal);
        Assert.Contains("actions lists \"tap\" twice", Define(["tap", "tap"]).Message, StringComparison.Ordinal);
        Assert.Contains("actions must be a non-empty array", Define([]).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Computes_the_pointer_capability_from_performAt_which_needs_its_pointerActions_list()
    {
        var handle = EngineManifest.DefineEngine(Toy() with { PerformAt = (_, _, _) => Task.CompletedTask, PointerActions = ["tap", "longPress"] });

        Assert.Equal(["observation", "pointer"], handle.Capabilities.Order(StringComparer.Ordinal));
        Assert.Equal(["tap", "longPress"], handle.PointerActions);

        Assert.Contains("performAt without pointerActions", Assert.Throws<ConfigurationException>(() => EngineManifest.DefineEngine(Toy() with { PerformAt = (_, _, _) => Task.CompletedTask })).Message, StringComparison.Ordinal);
        Assert.Contains("pointerActions without performAt", Assert.Throws<ConfigurationException>(() => EngineManifest.DefineEngine(Toy() with { PointerActions = ["tap"] })).Message, StringComparison.Ordinal);
        Assert.Contains("pointerActions names unknown kind \"fill\"", Assert.Throws<ConfigurationException>(() => EngineManifest.DefineEngine(Toy() with { PerformAt = (_, _, _) => Task.CompletedTask, PointerActions = ["fill"] })).Message, StringComparison.Ordinal);
        Assert.Contains("pointerActions must be a non-empty array", Assert.Throws<ConfigurationException>(() => EngineManifest.DefineEngine(Toy() with { PerformAt = (_, _, _) => Task.CompletedTask, PointerActions = [] })).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Computes_the_keyboard_capability_from_the_keyboard_hooks_which_need_observe_and_both_type_and_press()
    {
        var keyboard = new EngineKeyboardSpec { Type = (_, _, _) => Task.CompletedTask, Press = (_, _) => Task.CompletedTask };

        var handle = EngineManifest.DefineEngine(Toy() with { Keyboard = keyboard });

        Assert.Equal(["keyboard", "observation"], handle.Capabilities.Order(StringComparer.Ordinal));
        Assert.Null(handle.Keyboard!.Dismiss);
        Assert.Contains("keyboard without observe", Assert.Throws<ConfigurationException>(() => EngineManifest.DefineEngine(Bare() with { Keyboard = keyboard })).Message, StringComparison.Ordinal);
        Assert.Contains("keyboard.press must be a function", Assert.Throws<ConfigurationException>(() => EngineManifest.DefineEngine(Toy() with { Keyboard = new EngineKeyboardSpec { Type = keyboard.Type } })).Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Partial: upstream also refuses a non-handle in the secrets list and a non-array value, which the typed <c>IReadOnlyList&lt;Secret&gt;</c>
    /// cannot hold, and asserts the list is frozen. The handle keeps each name once, which is checked.
    /// </summary>
    [Fact]
    public void Partial_Keeps_the_secrets_its_options_hold_as_handles_each_name_once()
    {
        var handle = EngineManifest.DefineEngine(Toy() with { Secrets = [E2E.Secret.Create("key", "s3cret-one"), E2E.Secret.Create("key", "s3cret-one"), E2E.Secret.Create("other", "s3cret-two")] });

        Assert.Equal(["key", "other"], handle.Secrets!.Select(secret => secret.Name));
    }

    /// <summary>
    /// Partial: upstream refuses a <c>startVideo</c> without <c>stopVideo</c> and a <c>state</c> without <c>restore</c>, both checked here; it
    /// also rejects an unknown key inside a nested manifest, which a typed record has no way to receive, so that part is not ported.
    /// </summary>
    [Fact]
    public void Partial_Rejects_unknown_keys_inside_nested_manifests_the_grammar_is_closed()
    {
        var video = Assert.Throws<ConfigurationException>(() => EngineManifest.DefineEngine(Toy() with
        {
            Artifacts = new EngineArtifactsSpec { Screenshot = (_, _) => Task.FromResult("x"), StartVideo = _ => Task.CompletedTask },
        }));
        Assert.Contains("artifacts.startVideo and stopVideo must be declared together", video.Message, StringComparison.Ordinal);

        var restore = Assert.Throws<ConfigurationException>(() => EngineManifest.DefineEngine(Toy() with
        {
            State = new EngineStateSpec { Capture = _ => Task.FromResult(new EngineState("json", 1, null)) },
        }));
        Assert.Contains("state.restore must be a function", restore.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Partial: upstream calls each hook with the spec as its <c>this</c> and checks that a class instance's methods are found. C# delegates
    /// close over the spec, so the binding is the closure, and the call is checked to run.
    /// </summary>
    [Fact]
    public async Task Partial_Binds_validateApp_prepare_finish_and_fixture_factories_to_the_spec()
    {
        var bound = new List<string>();
        var spec = Toy() with
        {
            ValidateApp = (_, _) => bound.Add("validateApp"),
            Prepare = _ => { bound.Add("prepare"); return Task.CompletedTask; },
            Finish = _ => { bound.Add("finish"); return Task.CompletedTask; },
            Fixtures = new Dictionary<string, Delegate> { ["hello"] = new Action(() => bound.Add("fixture")) },
        };
        var handle = EngineManifest.DefineEngine(spec);

        handle.ValidateApp!(new EngineAppDeclaration(), new EngineAppCheckInfo { TargetName = "toy" });
        await handle.Prepare!(null!);
        await handle.Finish!(null!);
        handle.Fixtures!["hello"].DynamicInvoke();

        Assert.Equal(["validateApp", "prepare", "finish", "fixture"], bound);
    }

    /// <summary>
    /// Partial: upstream also rejects a non-positive, fractional, NaN, and string workers value; C# types it as an <c>int?</c>, so only the
    /// zero and negative bounds can be spelled. The declared bound is copied.
    /// </summary>
    [Fact]
    public void Partial_Copies_a_declared_workers_bound_and_rejects_one_that_is_not_a_positive_integer()
    {
        Assert.Equal(2, EngineManifest.DefineEngine(Toy() with { Workers = 2 }).Workers);
        Assert.Null(EngineManifest.DefineEngine(Toy()).Workers);
        foreach (var workers in new[] { 0, -1 })
        {
            Assert.Contains("workers must be a positive safe integer", Assert.Throws<ConfigurationException>(() => EngineManifest.DefineEngine(Toy() with { Workers = workers })).Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Requires_a_version_provenance_and_the_trace_cache_key_depend_on_it()
    {
        Assert.Contains("version", Assert.Throws<ConfigurationException>(() => EngineManifest.DefineEngine(Toy() with { Version = "" })).Message, StringComparison.Ordinal);
        Assert.Contains("version must be a non-empty string", Assert.Throws<ConfigurationException>(() => EngineManifest.DefineEngine(Toy() with { Version = null })).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_unsupported_spiVersion_values()
    {
        var error = Assert.Throws<ConfigurationException>(() => EngineManifest.DefineEngine(Toy() with { SpiVersion = 2 }));
        Assert.Contains("spiVersion 2", error.Message, StringComparison.Ordinal);
    }

    // Skipped upstream cases, named in the PR with the reason:
    //  - "refuses an app declaration, which the target makes now, and closes the session manifest": an object literal's unknown key and a
    //    JavaScript value's type; a C# record has no key to add and no type to mismatch.
    //  - "rejects unknown keys, pointing tools at the agent": the same reason, for the key `tools`.
    //  - "accepts the trace hooks an engine published before their removal declares, and never offers them": the port declares no retired hooks,
    //    and a typed record cannot receive an unknown property.
    //  - "accepts a class instance: prototype methods are found and bound to the body": JavaScript prototype lookup; C# has no equivalent, and a
    //    class's methods are bound by its delegates.
}
