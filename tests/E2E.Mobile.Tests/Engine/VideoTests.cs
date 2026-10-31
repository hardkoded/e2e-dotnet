// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Mobile.Tests.Screens;

namespace E2E.Mobile.Tests.Engine;

/// <remarks>
/// Upstream's <c>describe('video')</c> of <c>engine.test.ts</c>. The port's session is one attempt, so the harness's
/// <c>endAttempt</c> is the session's <c>DisposeAsync</c>, and an upstream "next attempt" is a session booted again.
/// </remarks>
public sealed class VideoTests : IDisposable
{
    private readonly string _artifactsDir = Directory.CreateTempSubdirectory("e2e-mobile-video-").FullName;

    public void Dispose() => Directory.Delete(_artifactsDir, recursive: true);

    private string VideoPath => Path.Combine(_artifactsDir, "video", "video.mp4");

    private static JsonObject Selection(string json) => (JsonObject)JsonNode.Parse(json)!;

    private static void Recorder(Harness h, string? finalizeAt = null)
    {
        string? requested = null;
        h.Fake.Respond(DeviceMethods.RecordingRecord, options =>
        {
            if (options!["action"]!.GetValue<string>() == "start")
            {
                requested = options["path"]!.GetValue<string>();
                return new JsonObject { ["recording"] = "started", ["outPath"] = finalizeAt ?? requested, ["sessionStateDir"] = "/tmp", ["showTouches"] = true };
            }

            var written = finalizeAt ?? requested!;
            File.WriteAllText(written, "mp4 bytes");
            return new JsonObject { ["recording"] = "stopped", ["outPath"] = written, ["artifacts"] = new JsonArray(), ["durationMs"] = 1200, ["showTouches"] = true };
        });
    }

    private static int Records(Harness h) => h.Fake.Methods().Count(method => method == DeviceMethods.RecordingRecord);

    private void AssertStart(Harness h, string selection, bool hideTouches = false)
    {
        var expected = Selection(selection);
        expected["action"] = "start";
        expected["path"] = VideoPath;
        expected["quality"] = "medium";
        expected["recordingScope"] = "device";
        if (hideTouches)
        {
            expected["hideTouches"] = true;
        }

        Assert.True(JsonNode.DeepEquals(expected, h.Fake.LastArgs(DeviceMethods.RecordingRecord)), h.Fake.LastArgs(DeviceMethods.RecordingRecord)?.ToJsonString());
    }

    private static void AssertStop(Harness h, string selection)
    {
        var expected = Selection(selection);
        expected["action"] = "stop";
        Assert.True(JsonNode.DeepEquals(expected, h.Fake.LastArgs(DeviceMethods.RecordingRecord)), h.Fake.LastArgs(DeviceMethods.RecordingRecord)?.ToJsonString());
    }

    [Fact]
    public async Task Starts_and_stops_the_device_recorder_into_the_attempt_directory_and_reports_one_segment()
    {
        var h = Harness.Create();
        Recorder(h);
        var session = await OpenAttempt(h, _artifactsDir);

        // Nothing recording yet: nothing to stop, and no device command spent on it.
        Assert.Empty(await session.StopVideoAsync(CancellationToken.None));
        Assert.Equal(0, Records(h));
        await session.StartVideoAsync(CancellationToken.None);
        AssertStart(h, """{"platform":"ios"}""");
        var segments = await session.StopVideoAsync(CancellationToken.None);
        AssertStop(h, """{"platform":"ios"}""");
        var segment = Assert.IsType<VideoFile>(Assert.Single(segments));
        Assert.Equal("video/video.mp4", segment.Path);
        Assert.True(DateTime.TryParse(segment.StartedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _));
        Assert.True(File.Exists(VideoPath));
        await session.DisposeAsync();
        Assert.Equal(2, Records(h));
    }

    [Fact]
    public async Task Records_again_in_the_same_attempt_once_the_runner_moved_the_previous_file_away()
    {
        var h = Harness.Create();
        Recorder(h);
        var session = await OpenAttempt(h, _artifactsDir);
        foreach (var moved in new[] { "first.mp4", "second.mp4" })
        {
            await session.StartVideoAsync(CancellationToken.None);
            var segment = Assert.IsType<VideoFile>(Assert.Single(await session.StopVideoAsync(CancellationToken.None)));
            Assert.Equal("video/video.mp4", segment.Path);
            Assert.True(DateTime.TryParse(segment.StartedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _));
            File.Move(VideoPath, Path.Combine(_artifactsDir, moved));
        }

        Assert.Equal(4, Records(h));

        // Nothing is left recording for the attempt's end to stop.
        await session.DisposeAsync();
        Assert.Equal(4, Records(h));
    }

