// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Mobile.Tests.Screens;

namespace E2E.Mobile.Tests.Engine;

public sealed class AScreenAgentDeviceCannotReadYetTests
{
    private static readonly LocatorExpression About = LocatorExpression.For("text", TextMatch.FromString("About"), new TextMatchOptions(), "value");

    private static AgentDeviceError InsufficientContent() => new(
        "COMMAND_FAILED",
        "Android snapshot helper returned insufficient foreground app content",
        new Dictionary<string, object?> { ["androidSnapshotHelperFailureReason"] = "content-poor-app-window" });

    [Fact]
    public async Task Locates_nothing_on_it_so_the_poll_waits_for_the_app_to_render()
    {
        var h = Harness.Create(options => options with { Platform = MobilePlatform.Android });
        var session = await h.Boot();
        await session.RestartAsync(CancellationToken.None);
        var before = h.Fake.Calls.Count;
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => throw InsufficientContent());
        Assert.Empty(await session.LocateAsync(About, TimeSpan.FromSeconds(5), CancellationToken.None));
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SettingsSnapshot());
        Assert.Single(await session.LocateAsync(About, TimeSpan.FromSeconds(5), CancellationToken.None));
        JsonAssert.Calls(
            h.Fake.Calls,
            before,
            (DeviceMethods.CaptureSnapshot, """{"interactiveOnly":false}"""),
            (DeviceMethods.CaptureSnapshot, """{"interactiveOnly":false}"""));
    }

    [Fact]
    public async Task Fails_a_locate_whose_capture_spent_the_budget_with_the_reason_so_the_wait_ends_saying_why()
    {
        var h = Harness.Create(options => options with { Platform = MobilePlatform.Android });
        var session = await h.Boot();
        await session.RestartAsync(CancellationToken.None);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
        {
            h.Clock.Advance(TimeSpan.FromSeconds(3));
            throw InsufficientContent();
        });
        var failure = await Assert.ThrowsAsync<EngineException>(() => session.LocateAsync(About, TimeSpan.FromSeconds(1), CancellationToken.None));
        Assert.Equal("ENGINE_FAILURE", failure.Code);
        Assert.Contains("insufficient foreground app content", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Locates_nothing_on_an_iOS_app_not_running_yet_after_a_launch_and_fails_on_one_that_exits_later()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        Task<IReadOnlyList<SemanticNode>> Locate() => session.LocateAsync(About, TimeSpan.FromSeconds(5), CancellationToken.None);
        AgentDeviceError NotRunning() => new(
            "COMMAND_FAILED",
            "app 'com.foo' is not running",
            new Dictionary<string, object?> { ["runnerErrorCode"] = "APP_NOT_RUNNING" });
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => throw NotRunning());
        Assert.Empty(await Locate());
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SettingsSnapshot());
        Assert.Single(await Locate());
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => throw NotRunning());
        var failure = await Assert.ThrowsAsync<EngineException>(Locate);
        Assert.Equal("ENGINE_FAILURE", failure.Code);
    }

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

    [Fact]
    public async Task Fails_a_locate_on_any_other_snapshot_failure()
    {
        var h = Harness.Create(options => options with { Platform = MobilePlatform.Android });
        var session = await h.Boot();
        await session.RestartAsync(CancellationToken.None);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => throw new AgentDeviceError("COMMAND_FAILED", "adb went away"));
        var failure = await Assert.ThrowsAsync<EngineException>(() => session.LocateAsync(About, TimeSpan.FromSeconds(5), CancellationToken.None));
        Assert.Equal("ENGINE_FAILURE", failure.Code);
    }
}
