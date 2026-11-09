// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E.Engine;
using E2E.Tests.ScriptedScreen;

namespace E2E.Tests.PressSequentially;

/// <summary>
/// <c>locator.pressSequentially</c>, ported from upstream <c>press-sequentially.test.ts</c>: composed in core from the <c>focus</c> action and
/// the keyboard, with no engine contract growth. What the engine sees, what the step records, and what is refused before any node resolves.
/// The scene is upstream's in-memory engine: it logs every action and keystroke call it receives.
/// Not ported, because C# cannot write them: "refuses a Secret with INVALID_ARGUMENT and points at fill" (the typed API has no
/// <c>Secret</c> overload of <c>PressSequentiallyAsync</c>, so the call does not compile) and the non-string text, <c>NaN</c> delay, and
/// <c>delayMs</c> cases of "rejects a non-string text and a malformed delay as INVALID_ARGUMENT" (a <c>string</c> and a
/// <see cref="TimeSpan"/> rule them out). The last test is the part of that <c>it</c> C# can express, so it does not keep its name.
/// </summary>
public sealed class LocatorPressSequentiallyTests
{
    private static ScriptedScene CityScene(bool keyboard = true, IReadOnlyCollection<string>? actions = null)
    {
        var scene = new ScriptedScene(_ => [new ScriptedNode { Id = "city", Role = "textbox", Name = "City" }]);
        scene.Open();
        scene.Screen.Steps = new StepRecorder("attempt");
        scene.Screen.Capabilities = keyboard ? EngineCapabilities.Keyboard : EngineCapabilities.None;
        scene.Screen.ActionKinds = actions;
        return scene;
    }

    [Fact]
    public async Task Focuses_the_one_matching_node_then_types_the_whole_text_in_one_keyboard_call()
    {
        using var scene = CityScene();

        await scene.Screen.GetByLabel("City").PressSequentiallyAsync("Warsaw");

        Assert.Equal(["focus:city", "type:Warsaw"], scene.Log);
        var step = Assert.Single(scene.Screen.Steps!.All());
        Assert.Equal((StepKind.Locator, "locator.pressSequentially", "getByLabel(\"City\")", StepStatus.Passed), (step.Kind, step.Api, step.Label, step.Status));
    }

    [Fact]
    public async Task With_a_delay_types_one_character_per_call_and_waits_between_them()
    {
        using var scene = CityScene();
        var started = Stopwatch.GetTimestamp();

        await scene.Screen.GetByLabel("City").PressSequentiallyAsync("Wa", new PressSequentiallyOptions { Delay = TimeSpan.FromMilliseconds(60) });

        Assert.Equal(["focus:city", "type:W", "type:a"], scene.Log);
        // A timer may fire a hair early, so the bound is a little under the 60 ms asked for.
        Assert.True(Stopwatch.GetElapsedTime(started) >= TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public async Task Cuts_a_pause_at_the_action_deadline_and_sends_no_character_past_it()
    {
        using var scene = CityScene();
        var started = Stopwatch.GetTimestamp();

        var error = await Assert.ThrowsAsync<TestException>(() =>
            scene.Screen.GetByLabel("City").PressSequentiallyAsync("ab", new PressSequentiallyOptions { Delay = TimeSpan.FromSeconds(1), Timeout = TimeSpan.FromMilliseconds(100) }));

        Assert.Equal("ACTION_FAILED", error.Code);
        Assert.Contains("typing 1 of 2 characters", error.Message, StringComparison.Ordinal);
        // Returned before the 1 s pause could elapse; the log proves nothing went out past the cut.
        Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(1));
        Assert.Equal(["focus:city", "type:a"], scene.Log);
    }

    [Fact]
    public async Task Splits_by_code_point_so_a_surrogate_pair_is_one_keystroke()
    {
        using var scene = CityScene();

        await scene.Screen.GetByLabel("City").PressSequentiallyAsync("a\U0001F600", new PressSequentiallyOptions { Delay = TimeSpan.Zero });

        Assert.Equal(["focus:city", "type:a", "type:\U0001F600"], scene.Log);
    }

    [Fact]
    public async Task Refuses_a_target_whose_engine_has_no_keyboard_before_resolving_any_node()
    {
        using var scene = CityScene(keyboard: false);

        var error = await Assert.ThrowsAsync<EngineException>(() => scene.Screen.GetByLabel("City").PressSequentiallyAsync("W"));

        Assert.Equal(EngineErrorCodes.UnsupportedCapability, error.Code);
        Assert.Contains("no keyboard", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, scene.Observations);
        Assert.Empty(scene.Log);
    }

    [Fact]
    public async Task Refuses_a_target_whose_engine_does_not_declare_the_focus_action()
    {
        using var scene = CityScene(actions: [.. EngineActionKinds.Locator.Where(kind => kind != "focus")]);

        var error = await Assert.ThrowsAsync<EngineException>(() => scene.Screen.GetByLabel("City").PressSequentiallyAsync("W"));

        Assert.Equal(EngineErrorCodes.UnsupportedCapability, error.Code);
        Assert.Contains("\"focus\" action", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, scene.Observations);
    }

    /// <summary>Partial: upstream's "rejects a non-string text and a malformed delay as INVALID_ARGUMENT" also passes a number, <c>NaN</c>, and <c>delayMs</c>, which C# types rule out. A null text and a negative delay are what remain.</summary>
    [Fact]
    public async Task Rejects_a_null_text_and_a_negative_delay_before_any_node_resolves()
    {
        using var scene = CityScene();
        var city = scene.Screen.GetByLabel("City");

        await Assert.ThrowsAsync<ArgumentNullException>(() => city.PressSequentiallyAsync(null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => city.PressSequentiallyAsync("W", new PressSequentiallyOptions { Delay = TimeSpan.FromMilliseconds(-1) }));
        Assert.Equal(0, scene.Observations);
        Assert.Empty(scene.Log);
    }
}
