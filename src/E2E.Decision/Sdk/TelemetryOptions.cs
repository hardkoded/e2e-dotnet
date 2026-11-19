// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

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
