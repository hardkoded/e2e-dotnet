// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AttemptSession;
using Microsoft.Playwright;

namespace E2E.Tests.Video;

/// <summary>
/// <c>describe('VideoRecorder')</c> of upstream <c>video.test.ts</c>: the recorder's segment bookkeeping over a scripted page,
/// with one segment per page, a failed stop that still left a file kept, a lost segment reported at stop, and every hook a
/// no-op until armed. The real screencast is covered by the lifecycle test of <c>BrowserProvider/RecordingTests</c>.
/// </summary>
public sealed class VideoRecorderTests : IDisposable
{
    private static readonly (int Width, int Height) Viewport = (320, 200);

    private readonly List<string> _made = [];
    private string _dir = "";

    public void Dispose()
    {
        foreach (var dir in _made)
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Waits_for_a_segments_first_frame_before_ending_it_within_a_bound_so_a_recording_stopped_right_after_a_paint_is_not_blank()
    {
        var video = Recorder();
        var late = new ScriptedPage(firstFrameMs: 150);
        await video.ArmAsync(late.Page);
        var stopping = Environment.TickCount64;
        await video.StopAsync();
        Assert.True(late.StoppedAt[0] - stopping >= 140);

        var silent = new ScriptedPage(firstFrameMs: null);
        await video.ArmAsync(silent.Page);
        var started = Environment.TickCount64;
        await video.StopAsync();

        // A page that never repaints ends after the bound, not never.
        Assert.True(silent.StoppedAt[0] - started >= 990);
        Assert.True(silent.StoppedAt[0] - started < 3000);
    }

    [Fact]
    public async Task Records_one_segment_per_page_in_order_each_with_its_start_instant()
    {
        var video = Recorder();
        var first = new ScriptedPage();
        var second = new ScriptedPage(viewport: (390, 600));
        await video.ArmAsync(first.Page);
        Assert.True(video.IsArmed);
        Assert.Equal([Path.Combine(_dir, "video", "video.webm")], first.Started);

        // A restart ends the segment before its page closes, then opens another page: the next segment.
        await video.PageClosingAsync();
        Assert.Equal(1, first.StoppedCount);
        Assert.True(video.IsArmed);
        await video.PageOpenedAsync(second.Page);
        Assert.Equal([Path.Combine(_dir, "video", "video-part2.webm")], second.Started);
        Assert.Equal([Viewport, (390, 600)], [.. first.Sizes, .. second.Sizes]);
        var segments = await video.StopAsync();
        Assert.Equal(["video/video.webm", "video/video-part2.webm"], segments.Select(segment => segment.Path));
        foreach (var segment in segments)
        {
            Assert.True(DateTimeOffset.TryParse(segment.StartedAt, System.Globalization.CultureInfo.InvariantCulture, out _));
        }

        Assert.False(video.IsArmed);
        Assert.Equal([1, 1], [first.StoppedCount, second.StoppedCount]);
    }

    [Fact]
    public async Task Records_at_the_viewport_size_by_default_and_at_the_configured_size_and_quality_when_given()
    {
        var plain = new ScriptedPage();
        await Recorder().ArmAsync(plain.Page);
        Assert.Equal(Viewport, plain.Sizes[0]);
        Assert.Null(plain.Qualities[0]);
        var tuned = new ScriptedPage();
        await Recorder(new WebScreencastOptions { Size = new WebViewport(640, 400), Quality = 70 }).ArmAsync(tuned.Page);
        Assert.Equal((640, 400), tuned.Sizes[0]);
        Assert.Equal(70, tuned.Qualities[0]);
    }

    [Fact]
    public async Task Keeps_a_segment_whose_stop_failed_but_whose_file_exists()
    {
        var video = Recorder();
        var flaky = new ScriptedPage(stopError: new InvalidOperationException("screencast.stop: protocol error"));
        await video.ArmAsync(flaky.Page);
        Assert.Equal(["video/video.webm"], (await video.StopAsync()).Select(segment => segment.Path));
    }

    [Fact]
    public async Task Reports_a_segment_whose_stop_failed_and_left_no_file_once()
    {
        var video = Recorder();
        var broken = new ScriptedPage(writes: false, stopError: new InvalidOperationException("screencast stop failed"));
        await video.ArmAsync(broken.Page);
        var caught = await Assert.ThrowsAsync<EngineException>(() => video.StopAsync());
        Assert.Contains("video/video.webm", caught.Message, StringComparison.Ordinal);
        Assert.Contains("screencast stop failed", caught.Message, StringComparison.Ordinal);
        Assert.False(video.IsArmed);

        // Reported, then forgotten: the next stop has nothing to say.
        Assert.Empty(await video.StopAsync());
        Assert.False(File.Exists(Path.Combine(_dir, "video", "video.webm")));
    }

    [Fact]
    public async Task Hands_the_segments_that_finalized_to_the_stop_after_one_that_reported_a_lost_segment()
    {
        var video = Recorder();
        var kept = new ScriptedPage();
        var broken = new ScriptedPage(writes: false, stopError: new InvalidOperationException("screencast stop failed"));
        await video.ArmAsync(kept.Page);
        await video.PageClosingAsync();
        await video.PageOpenedAsync(broken.Page);
        var caught = await Assert.ThrowsAsync<EngineException>(() => video.StopAsync());
        Assert.Contains("video/video-part2.webm", caught.Message, StringComparison.Ordinal);

        // A host that retries the stop gets what did finalize, so nothing on disk goes unreported.
        Assert.Equal(["video/video.webm"], (await video.StopAsync()).Select(segment => segment.Path));
    }

    [Fact]
    public async Task Starts_no_segment_for_a_page_opened_after_the_attempt_abandoned_its_recording()
    {
        var video = Recorder();
        var first = new ScriptedPage();
        var late = new ScriptedPage();
        await video.ArmAsync(first.Page);
        await video.AbandonAsync();
        Assert.False(video.IsArmed);
        await video.PageOpenedAsync(late.Page);
        Assert.Empty(late.Started);
    }

    [Fact]
    public async Task Stays_disarmed_when_the_first_segment_cannot_start()
    {
        var video = Recorder();
        var dead = new ScriptedPage(startError: new InvalidOperationException("screencast unavailable"));
        var caught = await Assert.ThrowsAsync<InvalidOperationException>(() => video.ArmAsync(dead.Page));
        Assert.Contains("screencast unavailable", caught.Message, StringComparison.Ordinal);
        Assert.False(video.IsArmed);
        Assert.Empty(await video.StopAsync());
        Assert.Equal(0, dead.StoppedCount);
    }

    [Fact]
    public async Task Runs_every_hook_straight_through_until_armed()
    {
        var video = Recorder();
        var idle = new ScriptedPage();
        await video.PageOpenedAsync(idle.Page);
        Assert.Empty(idle.Started);
        await video.PageClosingAsync();
        Assert.Empty(await video.StopAsync());
    }

    private VideoRecorder Recorder(WebScreencastOptions? options = null)
    {
        _dir = Directory.CreateTempSubdirectory("e2e-video-unit-").FullName;
        _made.Add(_dir);
        return new VideoRecorder(_dir, options);
    }

    /// <summary>
    /// A page whose screencast writes its file on start unless told not to, sends its first frame at once (or after
    /// <c>firstFrameMs</c>, or never with null), and fails to stop when told to.
    /// </summary>
    private sealed class ScriptedPage
    {
        public ScriptedPage(bool writes = true, Exception? stopError = null, Exception? startError = null, (int Width, int Height)? viewport = null, int? firstFrameMs = 0)
        {
            var (page, pageFake) = Fake<IPage>.New();
            var (screencast, screencastFake) = Fake<IScreencast>.New();
            Page = page;
            var size = viewport ?? Viewport;
            pageFake.Members["get_ViewportSize"] = _ => new PageViewportSizeResult { Width = size.Width, Height = size.Height };
            pageFake.Members["get_Screencast"] = _ => screencast;
            screencastFake.Members["StartAsync"] = args =>
            {
                var start = (ScreencastStartOptions)args[0]!;
                if (startError is not null)
                {
                    throw startError;
                }

                Started.Add(start.Path!);
                Sizes.Add((start.Size!.Width, start.Size.Height));
                Qualities.Add(start.Quality);
                if (writes)
                {
                    File.WriteAllText(start.Path!, "webm");
                }

                if (firstFrameMs is { } delay)
                {
                    _ = Task.Delay(delay).ContinueWith(_ => start.OnFrame?.Invoke(new ScreencastFrame()), TaskScheduler.Default);
                }

                return Task.FromResult(FakeRemote.Registration);
            };
            screencastFake.Members["StopAsync"] = _ =>
            {
                StoppedAt.Add(Environment.TickCount64);
                StoppedCount++;
                return stopError is null ? Task.CompletedTask : Task.FromException(stopError);
            };
        }

        public IPage Page { get; }

        public List<string> Started { get; } = [];

        public List<(int Width, int Height)> Sizes { get; } = [];

        public List<int?> Qualities { get; } = [];

        public List<long> StoppedAt { get; } = [];

        public int StoppedCount { get; private set; }
    }
}
