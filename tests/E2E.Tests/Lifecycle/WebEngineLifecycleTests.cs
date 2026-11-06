// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;
using Microsoft.Playwright;

namespace E2E.Tests.Lifecycle;

[Collection(BrowserCollection.Name)]
public sealed class WebEngineLifecycleTests
{
    [Fact]
    public async Task Serves_consecutive_attempts_from_one_browser_and_survives_an_attempt_close()
    {
        using var site = await FixtureApp.StartAsync();
        var engine = new WebEngine(new WebEngineOptions { Headless = true });

        await using (var first = await OpenAttemptAsync(engine, site.Url))
        {
            Assert.Equal("Home", await HeadingAsync(first));
            Assert.Equal("/", (await first.ObserveAsync(CancellationToken.None)).Route);
        }

        // The next attempt starts on the same engine once the first one closed.
        await using var second = await OpenAttemptAsync(engine, site.Url);
        Assert.Equal("Home", await HeadingAsync(second));
    }

    [Fact]
    public async Task Relaunches_after_dispose_and_stays_idempotent()
    {
        using var site = await FixtureApp.StartAsync();
        var engine = new WebEngine(new WebEngineOptions { Headless = true });
        var first = await OpenAttemptAsync(engine, site.Url);
        Assert.Equal("Home", await HeadingAsync(first));
        await first.DisposeAsync();
        await first.DisposeAsync();

        await using var second = await OpenAttemptAsync(engine, site.Url);
        Assert.Equal("Home", await HeadingAsync(second));
    }

    [Fact]
    public async Task Round_trips_persisted_state_through_capture_and_restore_and_reset_drops_it()
    {
        using var site = await FixtureApp.StartAsync();
        await using var session = await OpenAttemptAsync(new WebEngine(new WebEngineOptions { Headless = true }), site.Url);

        await session.OpenAsync(site.Url + "state?set=abc", CancellationToken.None);
        Assert.Equal("abc", await HeadingAsync(session));
        var state = await session.CaptureStateAsync(CancellationToken.None);
        Assert.Equal("playwright-storage-state", state.Format);

        await session.ClearStateAsync(CancellationToken.None);
        await session.OpenAsync(site.Url + "state", CancellationToken.None);
        Assert.Equal("none", await HeadingAsync(session));

        await session.RestoreStateAsync(state, CancellationToken.None);
        await session.OpenAsync(site.Url + "state", CancellationToken.None);
        Assert.Equal("abc", await HeadingAsync(session));

        // A restart keeps the persisted state and shows nothing until the harness reopens the app.
        await session.RestartAsync(CancellationToken.None);
        Assert.Equal("about:blank", WebEngine.SurfaceOf(session)!.Page().Url);
        // The port's Route is the URL without its origin, so about:blank reads "blank" where upstream's location reads the whole URL.
        Assert.Equal("blank", (await session.ObserveAsync(CancellationToken.None)).Route);
        await session.OpenAsync(site.Url + "state", CancellationToken.None);
        Assert.Equal("abc", await HeadingAsync(session));

        var invalid = await Assert.ThrowsAsync<EngineException>(() => session.RestoreStateAsync(new EngineState("playwright-storage-state", 1, System.Text.Json.Nodes.JsonValue.Create("/etc/passwd")), CancellationToken.None));
        Assert.Equal("INVALID_STATE", invalid.Code);
    }

    [Fact]
    public async Task Follows_the_window_under_viewport_null_and_swipes_by_it()
    {
        using var site = await FixtureApp.StartAsync();
        await using var session = await WebSemanticsTests.OpenAsync(site.Url + "form", new WebEngineOptions { Headless = true, Viewport = null });
        var page = WebEngine.SurfaceOf(session)!.Page();
        Assert.Null(page.ViewportSize);
        var measured = await page.EvaluateAsync<int[]>("() => [innerWidth, innerHeight]");
        Assert.True(measured[0] > 0);

        await page.SetContentAsync("<div style=\"height: 5000px\">tall</div>");
        await session.SwipeAsync(ScrollDirection.Down, CancellationToken.None);
        await page.WaitForFunctionAsync("() => window.scrollY > 0");
        // The swipe moves three quarters of the window height (upstream moves half).
        Assert.Equal((int)Math.Round(measured[1] * 0.75, MidpointRounding.AwayFromZero), await page.EvaluateAsync<int>("() => window.scrollY"));
    }

