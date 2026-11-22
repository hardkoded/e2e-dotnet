// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Mobile.Tests.Screens;

namespace E2E.Mobile.Tests.Engine;

/// <summary>Upstream's <c>describe('viewport')</c> under <c>describe('device fixture')</c> in packages/mobile/tests/unit/engine.test.ts.</summary>
public sealed class DeviceFixtureViewportTests
{
    private static JsonObject SettingsWithViewport(double width, double height)
    {
        var snapshot = FakeDeviceClient.SettingsSnapshot();
        snapshot["viewport"] = new JsonObject { ["width"] = width, ["height"] = height };
        return snapshot;
    }

    [Theory]
    [InlineData(MobilePlatform.Ios)]
    [InlineData(MobilePlatform.Android)]
    // Partial: upstream also asserts `declared.viewport` is `{ kind: 'resource' }`; the harness here has no recorder to read the declared step kind back.
    public async Task Partial_Reads_the_platform_viewport_as_a_recorded_resource_without_capturing_pixels(
        MobilePlatform platform
    )
    {
        var h = Harness.Create(options => options with { Platform = platform });
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => SettingsWithViewport(402, 874));
        var session = await OpenAttempt(h);
        var before = h.Fake.Calls.Count;

        Assert.Equal(new ViewportSize(402, 874), await session.Device.ViewportAsync());
        Assert.Equal([DeviceMethods.CaptureSnapshot], h.Fake.Methods().Skip(before));
    }

    [Fact]
    public async Task Keeps_its_learned_size_isolated_from_the_returned_object_and_reads_a_new_orientation()
    {
        var h = Harness.Create();
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => SettingsWithViewport(390, 844));
        var session = await OpenAttempt(h);
        var device = session.Device;
        Assert.Equal(new ViewportSize(390, 844), await device.ViewportAsync());
        var screenshotsBeforeRotation = h.Fake.Methods().Count(m => m == DeviceMethods.CaptureScreenshot);
        await device.SetOrientationAsync(DeviceOrientation.LandscapeLeft);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => new JsonObject { ["nodes"] = new JsonArray() });
        Assert.Equal(new ViewportSize(390, 844), await device.ViewportAsync());

        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => SettingsWithViewport(844, 390));
        Assert.Equal(new ViewportSize(844, 390), await device.ViewportAsync());
        Assert.Equal(screenshotsBeforeRotation, h.Fake.Methods().Count(m => m == DeviceMethods.CaptureScreenshot));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reuses_logical_screenshot_dimensions_when_landscape_snapshots_have_no_bounds(bool withLogical)
    {
        var h = Harness.Create(pinned: false);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => new JsonObject { ["nodes"] = new JsonArray() });
        h.Fake.Respond(DeviceMethods.CaptureScreenshot, _ =>
        {
            var shot = new JsonObject
            {
                ["width"] = 1170,
                ["height"] = 2532,
                ["pixelDensity"] = 3,
            };
            if (withLogical)
            {
                shot["logicalWidth"] = 390;
                shot["logicalHeight"] = 844;
            }

            return shot;
        });
        var session = await OpenAttempt(h);
        var device = session.Device;
        await device.SetOrientationAsync(DeviceOrientation.LandscapeLeft);

        Assert.Equal(new ViewportSize(390, 844), await device.ViewportAsync());
        Assert.Equal(new ViewportSize(390, 844), await device.ViewportAsync());
        Assert.Single(h.Fake.Methods(), m => m == DeviceMethods.CaptureScreenshot);
    }

    [Fact]
    public async Task Preserves_the_existing_failure_when_neither_the_snapshot_nor_screenshot_establishes_the_viewport()
    {
        var h = Harness.Create(pinned: false);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => new JsonObject { ["nodes"] = new JsonArray() });
        h.Fake.Respond(DeviceMethods.CaptureScreenshot, _ => new JsonObject { ["width"] = 1170, ["height"] = 2532 });
        var session = await OpenAttempt(h);

        var failure = await Assert.ThrowsAsync<EngineException>(() => session.Device.ViewportAsync());
        Assert.Equal("ENGINE_FAILURE", failure.Code);
        Assert.Contains("viewport is unknown", failure.Message);
    }

    // Partial: upstream also changes the operation's timeout between two calls and expects the second call to read the new budget.
    // Here the budget is the session's ActionTimeout, fixed when the attempt starts, so only the shared-budget half is checked.
    [Fact]
    public async Task Partial_Shares_one_action_budget_between_snapshot_and_screenshot()
    {
        var h = Harness.Create(pinned: false);
        var session = await OpenAttempt(h, actionTimeout: TimeSpan.FromMilliseconds(100));
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
        {
            h.Clock.Advance(TimeSpan.FromMilliseconds(60));
            return new JsonObject { ["nodes"] = new JsonArray() };
        });
        h.Fake.RespondAsync(DeviceMethods.CaptureScreenshot, async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(400), h.Clock, token);
            return new JsonObject { ["logicalWidth"] = 390, ["logicalHeight"] = 844 };
        });

        var read = session.Device.ViewportAsync();
        for (var i = 0; i < 100 && !read.IsCompleted; i++)
        {
            h.Clock.Advance(TimeSpan.FromMilliseconds(20));
            await Task.Delay(10);
        }

        var timedOut = await Assert.ThrowsAsync<EngineException>(() => read);
        Assert.Equal("OPERATION_TIMEOUT", timedOut.Code);
        Assert.Contains("device.viewport", timedOut.Message);
    }

    [Fact]
    public async Task Cancels_a_pending_read_and_follows_the_same_fixture_into_a_fresh_teardown_signal()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        h.Fake.RespondAsync(DeviceMethods.CaptureSnapshot, async (_, _) =>
        {
            started.TrySetResult();
            await release.Task;
            return FakeDeviceClient.SettingsSnapshot();
        });
        using var first = new CancellationTokenSource();
        var cancelled = session.Device.ViewportAsync(first.Token);
        await started.Task;
        await first.CancelAsync();
        Assert.Equal("CANCELLED", (await Assert.ThrowsAsync<EngineException>(() => cancelled)).Code);
        release.SetResult();
        var before = h.Fake.Calls.Count;
        await Assert.ThrowsAnyAsync<Exception>(() => session.Device.ViewportAsync(first.Token));
        Assert.Equal(before, h.Fake.Calls.Count);

        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SettingsSnapshot());
        Assert.Equal(new ViewportSize(390, 844), await session.Device.ViewportAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Does_not_start_a_new_transition_wait_for_controls_that_appear_after_the_read()
    {
        var h = Harness.Create(options => options with { TransitionMilliseconds = 120 });
        var session = await OpenAttempt(h);
        await Observed(session, "Back");
        h.Clock.Advance(TimeSpan.FromMilliseconds(150));
        var startedAt = h.Clock.GetUtcNow();
        await session.Device.ViewportAsync();
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SettingsSnapshotWith(
                new RawNode
                {
                    Ref = "@e11",
                    Index = 10,
                    ParentIndex = 0,
                    Depth = 1,
                    Type = "button",
                    Label = "Submit",
                    Rect = new Rect(0, 600, 390, 44),
                }
            ));
        var submit = await Observed(session, "Submit");
        await session.PerformAsync(submit, new LocatorAction.Tap(), OperationContext.Test, CancellationToken.None);

        Assert.True(h.Clock.GetUtcNow() - startedAt < TimeSpan.FromMilliseconds(120));
    }
}
