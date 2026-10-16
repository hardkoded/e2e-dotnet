// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Mobile.Tests.Errors;

// .NET-only: upstream adds nothing to AMBIGUOUS_MATCH, so no upstream `it` covers these.
public sealed class AmbiguousMatchTests
{
    private const string Message = "10 devices match this request equally; select one explicitly.";

    private static AgentDeviceError Ambiguous(string? hint)
    {
        var details = new Dictionary<string, object?> { ["devices"] = new[] { "iPhone 17", "iPhone 17" } };
        if (hint is not null)
        {
            details["hint"] = hint;
        }

        return new AgentDeviceError("AMBIGUOUS_MATCH", Message, details);
    }

    [Fact]
    public void Adds_the_option_that_selects_a_device_after_the_hint_agent_device_declared()
    {
        var error = Assert.IsType<EngineException>(DeviceErrors.TranslateError(Ambiguous("Pass --udid <id> or --device <name>."), "boot"));
        Assert.Equal("ENGINE_FAILURE", error.Code);
        Assert.Equal(
            $"boot failed: {Message} Hint: Pass --udid <id> or --device <name>. Set MobileOptions.Device to a device id, or boot exactly one device.",
            error.Message);
        var cause = Assert.IsType<AgentDeviceError>(error.InnerException);
        Assert.Equal("AMBIGUOUS_MATCH", cause.Code);
        Assert.Equal(Message, cause.Message);
        Assert.Contains("devices", cause.Details!.Keys);
    }

    [Fact]
    public void Adds_the_option_when_agent_device_declared_no_hint()
    {
        var error = Assert.IsType<EngineException>(DeviceErrors.TranslateError(Ambiguous(null), "boot"));
        Assert.Equal($"boot failed: {Message} Set MobileOptions.Device to a device id, or boot exactly one device.", error.Message);
    }

    [Fact]
    public void Leaves_other_codes_without_the_line()
    {
        var error = Assert.IsType<EngineException>(DeviceErrors.TranslateError(new AgentDeviceError("COMMAND_FAILED", "xcrun exploded"), "boot"));
        Assert.Equal("boot failed: xcrun exploded", error.Message);
    }
}
