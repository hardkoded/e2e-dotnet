// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Mobile.Tests.RawNodes;
using static E2E.Mobile.Tests.Screens;

namespace E2E.Mobile.Tests.Engine;

public sealed class SessionHooksViewportSwipeLocationArtifactsTests : IDisposable
{
    private readonly string _artifactsDir = Directory.CreateTempSubdirectory("e2e-mobile-artifacts-").FullName;

    public void Dispose()
    {
        Directory.Delete(_artifactsDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static async Task<string> Location(DeviceSession session) => (await session.ObserveAsync(CancellationToken.None)).Route;

    private static JsonObject PinOnly() => FakeDeviceClient.NodesOnly([N("e1", type: "SecureTextField", label: "PIN", value: "1234")]);

    /// <summary>Answers the screenshot with a file of these bytes and remembers where the engine asked for it.</summary>
    private static void Writes(Harness h, byte[] png, List<string> files) =>
        h.Fake.Respond(DeviceMethods.CaptureScreenshot, options =>
        {
            var file = options!["path"]!.GetValue<string>();
            files.Add(file);
            File.WriteAllBytes(file, png);
            return new JsonObject { ["path"] = file };
        });

    [Fact]
    public async Task Locates_the_observation_on_the_foreground_app_and_the_screen_title_and_omits_what_it_does_not_know()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        Assert.Equal("com.apple.Preferences / General", await Location(session));

        // No title bar: the app alone. The app identity is remembered from the launch when the snapshot omits it.
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.NodesOnly([N("@e1", type: "button", label: "Go")]));
        Assert.Equal("com.apple.Preferences", await Location(session));
        var other = FakeDeviceClient.SettingsSnapshot();
        other["appBundleId"] = "other.app";
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => other);
        Assert.Equal("other.app / General", await Location(session));

