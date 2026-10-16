// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using E2E.Mobile;

namespace E2E.TestMu.Mobile;

/// <summary>
/// What a failed Appium command becomes: the <see cref="AgentDeviceError"/> the engine's error translation reads
/// (<see cref="DeviceErrors"/>). A command the device cannot do is <c>UNSUPPORTED_OPERATION</c> and reaches the test
/// as <c>UNSUPPORTED_CAPABILITY</c>; a ref the screen no longer has is <c>ref_not_found</c> and reaches it as a
/// retryable <c>NODE_STALE</c>; a session the hub dropped is <c>SESSION_NOT_FOUND</c>.
/// </summary>
internal static class AppiumErrors
{
    /// <summary>The <c>details.reason</c> the engine treats as a stale ref.</summary>
    public const string StaleRef = "ref_not_found";

    public static AgentDeviceError Unsupported(string message) => new("UNSUPPORTED_OPERATION", message);

    public static AgentDeviceError NotImplemented(string message) => new("NOT_IMPLEMENTED", message);

    public static AgentDeviceError StaleReference(string reference) =>
        new("COMMAND_FAILED", $"{reference} is not on the newest screen read", new Dictionary<string, object?> { ["reason"] = StaleRef });

    public static AgentDeviceError InvalidArgument(string message) => new("INVALID_ARGS", message);

    /// <summary>The error for a failed Appium call. <paramref name="method"/> is the client method it served.</summary>
    public static AgentDeviceError Translate(WebDriverException failure, string method)
    {
        var details = new Dictionary<string, object?> { ["webdriverError"] = failure.Error, ["status"] = (int?)failure.Status };
        switch (failure.Error)
        {
            case "no such element" or "stale element reference":
                details["reason"] = StaleRef;
                return new AgentDeviceError("COMMAND_FAILED", $"{method}: {failure.Message}", details, failure);
            case "invalid session id" or "session not created":
                return new AgentDeviceError("SESSION_NOT_FOUND", $"the TestMu session is not available: {failure.Message}", details, failure);
            case "unsupported operation" or "unknown command" or "unknown method":
                return new AgentDeviceError("UNSUPPORTED_OPERATION", $"{method} is not supported by this device or the TestMu hub: {failure.Message}", details, failure);
        }

        if (failure.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new AgentDeviceError("COMMAND_FAILED", $"the TestMu hub rejected the credentials: {failure.Message}", details, failure);
        }

        return new AgentDeviceError("COMMAND_FAILED", $"{method}: {failure.Error}: {failure.Message}", details, failure);
    }
}
