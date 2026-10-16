// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Mobile.Tests.Engine;

public sealed class AScreenAgentDeviceCannotReadYetTests
{
    [Fact]
    public async Task Still_fails_an_observation_on_it()
    {
        var h = Harness.Create(options => options with { Platform = MobilePlatform.Android });
        var session = await h.Boot();
        await session.RestartAsync(CancellationToken.None);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => throw new AgentDeviceError(
            "COMMAND_FAILED",
            "Android snapshot helper returned insufficient foreground app content",
            new Dictionary<string, object?> { ["androidSnapshotHelperFailureReason"] = "content-poor-app-window" }));
        var failure = await Assert.ThrowsAsync<EngineException>(() => session.ObserveAsync(CancellationToken.None));
        Assert.Equal("ENGINE_FAILURE", failure.Code);
        Assert.Contains("insufficient foreground app content", failure.Message, StringComparison.Ordinal);
    }
}
