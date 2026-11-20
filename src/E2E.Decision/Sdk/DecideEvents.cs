// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Sdk;

/// <summary>
/// What a telemetry integration adds to every event it receives (the AI SDK's <c>augmentEvent</c> of <c>create-telemetry-dispatcher.ts</c>):
/// the call's telemetry settings and the SDK version. Null on an event a caller's <c>OnStart</c> or <c>OnEnd</c> callback receives.
/// </summary>
public abstract record TelemetryEvent
{
    /// <summary>Whether the call records inputs (<see cref="TelemetryOptions.RecordInputs"/>).</summary>
    public bool? RecordInputs { get; init; }

    /// <summary>Whether the call records outputs (<see cref="TelemetryOptions.RecordOutputs"/>).</summary>
    public bool? RecordOutputs { get; init; }

    /// <summary>The call's <see cref="TelemetryOptions.FunctionId"/>.</summary>
    public string? FunctionId { get; init; }

    /// <summary>The AI SDK version (<c>VERSION</c>).</summary>
    public string? AiVersion { get; init; }
}

/// <summary>
/// The event passed to <see cref="DecideOptions.OnStart"/> (the AI SDK's <c>DecideStartEvent</c>), when the operation begins, before the decision
/// model is called.
/// </summary>
public record DecideStartEvent : TelemetryEvent
{
    /// <summary>User-defined runtime context.</summary>
    public required IReadOnlyDictionary<string, object?> RuntimeContext { get; init; }

    /// <summary>Unique identifier for this decision call, used to correlate events.</summary>
    public required string CallId { get; init; }

    /// <summary>Identifies the operation type (<c>ai.decide</c>).</summary>
    public string OperationId => "ai.decide";

    /// <summary>The provider identifier.</summary>
    public required string Provider { get; init; }

    /// <summary>The decision model identifier.</summary>
    public required string ModelId { get; init; }

    /// <summary>The shared state used to make decisions.</summary>
    public required IReadOnlyList<DecisionStatePart> State { get; init; }

    /// <summary>The questions to decide against the shared state.</summary>
    public required IReadOnlyDictionary<string, DecisionQuestion> Questions { get; init; }

    /// <summary>Maximum output tokens requested for the decision.</summary>
    public int? MaxOutputTokens { get; init; }

    /// <summary>Reasoning effort requested for the decision.</summary>
    public string? Reasoning { get; init; }

    /// <summary>Maximum number of retries for the decision model call.</summary>
    public required int MaxRetries { get; init; }

    /// <summary>Additional HTTP headers sent with the request.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>Additional provider-specific options.</summary>
    public required IReadOnlyDictionary<string, JsonElement> ProviderOptions { get; init; }
}

/// <summary>
/// The event passed to <see cref="DecideOptions.OnEnd"/> (the AI SDK's <c>DecideEndEvent</c>), when the operation completes successfully.
/// The result's <c>response</c> is flattened as on <see cref="DecisionResult"/>.
/// </summary>
public record DecideEndEvent : DecideStartEvent
{
    /// <summary>Exactly one typed answer per question ID.</summary>
    public required IReadOnlyDictionary<string, DecisionAnswer> Answers { get; init; }

    /// <summary>Token usage for the decision operation.</summary>
    public required DecisionUsage Usage { get; init; }

    /// <summary>Warnings from the decision model.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>Provider-declared decimal precision for probabilities and scores.</summary>
    public DecisionRounding? Rounding { get; init; }

    /// <summary>Optional provider-specific metadata.</summary>
    public JsonElement? ProviderMetadata { get; init; }

    /// <summary>The resolved model ID of the response.</summary>
    public required string ResponseModelId { get; init; }

    /// <summary>The provider's response headers (upstream's <c>response.headers</c>), lowercase names.</summary>
    public IReadOnlyDictionary<string, string>? ResponseHeaders { get; init; }

    /// <summary>The provider's response body, as it sent it (upstream's <c>response.body</c>).</summary>
    public JsonElement? ResponseBody { get; init; }

    /// <summary>The timestamp of the response.</summary>
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// The event fired when the decision model call begins (the AI SDK's <c>DecisionModelCallStartEvent</c>). The logical model call includes any
/// provider retries.
/// </summary>
public record DecisionModelCallStartEvent : TelemetryEvent
{
    /// <summary>Unique identifier for the outer decision call.</summary>
    public required string CallId { get; init; }

    /// <summary>Identifies the inner operation (<c>ai.decide.doDecide</c>).</summary>
    public string OperationId => "ai.decide.doDecide";

    /// <summary>The provider identifier.</summary>
    public required string Provider { get; init; }

    /// <summary>The decision model identifier.</summary>
    public required string ModelId { get; init; }

    /// <summary>The shared state used to make decisions.</summary>
    public required IReadOnlyList<DecisionStatePart> State { get; init; }

    /// <summary>The questions to decide against the shared state.</summary>
    public required IReadOnlyDictionary<string, DecisionQuestion> Questions { get; init; }
}

/// <summary>
/// The event fired after the decision model response has been validated (the AI SDK's <c>DecisionModelCallEndEvent</c>): the result of the
/// logical model call, including any retries.
/// </summary>
public record DecisionModelCallEndEvent : DecisionModelCallStartEvent
{
    /// <summary>Exactly one answer per question ID.</summary>
    public required IReadOnlyDictionary<string, DecisionAnswer> Answers { get; init; }

    /// <summary>Token usage reported by the decision model.</summary>
    public DecisionUsage? Usage { get; init; }

    /// <summary>Warnings from the decision model.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>Provider-declared decimal precision for probabilities and scores.</summary>
    public DecisionRounding? Rounding { get; init; }

    /// <summary>Optional provider-specific metadata.</summary>
    public JsonElement? ProviderMetadata { get; init; }

    /// <summary>The id of the model that served the call, when the response names one.</summary>
    public string? ResponseModelId { get; init; }
}

/// <summary>The event a telemetry integration's <see cref="ITelemetry.OnErrorAsync"/> receives when the operation fails.</summary>
public sealed record TelemetryErrorEvent : TelemetryEvent
{
    /// <summary>Unique identifier for the decision call.</summary>
    public required string CallId { get; init; }

    /// <summary>The error the call failed with.</summary>
    public required Exception Error { get; init; }
}
