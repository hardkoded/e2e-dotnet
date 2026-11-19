// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable CS0618 // the deprecated aliases are the subject of these tests

using E2E.Decision.Sdk;
using static E2E.Decision.Tests.Decide.DecideTestHelpers;

namespace E2E.Decision.Tests.DeprecatedAliases;

/// <summary>The AI SDK's <c>decide/deprecated-aliases.test.ts</c>, the two telemetry <c>it</c>s.</summary>
public sealed class DeprecatedAliasesTests
{
    [Fact]
    public async Task Dispatches_deprecated_telemetry_callbacks_once_with_decision_operation_names()
    {
        var integration = new DeprecatedOnlyTelemetry();
        var (model, _) = Setup();

        await ExperimentalDecide.RunAsync(
            new DecideOptions
            {
                Model = model,
                State = [],
                Questions = QuestionMap,
                Telemetry = new TelemetryOptions { Integrations = [integration] },
            }
        );

        Assert.Equal(["ai.decide", "ai.decide.doDecide", "ai.decide.doDecide", "ai.decide"], integration.OperationIds);
        Assert.Equal(
            [
                nameof(ITelemetry.OnEvaluateStartAsync),
                nameof(ITelemetry.OnEvaluationModelCallStartAsync),
                nameof(ITelemetry.OnEvaluationModelCallEndAsync),
                nameof(ITelemetry.OnEvaluateEndAsync),
            ],
            integration.Calls
        );
    }

    [Fact]
    public async Task Prefers_each_decision_telemetry_callback_over_its_deprecated_alias()
    {
        var integration = new CurrentAndDeprecatedTelemetry();
        var (model, _) = Setup();

        await ExperimentalDecide.RunAsync(
            new DecideOptions
            {
                Model = model,
                State = [],
                Questions = QuestionMap,
                Telemetry = new TelemetryOptions { Integrations = [integration] },
            }
        );

        Assert.Equal(4, integration.Current);
        Assert.Equal(0, integration.Deprecated);
    }

    private sealed class DeprecatedOnlyTelemetry : ITelemetry
    {
        public List<string> Calls { get; } = [];

        public List<string> OperationIds { get; } = [];

        public Task OnEvaluateStartAsync(DecideStartEvent @event) =>
            Record(nameof(OnEvaluateStartAsync), @event.OperationId);

        public Task OnEvaluationModelCallStartAsync(DecisionModelCallStartEvent @event) =>
            Record(nameof(OnEvaluationModelCallStartAsync), @event.OperationId);

        public Task OnEvaluationModelCallEndAsync(DecisionModelCallEndEvent @event) =>
            Record(nameof(OnEvaluationModelCallEndAsync), @event.OperationId);

        public Task OnEvaluateEndAsync(DecideEndEvent @event) => Record(nameof(OnEvaluateEndAsync), @event.OperationId);

        private Task Record(string call, string operationId)
        {
            Calls.Add(call);
            OperationIds.Add(operationId);
            return Task.CompletedTask;
        }
    }

    private sealed class CurrentAndDeprecatedTelemetry : ITelemetry
    {
        public int Current { get; private set; }

        public int Deprecated { get; private set; }

        public Task OnDecideStartAsync(DecideStartEvent @event) => Count(current: true);

        public Task OnEvaluateStartAsync(DecideStartEvent @event) => Count(current: false);

        public Task OnDecisionModelCallStartAsync(DecisionModelCallStartEvent @event) => Count(current: true);

        public Task OnEvaluationModelCallStartAsync(DecisionModelCallStartEvent @event) => Count(current: false);

        public Task OnDecisionModelCallEndAsync(DecisionModelCallEndEvent @event) => Count(current: true);

        public Task OnEvaluationModelCallEndAsync(DecisionModelCallEndEvent @event) => Count(current: false);

        public Task OnDecideEndAsync(DecideEndEvent @event) => Count(current: true);

        public Task OnEvaluateEndAsync(DecideEndEvent @event) => Count(current: false);

        private Task Count(bool current)
        {
            if (current)
            {
                Current++;
            }
            else
            {
                Deprecated++;
            }

            return Task.CompletedTask;
        }
    }
}
