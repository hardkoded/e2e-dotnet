// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace E2E.Tests.SdkTypes;

/// <summary>
/// Upstream's <c>tests/types/sdk-types.ts</c>, the lines the <c>grammar-tools-export</c> commit adds (tester-army/e2e#960) and the
/// <c>onBeforeAction</c> commit adds (tester-army/e2e#1387). Upstream checks
/// them with the type checker; here they are reflection checks of the public API. Not ported: the lines upstream's checks keep for the
/// AI SDK's <c>ToolSet</c>, which the port has no counterpart of (<see cref="ToolLoopTool"/> stands in for it).
/// </summary>
public sealed class SdkTypesTests
{
    private static readonly Assembly Library = typeof(AgentTools).Assembly;

    // "A custom loop keeps the built-in actions: the grammar tools take the loop's guard, and their rules are text."
    [Fact]
    public void Partial_a_custom_loop_keeps_the_built_in_actions()
    {
        var create = typeof(AgentTools).GetMethod(nameof(AgentTools.CreateGrammarTools), BindingFlags.Public | BindingFlags.Static)!;
        Assert.Equal(typeof(IReadOnlyList<ToolLoopTool>), create.ReturnType);
        Assert.Equal([typeof(StepExecutorContext), typeof(GrammarToolsOptions)], create.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(typeof(Func<Func<Task<string>>, Task<string>>), typeof(GrammarToolsOptions).GetProperty(nameof(GrammarToolsOptions.Guard))!.PropertyType);
        Assert.Equal(typeof(string), typeof(DefaultAgent).GetField(nameof(DefaultAgent.BaseRules))!.FieldType);
        Assert.False(string.IsNullOrWhiteSpace(DefaultAgent.BaseRules));
    }

    // "await agent.assert('all settings are reachable', { explore: true, maxSteps: 10, maxModelCalls: 6, vision: 'only' })".
    [Fact]
    public void Accepts_exploration_options_on_an_assertion()
    {
        var options = new AssertOptions { Explore = true, MaxSteps = 10, MaxModelCalls = 6, Vision = VisionMode.Only };

        Assert.True(options.Explore);
        Assert.Equal((10, 6, VisionMode.Only), (options.MaxSteps, options.MaxModelCalls, options.Vision));
    }

    // "@ts-expect-error the screen presenter stays internal" and "failure causes stay internal".
    [Fact]
    public void The_screen_presenter_and_the_failure_causes_stay_internal()
    {
        var options = typeof(GrammarToolsOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Equal([nameof(GrammarToolsOptions.Guard)], options.Select(property => property.Name));
        Assert.False(Library.GetType("E2E.ScreenPresenter")!.IsPublic);
        Assert.False(Library.GetType("E2E.ScreenUpdateOptions")!.IsPublic);
        Assert.DoesNotContain(typeof(AgentTools).GetMethods(BindingFlags.Public | BindingFlags.Static), method => method.Name == "GrammarTools");
    }

    // "An executor outside the AI SDK loop keeps the built-in verdict: the tool, its rules as text, and the verdict it collected."
    [Fact]
    public void Partial_an_executor_outside_the_loop_keeps_the_built_in_verdict()
    {
        var conclusion = AgentTools.CreateVerdictTool();
        Assert.IsType<ModelTool>(conclusion.Tool);
        Assert.Equal(typeof(StepVerdict), Nullable.GetUnderlyingType(typeof(VerdictTool).GetProperty(nameof(VerdictTool.Verdict))!.PropertyType) ?? typeof(VerdictTool).GetProperty(nameof(VerdictTool.Verdict))!.PropertyType);
        Assert.Null(conclusion.Verdict);
        Assert.False(string.IsNullOrWhiteSpace(AgentTools.VerdictRules));
        Assert.Equal(typeof(string), typeof(AgentTools).GetField(nameof(AgentTools.VerdictRules))!.FieldType);
    }

    // "A runtime hard stop narrows to the AgentError the step rethrows untouched."
    [Fact]
    public void Partial_a_runtime_hard_stop_is_told_apart_from_a_failure()
    {
        Assert.True(StepExecutors.IsRuntimeHardStop(new AgentException("STEP_TIMEOUT", "timed out")));
        Assert.False(StepExecutors.IsRuntimeHardStop(new AgentException("ACTION_FAILED", "failed")));
    }

    // "A grammar tool's result is a rendered screen: text, or text with its screenshot." The port's result is ToolLoopResult.
    [Fact]
    public void Partial_a_grammar_tool_answers_text_or_text_with_its_screenshot()
    {
        Assert.True(typeof(ToolLoopResult).IsPublic);
        Assert.Equal(typeof(string), typeof(ToolLoopResult).GetProperty(nameof(ToolLoopResult.Content))!.PropertyType);
        var pixels = typeof(ToolLoopResult).GetProperty(nameof(ToolLoopResult.Pixels))!.PropertyType;
        Assert.Equal(typeof(ExecutorPixels), Nullable.GetUnderlyingType(pixels) ?? pixels);
        Assert.Equal(typeof(string), typeof(ExecutorPixels).GetProperty(nameof(ExecutorPixels.MediaType))!.PropertyType);
    }

    // The lines the onBeforeAction commit adds (tester-army/e2e#1387): the event holds an index and a source and nothing a dispatch has not
    // committed ("before dispatch has no committed summary", "no engine target or geometry is exposed"), the hook is a function, and an
    // assertion has no before-action observer. Upstream's `onBeforeAction: true` check has no counterpart: a delegate cannot be anything else.
    [Fact]
    public void Partial_the_before_action_observer_sees_an_index_and_a_source_and_acts_take_it_but_assertions_do_not()
    {
        Assert.Equal(
            [nameof(BeforeActionEvent.Index), nameof(BeforeActionEvent.Source)],
            typeof(BeforeActionEvent).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(typeof(int), typeof(BeforeActionEvent).GetProperty(nameof(BeforeActionEvent.Index))!.PropertyType);
        Assert.Equal(typeof(ActionSource), typeof(BeforeActionEvent).GetProperty(nameof(BeforeActionEvent.Source))!.PropertyType);
        Assert.True(typeof(Delegate).IsAssignableFrom(typeof(BeforeActionHook)));
        Assert.Equal(typeof(BeforeActionHook), typeof(ActOptions).GetProperty(nameof(ActOptions.OnBeforeAction))!.PropertyType);
        Assert.Null(typeof(AssertOptions).GetProperty("OnBeforeAction"));
    }
}
