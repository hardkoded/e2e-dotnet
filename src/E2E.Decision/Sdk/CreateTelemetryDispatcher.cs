// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>
/// The AI SDK's <c>createTelemetryDispatcher</c> (<c>telemetry/create-telemetry-dispatcher.ts</c>) for the decide events: it adds the call's
/// telemetry settings and the SDK version to each event (<c>augmentEvent</c>), filters it (<c>filterTelemetryEvent</c>), and merges the
/// integrations' callbacks (<c>mergeCallbacks</c>).
/// </summary>
internal sealed class TelemetryDispatcher
{
    /// <summary>The AI SDK version the events report (<c>VERSION</c>).</summary>
    internal const string AiVersion = "7.0.177";

    private readonly TelemetryOptions? _telemetry;

    private readonly IReadOnlyList<ITelemetry> _integrations;

    /// <summary>
    /// Creates the dispatcher. When telemetry is explicitly disabled, it has no integrations. A call with no
    /// <see cref="TelemetryOptions.Integrations"/> uses the global integrations of <see cref="TelemetryRegistry"/>.
    /// </summary>
    internal TelemetryDispatcher(TelemetryOptions? telemetry)
    {
        _telemetry = telemetry;
        _integrations =
            telemetry?.IsEnabled == false
                ? []
                : telemetry?.Integrations ?? TelemetryRegistry.GetGlobalTelemetryIntegrations();
    }

    /// <summary>
    /// <c>runInTracingChannelSpan</c>: runs <paramref name="execute"/> inside the AI SDK tracing channel, publishing the start event with
    /// the telemetry settings, the SDK version and the runtime context filtered by <see cref="TelemetryOptions.IncludeRuntimeContext"/>
    /// (<c>augmentEvent</c> with <c>filterContext</c>). The call's result is published only when inputs and outputs are both recorded.
    /// </summary>
    internal Task<T> RunInTracingChannelSpanAsync<T>(string type, DecideStartEvent @event, Func<Task<T>> execute) =>
        TracingChannelPublisher.RunWithTracingChannelSpanAsync(
            new TelemetryTracingChannelMessage
            {
                Type = type,
                Event = Augment(@event) with
                {
                    RuntimeContext = FilterIncludedContext.Filter(
                        @event.RuntimeContext,
                        _telemetry?.IncludeRuntimeContext
                    ),
                },
            },
            execute,
            // Raw operation results may contain both inputs and outputs.
            _telemetry?.RecordInputs != false
                && _telemetry?.RecordOutputs != false
        );

    /// <summary><c>experimental_onDecideStart</c>.</summary>
    internal Task OnDecideStartAsync(DecideStartEvent @event)
    {
        var augmented = FilterTelemetryEvent.Filter(Augment(@event), _telemetry);
        return MergeCallbacks(integration => integration.OnDecideStartAsync(augmented));
    }

    /// <summary><c>experimental_onDecideEnd</c>.</summary>
    internal Task OnDecideEndAsync(DecideEndEvent @event)
    {
        var augmented = FilterTelemetryEvent.Filter(Augment(@event), _telemetry);
        return MergeCallbacks(integration => integration.OnDecideEndAsync(augmented));
    }

    /// <summary><c>experimental_onDecisionModelCallStart</c>.</summary>
    internal Task OnDecisionModelCallStartAsync(DecisionModelCallStartEvent @event)
    {
        var augmented = FilterTelemetryEvent.Filter(Augment(@event), _telemetry);
        return MergeCallbacks(integration => integration.OnDecisionModelCallStartAsync(augmented));
    }

    /// <summary><c>experimental_onDecisionModelCallEnd</c>.</summary>
    internal Task OnDecisionModelCallEndAsync(DecisionModelCallEndEvent @event)
    {
        var augmented = FilterTelemetryEvent.Filter(Augment(@event), _telemetry);
        return MergeCallbacks(integration => integration.OnDecisionModelCallEndAsync(augmented));
    }

    /// <summary><c>onError</c>.</summary>
    internal Task OnErrorAsync(string callId, Exception error)
    {
        var augmented = FilterTelemetryEvent.Filter(
            Augment(new TelemetryErrorEvent { CallId = callId, Error = error }),
            _telemetry
        );
        return MergeCallbacks(integration => integration.OnErrorAsync(augmented));
    }

    /// <summary>
    /// The AI SDK's <c>mergeCallbacks</c>: runs the integrations in parallel, and a failing one, even one that throws before it returns a
    /// task, does not stop the others.
    /// </summary>
    private async Task MergeCallbacks(Func<ITelemetry, Task> call)
    {
        await Task.WhenAll(_integrations.Select(integration => IsolatedAsync(call, integration)).ToList())
            .ConfigureAwait(false);
    }

    private static async Task IsolatedAsync(Func<ITelemetry, Task> call, ITelemetry integration)
    {
        await Task.Yield();
        await call(integration).ConfigureAwait(false);
    }

    private DecideStartEvent Augment(DecideStartEvent @event) =>
        @event with
        {
            RecordInputs = _telemetry?.RecordInputs,
            RecordOutputs = _telemetry?.RecordOutputs,
            FunctionId = _telemetry?.FunctionId,
            AiVersion = AiVersion,
        };

    private DecideEndEvent Augment(DecideEndEvent @event) =>
        @event with
        {
            RecordInputs = _telemetry?.RecordInputs,
            RecordOutputs = _telemetry?.RecordOutputs,
            FunctionId = _telemetry?.FunctionId,
            AiVersion = AiVersion,
        };

    private DecisionModelCallStartEvent Augment(DecisionModelCallStartEvent @event) =>
        @event with
        {
            RecordInputs = _telemetry?.RecordInputs,
            RecordOutputs = _telemetry?.RecordOutputs,
            FunctionId = _telemetry?.FunctionId,
            AiVersion = AiVersion,
        };

    private DecisionModelCallEndEvent Augment(DecisionModelCallEndEvent @event) =>
        @event with
        {
            RecordInputs = _telemetry?.RecordInputs,
            RecordOutputs = _telemetry?.RecordOutputs,
            FunctionId = _telemetry?.FunctionId,
            AiVersion = AiVersion,
        };

    private TelemetryErrorEvent Augment(TelemetryErrorEvent @event) =>
        @event with
        {
            RecordInputs = _telemetry?.RecordInputs,
            RecordOutputs = _telemetry?.RecordOutputs,
            FunctionId = _telemetry?.FunctionId,
            AiVersion = AiVersion,
        };
}