    [Fact]
    public async Task Reports_an_unopened_page_as_INVALID_STATE_never_as_a_missing_node()
    {
        using var site = await FixtureApp.StartAsync();
        await using var session = await new WebEngine(new WebEngineOptions { Headless = true }).StartAsync(
            new EngineStartOptions { BaseUrl = site.Url, ActionTimeout = TimeSpan.FromSeconds(10) },
            CancellationToken.None);
        var error = await Assert.ThrowsAsync<EngineException>(() => session.ObserveAsync(CancellationToken.None));
        Assert.Equal("INVALID_STATE", error.Code);
    }

    [Fact]
    public async Task Cancels_an_in_flight_operation_when_its_signal_aborts_instead_of_waiting_out_Playwright()
    {
        using var site = await FixtureApp.StartAsync();
        await using var session = await new WebEngine(new WebEngineOptions { Headless = true }).StartAsync(
            new EngineStartOptions { BaseUrl = site.Url, ActionTimeout = TimeSpan.FromSeconds(10) },
            CancellationToken.None);
        using var controller = new CancellationTokenSource();
        var started = System.Diagnostics.Stopwatch.StartNew();
        var pending = session.OpenAsync(site.Url + "slow", controller.Token);
        controller.CancelAfter(TimeSpan.FromMilliseconds(100));
        var error = await Assert.ThrowsAnyAsync<E2EException>(() => pending);
        Assert.Equal("CANCELLED", error.Code);
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Reports_the_configured_test_id_attribute_as_testId_on_observed_and_located_nodes_alike()
    {
        using var site = await TinySite.StartAsync("""
            <!DOCTYPE html>
            <html><body><button data-qa="go">Go</button><button data-testid="stop">Stop</button></body></html>
            """);
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(new WebEngineOptions { Headless = true, TestIdAttribute = "data-qa" }),
            BaseUrl = site.Url,
            TestTitle = "web engine lifecycle > test id",
            AssertionTimeout = TimeSpan.FromSeconds(5),
            ActionTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(20),
            TestTimeout = TimeSpan.FromSeconds(30),
        });
        Exception? failure = null;
        try
        {
            await session.App.OpenAsync("/");
            var observed = WebSemanticsTests.Flatten((await session.Screen.ObserveAsync(CancellationToken.None)).Roots).ToList();
            var go = observed.Single(node => node.Name == "Go");
            var stop = observed.Single(node => node.Name == "Stop");
            Assert.Equal("go", go.TestId);
            Assert.Null(stop.TestId);
            // The tree carries the id as a field, not as an attribute.
            Assert.False(go.Attributes.ContainsKey("data-qa"));

            Assert.Equal(["go"], (await session.Screen.GetByTestId("go").ResolveAsync(CancellationToken.None)).Select(node => node.TestId));
            Assert.Empty(await session.Screen.GetByTestId("stop").ResolveAsync(CancellationToken.None));
        }
        catch (Exception ex)
        {
            failure = ex;
            throw;
        }
        finally
        {
            session.Complete(failure);
        }
    }

    [Fact]
    public async Task Partial_Declares_the_full_deterministic_tier_plus_the_browser_fixture()
    {
        using var site = await FixtureApp.StartAsync();
        var engine = new WebEngine(new WebEngineOptions { Headless = true });
        Assert.Equal("web", ((IEngine)engine).Name);
        // The port's capabilities are flags with no pointer, artifacts, or browser member: the pointer kinds are the session's own list,
        // and the browser fixture (Browser) is always there.
        const EngineCapabilities Tier = EngineCapabilities.Observation | EngineCapabilities.Actions | EngineCapabilities.Location | EngineCapabilities.Keyboard | EngineCapabilities.State;
        Assert.Equal(Tier, engine.Capabilities & Tier);
        Assert.True(engine.Capabilities.HasFlag(EngineCapabilities.Screenshot | EngineCapabilities.Video));
        await using var session = await engine.StartAsync(new EngineStartOptions { BaseUrl = site.Url, ActionTimeout = TimeSpan.FromSeconds(10) }, CancellationToken.None);
        Assert.NotEmpty(session.PointerActionKinds);
    }

    [Fact]
    public async Task Partial_Treats_endAttempt_before_startAttempt_and_dispose_on_a_cold_engine_as_no_ops()
    {
        using var site = await FixtureApp.StartAsync();
        // The port has no endAttempt: a session is the attempt. The cold engine's dispose, and a session that serves the page after it, are ported.
        var cold = new WebEngine(new WebEngineOptions { Headless = true });
        await cold.DisposeAsync();
        await cold.DisposeAsync();

        var engine = new WebEngine(new WebEngineOptions { Headless = true });
        await engine.DisposeAsync();
        await using var session = await OpenAttemptAsync(engine, site.Url);
        Assert.Equal("Home", await HeadingAsync(session));
    }

    [Fact]
    public async Task Records_a_video_per_page_at_the_viewport_size_a_restart_and_a_state_reset_each_continue_in_a_new_segment()
    {
        using var site = await FixtureApp.StartAsync();
        using var dir = new TempDirectory();
        await using var session = await StartWithArtifactsAsync(site.Url, dir.Path);
        await session.OpenAsync(site.Url + "form", CancellationToken.None);
        await session.StartVideoAsync(CancellationToken.None);
        var page = WebEngine.SurfaceOf(session)!.Page();
        var viewport = page.ViewportSize!;
        // Observations, actions, and captures run as they would without a recording.
        var observed = WebSemanticsTests.Flatten((await session.ObserveAsync(new ObserveOptions { Pixels = true }, TimeSpan.FromSeconds(10), CancellationToken.None)).Roots).ToList();
        var textbox = observed.First(node => node.Role == "textbox" && node.Name == "First");
        await session.PerformAsync(textbox, new LocatorAction.Fill("recorded"), CancellationToken.None);
        await session.ScreenshotArtifactAsync("shot", CancellationToken.None);
        await SettleAsync(WebEngine.SurfaceOf(session)!.Page());
        // A restart closes the page and opens another, blank until the harness reopens the app: the recording continues in a second segment.
        await session.RestartAsync(CancellationToken.None);
        Assert.NotSame(page, WebEngine.SurfaceOf(session)!.Page());
        Assert.Equal("about:blank", WebEngine.SurfaceOf(session)!.Page().Url);
        await session.OpenAsync(site.Url + "form", CancellationToken.None);
        await SettleAsync(WebEngine.SurfaceOf(session)!.Page());
        // A state reset recreates the context; the recording continues in a third.
        await session.ClearStateAsync(CancellationToken.None);
        Assert.Equal("about:blank", WebEngine.SurfaceOf(session)!.Page().Url);
        await session.OpenAsync(site.Url, CancellationToken.None);
        await SettleAsync(WebEngine.SurfaceOf(session)!.Page());
        var segments = VideoFiles(await session.StopVideoAsync(CancellationToken.None));
        Assert.Equal(["video/video.webm", "video/video-part2.webm", "video/video-part3.webm"], segments.Select(segment => segment.Path));
        var viewer = WebEngine.SurfaceOf(session)!.Page();
        foreach (var segment in segments)
        {
            await AssertSegmentAtViewportAsync(viewer, dir.Path, segment, viewport.Width, viewport.Height);
        }
    }

    [Fact]
    public async Task Records_a_video_started_mid_attempt_at_the_viewport_size_each_time_it_starts()
    {
        using var site = await FixtureApp.StartAsync();
        using var dir = new TempDirectory();
        await using var session = await StartWithArtifactsAsync(site.Url, dir.Path);
        // A host that records on demand, after the page is open.
        await session.OpenAsync(site.Url, CancellationToken.None);
        var viewer = WebEngine.SurfaceOf(session)!.Page();
        var viewport = viewer.ViewportSize!;
        List<VideoFile> segments = [];
        foreach (var url in new[] { site.Url + "form", site.Url })
        {
            await session.StartVideoAsync(CancellationToken.None);
            await session.OpenAsync(url, CancellationToken.None);
            await SettleAsync(viewer);
            segments.AddRange(VideoFiles(await session.StopVideoAsync(CancellationToken.None)));
        }

        Assert.Equal(["video/video.webm", "video/video-part2.webm"], segments.Select(segment => segment.Path));
        foreach (var segment in segments)
        {
            var frame = await LastFrameAsync(viewer, await File.ReadAllBytesAsync(Path.Combine(dir.Path, segment.Path)));
            Assert.True(frame.Width == viewport.Width && frame.Height == viewport.Height, segment.Path);
            Assert.True(frame.Padded < 0.01, segment.Path + " padded");
            Assert.True(frame.Inked > 0.0005, segment.Path + " inked");
        }
    }

    [Fact]
    public async Task Partial_Rejects_a_video_that_cannot_start_and_leaves_the_session_working()
    {
        using var site = await FixtureApp.StartAsync();
        using var dir = new TempDirectory();
        await using var session = await StartWithArtifactsAsync(site.Url, dir.Path);
        await session.OpenAsync(site.Url, CancellationToken.None);
        var page = WebEngine.SurfaceOf(session)!.Page();
        // Upstream spies on page.screencast.start to reject it with "screencast unavailable". The port cannot mock Playwright, so the start fails
        // for real: a screencast the test already runs on the page refuses a second one, and the failure reads Playwright's own message.
        await page.Screencast.StartAsync(new ScreencastStartOptions { Path = Path.Combine(dir.Path, "held.webm") });
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => session.StartVideoAsync(CancellationToken.None));
        }
        finally
        {
            await page.Screencast.StopAsync();
        }

        await session.OpenAsync(site.Url + "form", CancellationToken.None);
        Assert.Equal(site.Url + "form", WebEngine.SurfaceOf(session)!.Page().Url);
    }

    [Fact]
    public async Task Opens_the_attempt_page_when_the_video_starts()
    {
        using var site = await FixtureApp.StartAsync();
        using var dir = new TempDirectory();
        await using var session = await StartWithArtifactsAsync(site.Url, dir.Path);
        await session.StartVideoAsync(CancellationToken.None);
        // The page exists before any navigation: the recording owns its screencast.
        Assert.Equal("about:blank", WebEngine.SurfaceOf(session)!.Page().Url);
        await session.OpenAsync(site.Url, CancellationToken.None);
        var segments = VideoFiles(await session.StopVideoAsync(CancellationToken.None));
        Assert.Equal(["video/video.webm"], segments.Select(segment => segment.Path));
        Assert.True(new FileInfo(Path.Combine(dir.Path, "video/video.webm")).Length > 0);
    }

    [Fact]
    public async Task Masks_secure_fields_in_artifact_screenshots_and_restarts_the_counter_per_attempt()
    {
        using var site = await FixtureApp.StartAsync();
        using var dir = new TempDirectory();
        var engine = new WebEngine(new WebEngineOptions { Headless = true });
        await using (var first = await StartWithArtifactsAsync(engine, site.Url, dir.Path))
        {
            await first.OpenAsync(site.Url + "login", CancellationToken.None);
            Assert.Equal("screenshots/001-first.png", await first.ScreenshotArtifactAsync("first", CancellationToken.None));
        }

        await using var second = await StartWithArtifactsAsync(engine, site.Url, dir.Path);
        await second.OpenAsync(site.Url + "login", CancellationToken.None);
        var nodes = WebSemanticsTests.Flatten((await second.ObserveAsync(CancellationToken.None)).Roots).ToList();
        var user = nodes.First(node => node.Role == "textbox" && node.Name == "User");
        var password = nodes.First(node => node.States.Secure);
        Assert.NotNull(user.Rect);
        Assert.NotNull(password.Rect);
        await second.PerformAsync(password, new LocatorAction.Fill("hunter2", Sensitive: true), CancellationToken.None);

        var relative = await second.ScreenshotArtifactAsync("login", CancellationToken.None);
        Assert.Equal("screenshots/001-login.png", relative);
        var image = ImageOps.DecodePng(await File.ReadAllBytesAsync(Path.Combine(dir.Path, relative)));
        // The password field is covered by the opaque mask; the plain field is not.
        Assert.Equal([0, 0, 0], PixelAtCentre(image, password.Rect));
        Assert.Equal([255, 255, 255], PixelAtCentre(image, user.Rect));
    }

    private static byte[] PixelAtCentre(RgbaImage image, BoundingBox rect)
    {
        var x = (int)Math.Round(rect.X + (rect.Width / 2), MidpointRounding.AwayFromZero);
        var y = (int)Math.Round(rect.Y + (rect.Height / 2), MidpointRounding.AwayFromZero);
        var at = ((y * image.Width) + x) * 4;
        return image.Data[at..(at + 3)];
    }

    /// <summary>The segments as files: a local recording never links, so a link here is a failure.</summary>
    private static List<VideoFile> VideoFiles(IReadOnlyList<VideoSegment> segments) =>
        [.. segments.Select(segment => segment as VideoFile ?? throw new InvalidOperationException("expected a video file, got a link"))];

    // Upstream waits for a painted frame its spy on the screencast saw. The port cannot spy, so it paints the page and waits past the
    // recorder's own first-frame wait, which ends a segment only after its first frame or that bound.
    private static async Task SettleAsync(IPage page)
    {
        await page.ScreenshotAsync();
        await Task.Delay(VideoRecorder.FirstFrameWait + TimeSpan.FromMilliseconds(500));
    }

    private static async Task AssertSegmentAtViewportAsync(IPage viewer, string dir, VideoFile segment, int width, int height)
    {
        var absolute = Path.Combine(dir, segment.Path);
        Assert.True(File.Exists(absolute), segment.Path);
        Assert.True(new FileInfo(absolute).Length > 0);
        var bytes = await File.ReadAllBytesAsync(absolute);
        // Every WebM file opens with the EBML magic.
        Assert.Equal("1a45dfa3", Convert.ToHexStringLower(bytes.AsSpan(0, 4)));
        Assert.True(DateTimeOffset.TryParse(segment.StartedAt, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _));
        // Every segment shows the page at the attempt's viewport size: no grey padding from a screencast another client sized smaller,
        // and some ink, so the page painted into it.
        var frame = await LastFrameAsync(viewer, bytes);
        Assert.True(frame.Width == width && frame.Height == height, segment.Path);
        Assert.True(frame.Padded < 0.01, segment.Path + " padded");
        Assert.True(frame.Inked > 0.0005, segment.Path + " inked");
    }

    private sealed record Frame(int Width, int Height, double Padded, double Inked);

    /// <summary>Decodes a recording inside the page and reads its last frame: its size, how much of it is the grey a screencast pads a smaller frame with, and how much is content.</summary>
    private static async Task<Frame> LastFrameAsync(IPage page, byte[] webm)
    {
        var result = await page.EvaluateAsync<System.Text.Json.JsonElement>(
            """
            async (bytes) => {
              const video = document.createElement('video');
              video.muted = true;
              video.src = URL.createObjectURL(new Blob([new Uint8Array(bytes)], { type: 'video/webm' }));
              await new Promise((resolve, reject) => {
                video.addEventListener('loadedmetadata', () => resolve(), { once: true });
                video.addEventListener('error', () => reject(new Error('the recording did not decode')), { once: true });
              });
              await video.play();
              await new Promise((resolve) => {
                video.addEventListener('ended', () => resolve(), { once: true });
                setTimeout(resolve, 5000);
              });
              const canvas = document.createElement('canvas');
              canvas.width = video.videoWidth;
              canvas.height = video.videoHeight;
              const context = canvas.getContext('2d');
              context.drawImage(video, 0, 0);
              const { data } = context.getImageData(0, 0, canvas.width, canvas.height);
              let padded = 0;
              let inked = 0;
              for (let i = 0; i < data.length; i += 4) {
                const r = data[i];
                const g = data[i + 1];
                const b = data[i + 2];
                if (r === g && g === b && r > 120 && r < 136) padded += 1;
                else if (r < 200 || g < 200 || b < 200) inked += 1;
              }
              const total = data.length / 4;
              return { width: canvas.width, height: canvas.height, padded: padded / total, inked: inked / total };
            }
            """,
            webm.Select(value => (int)value).ToArray());
        return new Frame(result.GetProperty("width").GetInt32(), result.GetProperty("height").GetInt32(), result.GetProperty("padded").GetDouble(), result.GetProperty("inked").GetDouble());
    }

    private static Task<IEngineSession> StartWithArtifactsAsync(string url, string artifactsDir) =>
        StartWithArtifactsAsync(new WebEngine(new WebEngineOptions { Headless = true }), url, artifactsDir);

    private static Task<IEngineSession> StartWithArtifactsAsync(WebEngine engine, string url, string artifactsDir) =>
        engine.StartAsync(new EngineStartOptions { BaseUrl = url, ActionTimeout = TimeSpan.FromSeconds(10), ArtifactsDir = artifactsDir }, CancellationToken.None);

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("e2e-lifecycle-").FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    /// <summary>Starts an attempt on <paramref name="engine"/> and opens the home page.</summary>
    private static async Task<IEngineSession> OpenAttemptAsync(WebEngine engine, string url)
    {
        var session = await engine.StartAsync(new EngineStartOptions { BaseUrl = url, ActionTimeout = TimeSpan.FromSeconds(10) }, CancellationToken.None);
        await session.OpenAsync(url, CancellationToken.None);
        return session;
    }

    private static async Task<string?> HeadingAsync(IEngineSession session) =>
        WebSemanticsTests.Flatten((await session.ObserveAsync(CancellationToken.None)).Roots).First(node => node.Role == "heading").Name;
}
