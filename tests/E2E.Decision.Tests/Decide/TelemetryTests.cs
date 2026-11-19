// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk;
using static E2E.Decision.Tests.Decide.DecideTestHelpers;

namespace E2E.Decision.Tests.Decide;

/// <summary>The AI SDK's <c>decide.test.ts</c>, <c>describe('telemetry')</c>.</summary>
public sealed class TelemetryTests
{
    [Fact]
    public async Task Partial_emits_operation_and_model_call_lifecycle_events()
    {
        var integration = new RecordingTelemetry();
        var decideStarts = new List<DecideStartEvent>();
        var decideEnds = new List<DecideEndEvent>();
        var (model, _) = Setup(new DecisionModelResult { Answers = AnswerMap, Usage = new DecisionUsage(30, 4) });
        var state = new DecisionStatePart[]
        {
            new DecisionJsonPart(JsonDocument.Parse("""{"message":"refund"}""").RootElement),
        };

        await ExperimentalDecide.RunAsync(
            new DecideOptions
            {
                Model = model,
                State = state,
                Questions = QuestionMap,
                Telemetry = new TelemetryOptions
                {
                    Integrations = [integration],
                    FunctionId = "decide-test",
                    RecordInputs = false,
                    RecordOutputs = true,
                },
                RuntimeContext = new Dictionary<string, object?> { ["requestId"] = "request-1", ["secret"] = "hidden" },
                OnStart = @event =>
                {
                    decideStarts.Add(@event);
                    return Task.CompletedTask;
                },
                OnEnd = @event =>
                {
                    decideEnds.Add(@event);
                    return Task.CompletedTask;
                },
                GenerateCallId = () => "test-call-id",
            }
        );

        var start = Assert.Single(integration.DecideStarts);
        Assert.Equal("test-call-id", start.CallId);
        Assert.Equal("ai.decide", start.OperationId);
        Assert.Equal("mock-provider", start.Provider);
        Assert.Equal("mock-model-id", start.ModelId);
        Assert.Empty(start.State);
        Assert.Empty(start.Questions);
        Assert.Empty(start.RuntimeContext);
        Assert.Equal(2, start.MaxRetries);
        Assert.False(start.RecordInputs);
        Assert.True(start.RecordOutputs);
        Assert.Equal("decide-test", start.FunctionId);

        var modelStart = Assert.Single(integration.ModelCallStarts);
        Assert.Equal("test-call-id", modelStart.CallId);
        Assert.Equal("ai.decide.doDecide", modelStart.OperationId);
        Assert.Empty(modelStart.State);
        Assert.Empty(modelStart.Questions);

        var modelEnd = Assert.Single(integration.ModelCallEnds);
        Assert.Equal("test-call-id", modelEnd.CallId);
        Assert.Equal("ai.decide.doDecide", modelEnd.OperationId);
        Assert.Equal(AnswerMap, modelEnd.Answers);
        Assert.Equal(new DecisionUsage(30, 4), modelEnd.Usage);

        var end = Assert.Single(integration.DecideEnds);
        Assert.Equal("test-call-id", end.CallId);
        Assert.Equal("ai.decide", end.OperationId);
        Assert.Equal(AnswerMap, end.Answers);
        Assert.Equal(new DecisionUsage(30, 4), end.Usage);
        Assert.Equal(34, end.Usage.TotalTokens);
        Assert.Empty(end.RuntimeContext);

        foreach (var callback in new DecideStartEvent[] { Assert.Single(decideStarts), Assert.Single(decideEnds) })
        {
            Assert.Equal("ai.decide", callback.OperationId);
            Assert.Equal(state, callback.State);
            Assert.Equal(QuestionMap, callback.Questions);
            Assert.Equal(
                new Dictionary<string, object?> { ["requestId"] = "request-1", ["secret"] = "hidden" },
                callback.RuntimeContext
            );
        }
    }

    [Fact]
    public async Task Includes_only_selected_runtime_context_fields()
    {
        var integration = new RecordingTelemetry();

        await ExperimentalDecide.RunAsync(
            new DecideOptions
            {
                Model = Setup().Model,
                State = [new DecisionTextPart("text")],
                Questions = QuestionMap,
                Telemetry = new TelemetryOptions
                {
                    Integrations = [integration],
                    IncludeRuntimeContext = new Dictionary<string, bool> { ["requestId"] = true },
                },
                RuntimeContext = new Dictionary<string, object?> { ["requestId"] = "request-1", ["secret"] = "hidden" },
            }
        );

        var start = Assert.Single(integration.DecideStarts);
        Assert.Equal(new Dictionary<string, object?> { ["requestId"] = "request-1" }, start.RuntimeContext);
    }

    [Fact]
    public async Task Emits_an_error_event_when_decision_fails()
    {
        var error = new InvalidOperationException("decision failed");
        var integration = new RecordingTelemetry();
        var (model, _) = Setup();
        model.FailOnce(error);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ExperimentalDecide.RunAsync(
                new DecideOptions
                {
                    Model = model,
                    State = [new DecisionTextPart("text")],
                    Questions = QuestionMap,
                    MaxRetries = 0,
                    Telemetry = new TelemetryOptions { Integrations = [integration] },
                    GenerateCallId = () => "test-call-id",
                }
            )
        );

        Assert.Same(error, thrown);
        var failed = Assert.Single(integration.Errors);
        Assert.Equal("test-call-id", failed.CallId);
        Assert.Same(error, failed.Error);
        Assert.Equal("7.0.177", failed.AiVersion);
    }

    // .NET-only (the isolation and filtering the review of #1861 asked for; no upstream `it`).
    [Fact]
    public async Task Partial_keeps_the_start_inputs_when_only_outputs_are_hidden_and_isolates_failing_integrations()
    {
        var recording = new RecordingTelemetry();
        var (model, _) = Setup();

        await ExperimentalDecide.RunAsync(
            new DecideOptions
            {
                Model = model,
                State = [new DecisionTextPart("text")],
                Questions = QuestionMap,
                Telemetry = new TelemetryOptions
                {
                    Integrations = [new ThrowingTelemetry(), recording],
                    RecordOutputs = false,
                },
            }
        );

        var start = Assert.Single(recording.DecideStarts);
        Assert.Single(start.State);
        Assert.Equal(QuestionMap, start.Questions);
        Assert.Empty(Assert.Single(recording.DecideEnds).Answers);
    }

    [Fact]
    public async Task Partial_a_hanging_callback_does_not_outlast_the_callers_cancellation()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ExperimentalDecide.RunAsync(
                new DecideOptions
                {
                    Model = Setup().Model,
                    State = [new DecisionTextPart("text")],
                    Questions = QuestionMap,
                    OnStart = _ => new TaskCompletionSource().Task,
                },
                cts.Token
            )
        );
    }

    private sealed class ThrowingTelemetry : ITelemetry
    {
        public Task OnDecideStartAsync(DecideStartEvent @event) => throw new InvalidOperationException("sync");

        public Task OnDecideEndAsync(DecideEndEvent @event) =>
            Task.FromException(new InvalidOperationException("async"));
    }
}