        // Nothing pinned, nothing opened, no title: no location, and never an invented URL.
        var cold = Harness.Create(pinned: false);
        var coldSession = await OpenAttempt(cold);
        cold.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.NodesOnly([N("@e1", type: "button", rect: new Rect(0, 0, 10, 10))]));
        Assert.Equal("", await Location(coldSession));
    }

    [Fact]
    public async Task Numbers_screenshots_per_attempt_masks_secure_fields_in_them_and_refuses_an_unmaskable_one()
    {
        var h = Harness.Create();

        // A third of the 390x844 viewport: the mask scales the Password field's bounds (y=270, 44 tall) to the image.
        var files = new List<string>();
        Writes(h, PngCodec.Encode(PngCodec.Solid(130, 282, 3, 200)), files);
        var session = await OpenAttempt(h, _artifactsDir);
        Assert.Equal("screenshots/001-first_shot.png", await session.ScreenshotArtifactAsync("first shot", CancellationToken.None));
        Assert.Equal("screenshots/002-screenshot.png", await session.ScreenshotArtifactAsync(null, CancellationToken.None));
        var written = PngCodec.Decode(await File.ReadAllBytesAsync(Path.Combine(_artifactsDir, "screenshots", "002-screenshot.png")));
        byte[] At(int x, int y)
        {
            var start = ((y * written.Width) + x) * written.Channels;
            return written.Pixels[start..(start + 3)];
        }

        Assert.Equal([0, 0, 0], At(33, 96));
        Assert.Equal([200, 200, 200], At(33, 80));

        // The next attempt is a new session, and numbers its screenshots from one.
        await session.DisposeAsync();
        var next = await OpenAttempt(h, _artifactsDir);
        Assert.Equal("screenshots/001-again.png", await next.ScreenshotArtifactAsync("again", CancellationToken.None));

        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => PinOnly());
        var leak = await Assert.ThrowsAsync<EngineException>(() => next.ScreenshotArtifactAsync("leak", CancellationToken.None));
        Assert.Equal("ENGINE_FAILURE", leak.Code);
        Assert.False(File.Exists(Path.Combine(_artifactsDir, "screenshots", "002-leak.png")));
        Assert.Equal(files.Count, files.Distinct().Count());
        foreach (var file in files)
        {
            Assert.False(File.Exists(file));
            Assert.False(Directory.Exists(Path.GetDirectoryName(file)));
        }
    }

    [Fact]
    public async Task Removes_temporary_screenshots_after_a_capture_failure()
    {
        var h = Harness.Create();
        string? file = null;
        h.Fake.Respond(DeviceMethods.CaptureScreenshot, options =>
        {
            file = options!["path"]!.GetValue<string>();
            File.WriteAllBytes(file, [1, 2, 3]);
            throw new AgentDeviceError("COMMAND_FAILED", "capture failed after writing");
        });
        var session = await OpenAttempt(h);
        var failure = await Assert.ThrowsAsync<EngineException>(() => session.ScreenshotAsync(CancellationToken.None));
        Assert.Equal("ENGINE_FAILURE", failure.Code);
        Assert.NotNull(file);
        Assert.False(File.Exists(file));
        Assert.False(Directory.Exists(Path.GetDirectoryName(file)));
    }

    // .NET-only: upstream's client takes no abort signal, so a cancel cannot end its request. The port's HTTP client would.
    [Fact]
    public async Task Keeps_a_cancelled_command_in_flight_until_the_daemon_answers_even_when_the_client_honours_its_token()
    {
        var h = Harness.Create();
        var pressed = new TaskCompletionSource<JsonNode?>();
        var received = new TaskCompletionSource<CancellationToken>();
        h.Fake.RespondAsync(DeviceMethods.InteractionsPress, (_, token) =>
        {
            received.TrySetResult(token);
            return pressed.Task.WaitAsync(token);
        });
        var session = await OpenAttempt(h);
        var about = await Observed(session, "About");
        using var controller = new CancellationTokenSource();
        var pending = session.PerformAsync(about, new LocatorAction.Tap(), controller.Token);
        var sent = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await controller.CancelAsync();
        var cancelled = await Assert.ThrowsAsync<EngineException>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal("CANCELLED", cancelled.Code);

        // The request was not aborted with the caller, and the next attempt still waits for the daemon.
        Assert.False(sent.IsCancellationRequested);
        await session.DisposeAsync();
        var next = h.Boot();
        await Task.Delay(100);
        Assert.False(next.IsCompleted);
        pressed.SetResult(new JsonObject());
        await next;
    }

    [Fact]
    public async Task Waits_for_an_abandoned_command_to_settle_before_the_next_attempt_starts_and_its_launch_goes_out()
    {
        var h = Harness.Create();
        var pressed = new TaskCompletionSource<JsonNode?>();
        var settled = false;
        h.Fake.RespondAsync(DeviceMethods.InteractionsPress, (_, _) => pressed.Task);
        bool? openedAfterSettle = null;
        h.Fake.Respond(DeviceMethods.AppsOpen, _ =>
        {
            openedAfterSettle = settled;
            return new JsonObject { ["appName"] = "Settings", ["appBundleId"] = "com.apple.Preferences" };
        });
        var session = await OpenAttempt(h);
        var about = await Observed(session, "About");
        using var controller = new CancellationTokenSource();
        var pending = session.PerformAsync(about, new LocatorAction.Tap(), controller.Token);
        await controller.CancelAsync();
        var cancelled = await Assert.ThrowsAsync<EngineException>(() => pending);
        Assert.Equal("CANCELLED", cancelled.Code);
        await session.DisposeAsync();

        var next = h.Boot();
        Assert.False(next.IsCompleted);
        settled = true;
        pressed.SetResult(new JsonObject());
        var second = await next;
        await second.RestartAsync(CancellationToken.None);
        Assert.True(openedAfterSettle);

        // A command that never settles fails the launch within its budget instead of racing it.
        h.Fake.RespondAsync(DeviceMethods.InteractionsPress, (_, _) => new TaskCompletionSource<JsonNode?>().Task);
        var stuck = await Observed(second, "About");
        using var abort = new CancellationTokenSource();
        var hung = second.PerformAsync(stuck, new LocatorAction.Tap(), abort.Token);
        await abort.CancelAsync();
        var abandoned = await Assert.ThrowsAsync<EngineException>(() => hung);
        Assert.Equal("CANCELLED", abandoned.Code);
        await second.DisposeAsync();
        using var launch = new CancellationTokenSource();
        var launching = h.Boot(cancellationToken: launch.Token);
        await launch.CancelAsync();
        var launchFailure = await Assert.ThrowsAsync<EngineException>(() => launching);
        Assert.Equal("CANCELLED", launchFailure.Code);
    }

    [Fact]
    public async Task Cleans_up_a_screenshot_that_fails_after_cancellation()
    {
        var h = Harness.Create();
        var started = new TaskCompletionSource<string>();
        var finish = new TaskCompletionSource();
        h.Fake.RespondAsync(DeviceMethods.CaptureScreenshot, async (options, _) =>
        {
            var file = options!["path"]!.GetValue<string>();
            started.SetResult(file);
            await finish.Task;
            File.WriteAllBytes(file, [1, 2, 3]);
            throw new AgentDeviceError("COMMAND_FAILED", "late capture failure");
        });
        var session = await OpenAttempt(h);
        using var controller = new CancellationTokenSource();
        var pending = session.ScreenshotAsync(controller.Token);
        var file = await started.Task;
        await controller.CancelAsync();
        var cancelled = await Assert.ThrowsAsync<EngineException>(() => pending);
        Assert.Equal("CANCELLED", cancelled.Code);

        // The capture was only abandoned: it fails after the caller left, and its temporary files go with it.
        finish.SetResult();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while ((File.Exists(file) || Directory.Exists(Path.GetDirectoryName(file))) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.False(File.Exists(file));
        Assert.False(Directory.Exists(Path.GetDirectoryName(file)));
    }
}
