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

    /// <summary>Called when the operation fails (<c>onError</c>).</summary>
    Task OnErrorAsync(TelemetryErrorEvent @event) => Task.CompletedTask;
}

/// <summary>Telemetry configuration (the AI SDK's <c>TelemetryOptions</c>, <c>telemetry/telemetry-options.ts</c>).</summary>
public sealed class TelemetryOptions
{
    /// <summary>Enable or disable telemetry. Enabled by default. Set to <c>false</c> to opt out.</summary>
    public bool? IsEnabled { get; init; }

    /// <summary>
    /// Enable or disable input recording. Enabled by default. You might want to disable input recording to avoid recording sensitive
    /// information, to reduce data transfers, or to increase performance.
    /// </summary>
    public bool? RecordInputs { get; init; }

    /// <summary>
    /// Enable or disable output recording. Enabled by default. You might want to disable output recording to avoid recording sensitive
    /// information, to reduce data transfers, or to increase performance.
    /// </summary>
    public bool? RecordOutputs { get; init; }

    /// <summary>Identifier for this function. Used to group telemetry data by function.</summary>
    public string? FunctionId { get; init; }

    /// <summary>
    /// Top-level runtime context properties that should be included in telemetry. Runtime context properties are excluded unless they are
    /// explicitly set to <c>true</c>.
    /// </summary>
    public IReadOnlyDictionary<string, bool>? IncludeRuntimeContext { get; init; }

    /// <summary>Per-call telemetry integrations that receive lifecycle events during the call.</summary>
    public IReadOnlyList<ITelemetry>? Integrations { get; init; }
}
