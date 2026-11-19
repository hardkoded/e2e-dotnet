// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>
/// A telemetry integration (the decide members of the AI SDK's <c>Telemetry</c>, <c>telemetry/telemetry.ts</c>). Every member does nothing by
/// default, so an integration overrides the ones it listens to.
/// </summary>
public interface ITelemetry
{
    /// <summary>Called when an experimental decision operation begins (<c>experimental_onDecideStart</c>).</summary>
    Task OnDecideStartAsync(DecideStartEvent @event) => Task.CompletedTask;

    /// <summary>Called immediately before an experimental decision model call begins (<c>experimental_onDecisionModelCallStart</c>).</summary>
    Task OnDecisionModelCallStartAsync(DecisionModelCallStartEvent @event) => Task.CompletedTask;

    /// <summary>Called after an experimental decision model response has been validated (<c>experimental_onDecisionModelCallEnd</c>).</summary>
    Task OnDecisionModelCallEndAsync(DecisionModelCallEndEvent @event) => Task.CompletedTask;

    /// <summary>Called when an experimental decision operation completes (<c>experimental_onDecideEnd</c>).</summary>
    Task OnDecideEndAsync(DecideEndEvent @event) => Task.CompletedTask;

    /// <summary>Deprecated alias of <see cref="OnDecideStartAsync"/> (<c>experimental_onEvaluateStart</c>); ignored when the integration implements the current member.</summary>
    [Obsolete("Use OnDecideStartAsync.")]
    Task OnEvaluateStartAsync(DecideStartEvent @event) => Task.CompletedTask;

    /// <summary>Deprecated alias of <see cref="OnDecisionModelCallStartAsync"/> (<c>experimental_onEvaluationModelCallStart</c>).</summary>
    [Obsolete("Use OnDecisionModelCallStartAsync.")]
    Task OnEvaluationModelCallStartAsync(DecisionModelCallStartEvent @event) => Task.CompletedTask;

    /// <summary>Deprecated alias of <see cref="OnDecisionModelCallEndAsync"/> (<c>experimental_onEvaluationModelCallEnd</c>).</summary>
    [Obsolete("Use OnDecisionModelCallEndAsync.")]
    Task OnEvaluationModelCallEndAsync(DecisionModelCallEndEvent @event) => Task.CompletedTask;

    /// <summary>Deprecated alias of <see cref="OnDecideEndAsync"/> (<c>experimental_onEvaluateEnd</c>).</summary>
    [Obsolete("Use OnDecideEndAsync.")]
    Task OnEvaluateEndAsync(DecideEndEvent @event) => Task.CompletedTask;

    /// <summary>Called when the operation fails (<c>onError</c>).</summary>
    Task OnErrorAsync(TelemetryErrorEvent @event) => Task.CompletedTask;
}
