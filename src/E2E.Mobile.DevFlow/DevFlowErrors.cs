// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;

namespace E2E.Mobile.DevFlow;

/// <summary>
/// What a failed agent call becomes: the <see cref="AgentDeviceError"/> the engine's error translation reads
/// (<see cref="DeviceErrors"/>). What the agent cannot do is <c>UNSUPPORTED_OPERATION</c> and reaches the test as
/// <c>UNSUPPORTED_CAPABILITY</c>; an element the screen no longer has, or a capture the agent calls stale, is
/// <c>ref_not_found</c> and reaches it as a retryable <c>NODE_STALE</c>; an agent that does not answer is
/// <c>SESSION_NOT_FOUND</c>.
/// </summary>
internal static class DevFlowErrors
{
    /// <summary>The <c>details.reason</c> the engine treats as a stale ref.</summary>
    public const string StaleRef = "ref_not_found";

    private static readonly HashSet<string> StaleCodes = new(["stale-capture-epoch", "stale-element-reference", "element-not-found", "capture-changed", "capture-window-mismatch"], StringComparer.Ordinal);

    private static readonly HashSet<string> UnsupportedCodes = new(["unsupported-capability", "unknown-command", "not_supported"], StringComparer.Ordinal);

    public static AgentDeviceError Unsupported(string message) => new("UNSUPPORTED_OPERATION", message);

    public static AgentDeviceError InvalidArgument(string message) => new("INVALID_ARGS", message);

    public static AgentDeviceError Failed(string message) => new("COMMAND_FAILED", message);

    public static AgentDeviceError StaleReference(string reference) =>
        new("COMMAND_FAILED", $"{reference} is not on the newest screen read", new Dictionary<string, object?> { ["reason"] = StaleRef });

    /// <summary>The error for a failed agent call. <paramref name="method"/> is the client method it served.</summary>
    public static AgentDeviceError Translate(DevFlowAgentException failure, string method)
    {
        var details = new Dictionary<string, object?> { ["devflowError"] = failure.Code, ["status"] = (int?)failure.Status };
        if (failure.Status is null)
        {
            return failure.Code == "timeout"
                ? new AgentDeviceError("COMMAND_FAILED", $"{method}: {failure.Message}", details, failure)
                : new AgentDeviceError("SESSION_NOT_FOUND", $"the DevFlow agent is not available: {failure.Message}", details, failure);
        }

        if (failure.Code is { } code && code.Contains("lease", StringComparison.OrdinalIgnoreCase))
        {
            var holderKind = Text(failure.Details?["HolderKind"]) ?? Text(failure.Details?["holderKind"]);
            var label = Text(failure.Details?["Label"]) ?? Text(failure.Details?["label"]);
            var holder = holderKind is null
                ? "no host holds the app's mutation lease any more, so this client's lease lapsed; the next action claims it again"
                : $"another DevFlow host holds the app's mutation lease: {holderKind}{(label is null ? "" : $" ({label})")}; close it or wait for its lease to lapse";
            details["holderKind"] = holderKind;
            details["holderLabel"] = label;
            return new AgentDeviceError("COMMAND_FAILED", $"{method}: {holder}. {failure.Message}", details, failure);
        }

        var notFoundElement = failure.Message.Contains("element not found", StringComparison.OrdinalIgnoreCase)
            || failure.Message.Contains("stale", StringComparison.OrdinalIgnoreCase);
        if (failure.Status == HttpStatusCode.Conflict || (failure.Code is { } stale && StaleCodes.Contains(stale)) || notFoundElement)
        {
            details["reason"] = StaleRef;
            return new AgentDeviceError("COMMAND_FAILED", $"{method}: {failure.Message}", details, failure);
        }

        // A 404 that names no element is a route this agent does not serve: an older agent, or a backend without the feature.
        if (failure.Status is HttpStatusCode.NotImplemented or HttpStatusCode.NotFound || (failure.Code is { } unsupported && UnsupportedCodes.Contains(unsupported)))
        {
            return new AgentDeviceError("UNSUPPORTED_OPERATION", $"{method} is not supported by this DevFlow agent: {failure.Message}", details, failure);
        }

        return new AgentDeviceError("COMMAND_FAILED", $"{method}: {failure.Message}", details, failure);
    }

    private static string? Text(System.Text.Json.Nodes.JsonNode? node) =>
        node is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
