// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Reports;
using E2E.Tests.AgentAct;

namespace E2E.Tests.FixtureRuntime;

/// <summary>Upstream's <c>tests/unit/fixture-runtime.test.ts</c>, <c>describe('model reasoning on step events')</c>.</summary>
public sealed class ModelReasoningOnStepEventsTests
{
    private static async Task<StepEvent?> ModelEventAsync(ScriptedModel model, Func<E2ESession, Task> run)
    {
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(CounterPage.World()),
            Model = model,
            BaseUrl = "https://counter.test",
            TestTitle = "reasoning",
            CacheEnabled = false,
        });
        await session.App.OpenAsync("/");
        await run(session);
        return session.StepRecords.SelectMany(step => step.Events).FirstOrDefault(item => item.Kind == StepEventKind.Model);
    }

    [Fact]
    public async Task Carries_an_act_turns_reasoning_onto_its_model_event()
    {
        var model = new ScriptedModel(_ => new ModelResponse
        {
            ToolCalls = ModelResponses.Done("passed", "done").ToolCalls,
            ReasoningText = "The counter already reads 1; nothing left to do.",
        });

        var modelEvent = await ModelEventAsync(model, session => session.Agent.ActAsync("check the counter"));

        Assert.Equal("The counter already reads 1; nothing left to do.", modelEvent?.Reasoning);
    }

    [Fact]
    public async Task Carries_a_judgment_calls_reasoning_onto_its_model_event()
    {
        var judgment = ModelResponses.Judgment("holds", "the status reads 1");
        var model = new ScriptedModel(_ => new ModelResponse { Content = judgment.Content, ReasoningText = "The status node text is \"1\"." });

        var modelEvent = await ModelEventAsync(model, session => session.Agent.AssertAsync("the status reads 1"));

        Assert.Equal("The status node text is \"1\".", modelEvent?.Reasoning);
    }

    [Fact]
    public async Task Omits_the_field_when_the_turn_produced_no_reasoning()
    {
        var model = new ScriptedModel(_ => new ModelResponse { ToolCalls = ModelResponses.Done("passed", "done").ToolCalls });

        var modelEvent = await ModelEventAsync(model, session => session.Agent.ActAsync("check the counter"));

        Assert.NotNull(modelEvent);
        Assert.Null(modelEvent.Reasoning);
        Assert.False(ReportWire.Event(modelEvent).ContainsKey("reasoning"));
    }
}
