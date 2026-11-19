// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>
/// The AI SDK's <c>createRestrictedTelemetryDispatcher</c> (<c>decide/restricted-telemetry-dispatcher.ts</c>): the telemetry dispatcher whose
/// <c>onStart</c> and <c>onEnd</c> send the decide start and end events with the runtime context filtered by
/// <see cref="TelemetryOptions.IncludeRuntimeContext"/>. The model-call events and <c>onError</c> are the dispatcher's.
/// </summary>
internal sealed class RestrictedTelemetryDispatcher(TelemetryOptions? telemetry)
{
    private readonly TelemetryDispatcher _dispatcher = new(telemetry);

    /// <summary>The dispatcher's <c>runInTracingChannelSpan</c>.</summary>
    internal Task<T> RunInTracingChannelSpanAsync<T>(string type, DecideStartEvent @event, Func<Task<T>> execute) =>
        _dispatcher.RunInTracingChannelSpanAsync(type, @event, execute);

    /// <summary>The restricted dispatcher's <c>onStart</c>.</summary>
    internal Task OnStartAsync(DecideStartEvent @event) =>
        _dispatcher.OnDecideStartAsync(
            @event with
            {
                RuntimeContext = FilterIncludedContext.Filter(@event.RuntimeContext, telemetry?.IncludeRuntimeContext),
            }
        );

    /// <summary>The restricted dispatcher's <c>onEnd</c>.</summary>
    internal Task OnEndAsync(DecideEndEvent @event) =>
        _dispatcher.OnDecideEndAsync(
            @event with
            {
                RuntimeContext = FilterIncludedContext.Filter(@event.RuntimeContext, telemetry?.IncludeRuntimeContext),
            }
        );

    internal Task OnDecisionModelCallStartAsync(DecisionModelCallStartEvent @event) =>
        _dispatcher.OnDecisionModelCallStartAsync(@event);

    internal Task OnDecisionModelCallEndAsync(DecisionModelCallEndEvent @event) =>
        _dispatcher.OnDecisionModelCallEndAsync(@event);

    internal Task OnErrorAsync(string callId, Exception error) => _dispatcher.OnErrorAsync(callId, error);
}
