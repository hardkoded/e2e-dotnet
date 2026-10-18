// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>
/// An attempt's video as its session drives it: armed once the attempt's page is open, told when pages open and close,
/// stopped for its segments when the attempt collects its video, and abandoned when the attempt ends without collecting.
/// <see cref="VideoRecorder"/> is the screencast; <see cref="ProviderVideo"/> is the browser provider's own recording.
/// Upstream's <c>AttemptVideo</c>.
/// </summary>
internal interface IAttemptVideo
{
    /// <summary>True while pages the attempt opens start segments of their own.</summary>
    bool IsArmed { get; }

    Task ArmAsync(IPage page, CancellationToken cancellationToken);

    Task PageOpenedAsync(IPage page);

    Task PageClosingAsync();

    Task<IReadOnlyList<VideoSegment>> StopAsync(CancellationToken cancellationToken);

    /// <summary>Ends a recording the attempt never collected; nothing it recorded is reported.</summary>
    Task AbandonAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The attempt's recording on the Playwright surface. A screencast belongs to one page, so a recording is a series of
/// segments: one per page the attempt shows. <see cref="ArmAsync"/> starts the first segment on the attempt's page; a page
/// about to close (a restart, a context replaced by a state reset) ends its segment first, since Playwright writes nothing
/// for a screencast whose page closed under it, and the next page the attempt opens starts the next segment. Each segment
/// is captured at <see cref="WebScreencastOptions.Size"/> when set, else at its page's viewport size when it starts (the
/// window's under a null viewport, so a page <c>Browser.SetViewportAsync</c> sized is recorded at that size), and carries
/// the instant it began, so a consumer can place step timestamps on it. The first segment is <c>video/video.webm</c>; later
/// ones are <c>video/video-part&lt;n&gt;.webm</c>. Without a recording, every hook here is a no-op.
/// Upstream's <c>VideoRecorder</c>.
/// </summary>
internal sealed class VideoRecorder(string artifactsDir, WebScreencastOptions? options = null) : IAttemptVideo
{
    /// <summary>
    /// How long a segment that has not received a frame yet waits for one before it ends. A screencast sends a frame only
    /// when the page repaints, and on Linux the frame of a page that just painted arrives after the paint: a segment
    /// stopped right after a navigation would otherwise end with no frame, which Playwright writes as a white video.
    /// </summary>
    internal static readonly TimeSpan FirstFrameWait = TimeSpan.FromMilliseconds(1000);

    private readonly WebScreencastOptions _options = options ?? new WebScreencastOptions();

    /// <summary>Set by <see cref="ArmAsync"/>, cleared by <see cref="StopAsync"/>; pages the attempt opens in between start segments.</summary>
    private bool _armed;

    private Segment? _current;

    /// <summary>Segments finished this attempt, in order; <see cref="StopAsync"/> hands them over.</summary>
    private List<VideoFile> _finished = [];

    /// <summary>Segments started this attempt, for their file names.</summary>
    private int _count;

    /// <summary>The first segment whose stop failed and left no file; <see cref="StopAsync"/> reports it.</summary>
    private (string Relative, Exception Cause)? _lost;

    /// <summary>True between <see cref="ArmAsync"/> and <see cref="StopAsync"/>: a page the attempt opens then starts a segment.</summary>
    public bool IsArmed => _armed;

    /// <summary>Starts the recording on <paramref name="page"/>, unless a segment already records, and arms it.</summary>
    public async Task ArmAsync(IPage page, CancellationToken cancellationToken = default)
    {
        if (_current is null)
        {
            await BeginAsync(page).ConfigureAwait(false);
        }

        _armed = true;
    }

    /// <summary>A page the attempt opened: the next segment, when armed.</summary>
    public async Task PageOpenedAsync(IPage page)
    {
        if (_armed)
        {
            await BeginAsync(page).ConfigureAwait(false);
        }
    }

    /// <summary>The recorded page is about to close: its segment ends here, while the page can still flush it.</summary>
    public Task PageClosingAsync() => EndAsync();

    /// <summary>The attempt closes uncollected: disarmed first, so no late page starts a segment, then the one in progress ends and its file is complete on disk.</summary>
    public Task AbandonAsync(CancellationToken cancellationToken = default)
    {
        _armed = false;
        return EndAsync();
    }