    [Theory]
    [InlineData(MobilePlatform.Ios, "73329AAC-8EB6-4AAD-B33C-8A8E911CD2A6", """{"platform":"ios","udid":"73329AAC-8EB6-4AAD-B33C-8A8E911CD2A6"}""")]
    [InlineData(MobilePlatform.Android, "emulator-5554", """{"platform":"android","serial":"emulator-5554"}""")]
    [InlineData(MobilePlatform.Ios, "iPhone 17 Pro", """{"platform":"ios","device":"iPhone 17 Pro"}""")]
    public async Task Records_and_stops_on_the_pinned_platform_device_after_closeApp_ends_the_previous_session(MobilePlatform platform, string device, string selection)
    {
        var h = Harness.Create(options => options with { Platform = platform, Device = device });
        Recorder(h);
        var first = await OpenAttempt(h, _artifactsDir);
        await first.CloseAppAsync(CancellationToken.None);
        await first.DisposeAsync();
        var second = await OpenAttempt(h, _artifactsDir);
        await second.StartVideoAsync(CancellationToken.None);
        AssertStart(h, selection);
        await second.StopVideoAsync(CancellationToken.None);
        AssertStop(h, selection);
    }

    [Fact]
    public async Task Records_without_the_touch_indicator_when_videoTouches_is_false()
    {
        var h = Harness.Create(options => options with { VideoTouches = false });
        Recorder(h);
        var session = await OpenAttempt(h, _artifactsDir);
        await session.StartVideoAsync(CancellationToken.None);
        AssertStart(h, """{"platform":"ios"}""", hideTouches: true);
    }

    [Fact]
    public async Task Moves_a_recording_the_device_finalized_elsewhere_into_place_and_stops_a_dangling_one_at_attempt_end()
    {
        var h = Harness.Create();
        var elsewhere = Path.Combine(_artifactsDir, "elsewhere.mp4");
        Recorder(h, elsewhere);
        var session = await OpenAttempt(h, _artifactsDir);
        await session.StartVideoAsync(CancellationToken.None);
        var segments = await session.StopVideoAsync(CancellationToken.None);
        Assert.Equal("video/video.mp4", Assert.IsType<VideoFile>(Assert.Single(segments)).Path);
        Assert.True(File.Exists(VideoPath));
        Assert.False(File.Exists(elsewhere));
        await session.DisposeAsync();

        // A recording still running when the attempt ends is stopped, best-effort.
        var next = await OpenAttempt(h, _artifactsDir);
        await next.StartVideoAsync(CancellationToken.None);
        var before = Records(h);
        await next.DisposeAsync();
        Assert.Equal(before + 1, Records(h));
        AssertStop(h, """{"platform":"ios"}""");
    }

    [Fact]
    public async Task Stops_a_start_that_outlived_its_budget_and_keeps_a_recording_whose_stop_failed_for_endAttempt()
    {
        var h = Harness.Create();
        var release = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stops = 0;
        var failNextStop = false;
        h.Fake.RespondAsync(DeviceMethods.RecordingRecord, (options, _) =>
        {
            if (options!["action"]!.GetValue<string>() == "start")
            {
                reached.TrySetResult();
                return release.Task;
            }

            stops++;
            if (failNextStop)
            {
                failNextStop = false;
                throw new InvalidOperationException("recorder busy");
            }

            return Task.FromResult<JsonNode?>(new JsonObject { ["recording"] = "stopped", ["artifacts"] = new JsonArray(), ["durationMs"] = 0, ["showTouches"] = true });
        });
        var session = await OpenAttempt(h, _artifactsDir);

        // The launch budget expires while the device is still starting the recorder; the recorder comes up anyway, and ending the attempt must stop it.
        using var budget = new CancellationTokenSource();
        var starting = session.StartVideoAsync(budget.Token);
        await reached.Task;
        await budget.CancelAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => starting);
        release.SetResult(new JsonObject { ["recording"] = "started", ["outPath"] = VideoPath, ["sessionStateDir"] = "/tmp", ["showTouches"] = true });
        await session.DisposeAsync();
        Assert.Equal(1, stops);

        // A stop that fails keeps the recording marked, so ending the attempt stops it again.
        var next = await OpenAttempt(h, _artifactsDir);
        await next.StartVideoAsync(CancellationToken.None);
        failNextStop = true;
        await Assert.ThrowsAnyAsync<Exception>(() => next.StopVideoAsync(CancellationToken.None));
        Assert.Equal(2, stops);
        await next.DisposeAsync();
        Assert.Equal(3, stops);
        AssertStop(h, """{"platform":"ios"}""");
    }
}