    /// <summary>
    /// Ends the recording and returns every segment written this attempt, in order. A segment that was lost (its stop
    /// failed and left no file) is reported instead: the harness records that as a cleanup failure, and the segments that
    /// did finalize stay on disk.
    /// </summary>
    public async Task<IReadOnlyList<VideoFile>> StopAsync(CancellationToken cancellationToken = default)
    {
        await EndAsync().ConfigureAwait(false);
        _armed = false;
        var lost = _lost;
        _lost = null;
        if (lost is { } segment)
        {
            throw new EngineException(
                EngineErrorCodes.EngineFailure,
                $"video segment {segment.Relative} could not be finalized: {WebErrors.Message(segment.Cause)}",
                retryable: false,
                segment.Cause);
        }

        var finished = _finished;
        _finished = [];
        return finished;
    }

    async Task<IReadOnlyList<VideoSegment>> IAttemptVideo.StopAsync(CancellationToken cancellationToken) =>
        await StopAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Starts one segment on a page, a screencast at the page's viewport size. A segment still recording (a page the app
    /// closed on its own) ends first.
    /// </summary>
    private async Task BeginAsync(IPage page)
    {
        await EndAsync().ConfigureAwait(false);
        _count++;
        var name = _count == 1 ? "video" : $"video-part{_count.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        var relative = $"video/{name}.webm";
        var absolute = Path.Combine(artifactsDir, "video", name + ".webm");
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        var size = _options.Size ?? await CurrentViewportAsync(page).ConfigureAwait(false);
        var framed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = new ScreencastStartOptions
        {
            Path = absolute,
            Size = new ScreencastSize { Width = size.Width, Height = size.Height },
            OnFrame = _ =>
            {
                framed.TrySetResult();
                return Task.CompletedTask;
            },
        };
        if (_options.Quality is { } quality)
        {
            start.Quality = quality;
        }

        await page.Screencast.StartAsync(start).ConfigureAwait(false);
        _current = new Segment(page, relative, absolute, DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture), framed.Task);
    }

    /// <summary>
    /// Ends the segment in progress. A stop that failed but left a file keeps the file; one that left nothing (a page
    /// closed before the stop, which Playwright never flushes) lost the segment, which <see cref="StopAsync"/> reports. A
    /// segment that wrote nothing is not a segment.
    /// </summary>
    private async Task EndAsync()
    {
        var segment = _current;
        if (segment is null)
        {
            return;
        }

        _current = null;
        await WaitForFrameAsync(segment, FirstFrameWait).ConfigureAwait(false);
        try
        {
            await segment.Page.Screencast.StopAsync().ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is not OperationCanceledException)
        {
            if (!File.Exists(segment.Absolute))
            {
                _lost ??= (segment.Relative, cause);
                return;
            }
        }

        if (File.Exists(segment.Absolute))
        {
            _finished.Add(new VideoFile { Path = segment.Relative, StartedAt = segment.StartedAt });
        }
    }

    /// <summary>
    /// The page's size in CSS pixels: the emulated viewport, else the window's, measured in the page, since a null
    /// viewport follows the window and Playwright then reports no size of its own. Upstream's <c>currentViewport</c>.
    /// </summary>
    private static async Task<WebViewport> CurrentViewportAsync(IPage page)
    {
        if (page.ViewportSize is { } emulated)
        {
            return new WebViewport(emulated.Width, emulated.Height);
        }

        var measured = await page.EvaluateAsync<System.Text.Json.JsonElement>("() => ({ width: window.innerWidth, height: window.innerHeight })").ConfigureAwait(false);
        return new WebViewport(measured.GetProperty("width").GetInt32(), measured.GetProperty("height").GetInt32());
    }

    /// <summary>Waits up to <paramref name="timeout"/> for the segment's first frame.</summary>
    private static async Task WaitForFrameAsync(Segment segment, TimeSpan timeout)
    {
        try
        {
            await segment.Framed.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // A page that never repaints ends after the bound, not never.
        }
    }

    /// <summary>One segment in progress: the page it records, where its file lands, and whether a frame reached it yet.</summary>
    private sealed record Segment(IPage Page, string Relative, string Absolute, string StartedAt, Task Framed);
}
