// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using System.Text;
using E2E.Engine;

namespace E2E.Tests.Tabs;

/// <summary>
/// Upstream's <c>describe('tabs')</c> (<c>packages/web/tests/integration/tabs.test.ts</c>): tabs in a real browser. A tab the app opens joins the
/// tabs in the background, <c>waitForPopup</c> and <c>switchTab</c> show one, and when the shown tab closes, the tab that opened it is shown
/// again. Driven through the engine hooks the harness and the agent use, and the <c>browser</c> fixture's tab calls.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class TabsTests : IAsyncLifetime
{
    private static readonly Dictionary<string, string> Pages = new()
    {
        ["/opener"] = """
            <!doctype html><title>Opener</title><h1>Opener</h1>
            <button onclick="window.open('/popup', '_blank')">Open window</button>
            <a href="/popup" target="_blank">Open link</a>
            <button onclick="const tab = window.open('', '_blank'); tab.document.write('<title>Written</title><p>Code TAB-88421</p>'); tab.document.close();">Write tab</button>
            <a href="/slow" target="_blank">Open slow</a>
            <a href="/hang" target="_blank">Open hang</a>
            <a href="/no-content" target="_blank">Open nothing</a>
            <a href="mailto:help@example.com" target="_blank">Email us</a>
            <a href="/export" target="_blank">Export</a>
            <button onclick="const tab = window.open('/popup', '_blank'); setTimeout(() => tab.close(), 400);">Open briefly</button>
            """,
        ["/popup"] = """
            <!doctype html><title>Popup</title><h1>Popup</h1>
            <button onclick="window.close()">Close me</button>
            <button onclick="window.open('/nested', '_blank')">Open nested</button>
            <button onclick="window.open('/slow', '_blank'); window.close();">Hand off</button>
            """,
        ["/nested"] = """<!doctype html><title>Nested</title><h1>Nested</h1><button onclick="window.close()">Close nested</button>""",
        ["/slow"] = "<!doctype html><title>Slow</title><h1>Slow</h1>",
    };

    private static readonly CancellationToken None = CancellationToken.None;

    private static readonly LocatorExpression Heading = LocatorExpression.ForRole("heading", null, new RoleOptions { Exact = true });

    /// <summary>Requests for <c>/hang</c>, answered only when a test lets them go.</summary>
    private readonly List<HttpListenerResponse> _hanging = [];

    private TinySite _site = null!;
    private string _url = "";
    private string _artifactsDir = "";

    public async Task InitializeAsync()
    {
        _site = await TinySite.StartAsync(async context =>
        {
            var path = context.Request.Url!.AbsolutePath;
            if (path == "/hang")
            {
                lock (_hanging)
                {
                    _hanging.Add(context.Response);
                }

                return;
            }

            if (path == "/no-content")
            {
                context.Response.StatusCode = 204;
                context.Response.Close();
                return;
            }

            if (path == "/export")
            {
                context.Response.ContentType = "text/csv";
                context.Response.AddHeader("content-disposition", "attachment; filename=\"export.csv\"");
                var csv = Encoding.UTF8.GetBytes("a,b\n");
                context.Response.ContentLength64 = csv.Length;
                await context.Response.OutputStream.WriteAsync(csv);
                context.Response.Close();
                return;
            }

            Pages.TryGetValue(path, out var page);
            if (path == "/slow")
            {
                await Task.Delay(1_500);
            }

            if (page is null)
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            await TinySite.RespondAsync(context, page);
        });
        _url = _site.Url.TrimEnd('/');
        _artifactsDir = Directory.CreateTempSubdirectory("e2e-tabs-").FullName;
    }

    public Task DisposeAsync()
    {
        lock (_hanging)
        {
            foreach (var response in _hanging)
            {
                response.Abort();
            }
        }

        _site.Dispose();
        Directory.Delete(_artifactsDir, recursive: true);
        return Task.CompletedTask;
    }

    /// <summary>Runs <paramref name="body"/> on one engine in a running attempt opened at <c>/opener</c>, with the <c>browser</c> fixture.</summary>
    private async Task WithAttemptAsync(Func<IEngineSession, Browser, Task> body)
    {
        await using var session = await new WebEngine(headless: true).StartAsync(
            new EngineStartOptions { BaseUrl = _site.Url, ArtifactsDir = _artifactsDir, ActionTimeout = TimeSpan.FromSeconds(30) },
            None);
        var browser = new Browser(session, "web", _site.Url, Directory.GetCurrentDirectory(), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), () => None, () => { }, new SoftFailures());
        await session.OpenAsync($"{_url}/opener", None);
        await body(session, browser);
    }

    private static IEnumerable<SemanticNode> Flatten(SemanticNode node) => [node, .. node.Children.SelectMany(Flatten)];

    /// <summary>Taps the node the current screen names <paramref name="name"/>.</summary>
    private static async Task TapAsync(IEngineSession session, string name)
    {
        var node = (await session.ObserveAsync(None)).Roots.SelectMany(Flatten).FirstOrDefault(candidate => candidate.Name == name)
            ?? throw new InvalidOperationException($"no node named {name} on the screen");
        await session.PerformAsync(node, new LocatorAction.Tap(), None);
    }

    /// <summary>Polls <paramref name="read"/> until <paramref name="accept"/> takes what it reads, for up to ten seconds, and returns the last read.</summary>
    private static async Task<T> PollAsync<T>(Func<Task<T>> read, Func<T, bool> accept)
    {
        var deadline = Stopwatch.StartNew();
        while (true)
        {
            var value = await read();
            if (accept(value) || deadline.Elapsed > TimeSpan.FromSeconds(10))
            {
                return value;
            }

            await Task.Delay(50);
        }
    }

    /// <summary>The screen's location and tabs, once its location ends with <paramref name="suffix"/>.</summary>
    private static async Task<Observation> ScreenAtAsync(IEngineSession session, string suffix)
    {
        var snapshot = await PollAsync(() => session.ObserveAsync(None), observed => (observed.Location ?? "").EndsWith(suffix, StringComparison.Ordinal));
        Assert.EndsWith(suffix, snapshot.Location ?? "", StringComparison.Ordinal);
        return snapshot;
    }

    /// <summary>The screen once the attempt has <paramref name="count"/> tabs open.</summary>
    private static async Task<Observation> WithTabsAsync(IEngineSession session, int count)
    {
        var snapshot = await PollAsync(() => session.ObserveAsync(None), observed => (observed.Tabs?.Count ?? 1) == count);
        Assert.Equal(count, snapshot.Tabs?.Count ?? 1);
        return snapshot;
    }

    [Fact]
    public async Task A_tab_the_app_opens_stays_in_the_background_until_it_is_switched_to_and_its_close_shows_its_opener()
    {
        await WithAttemptAsync(async (session, _) =>
        {
            await TapAsync(session, "Open window");
            var opened = await WithTabsAsync(session, 2);
            Assert.Equal($"{_url}/opener", opened.Location);
            Assert.Equal(new EngineTabs(2, 0), opened.Tabs);
            await session.SwitchTabAsync(1, OperationContext.Test, None);
            Assert.Equal(new EngineTabs(2, 1), (await ScreenAtAsync(session, "/popup")).Tabs);

            // The tap's only effect is closing the page it tapped on.
            await TapAsync(session, "Close me");
            var back = await ScreenAtAsync(session, "/opener");
            Assert.Null(back.Tabs);
        });
    }

    [Fact]
    public async Task An_operation_after_a_tap_that_opens_a_slow_tab_runs_on_the_opener_at_once_waitForPopup_waits_for_the_page()
    {
        await WithAttemptAsync(async (session, browser) =>
        {
            await TapAsync(session, "Open slow");
            var started = Stopwatch.StartNew();
            Assert.Equal($"{_url}/opener", (await session.ObserveAsync(None)).Location);
            Assert.True(started.Elapsed < TimeSpan.FromSeconds(1));

            // It joins in the background once its page loads.
            Assert.Equal(new EngineTabs(2, 0), (await WithTabsAsync(session, 2)).Tabs);
            var slow = await browser.WaitForPopupAsync(() => TapAsync(session, "Open slow"));
            Assert.Equal($"{_url}/slow", await slow.UrlAsync());
            Assert.Equal(new EngineTabs(3, 2), (await session.ObserveAsync(None)).Tabs);
        });
    }

    [Fact]
    public async Task WaitForPopup_names_the_page_a_tab_could_not_load_when_its_time_runs_out_and_the_opener_still_answers()
    {
        await WithAttemptAsync(async (session, browser) =>
        {
            var failure = await Assert.ThrowsAsync<TestException>(() => browser.WaitForPopupAsync(() => TapAsync(session, "Open hang"), TimeSpan.FromSeconds(1)));
            Assert.Equal("ACTION_FAILED", failure.Code);
            Assert.Equal($"the tab the app opened did not load {_url}/hang within 1000ms", failure.Message);
            Assert.Equal("Opener", await browser.TitleAsync());
            lock (_hanging)
            {
                foreach (var response in _hanging)
                {
                    response.Abort();
                }

                _hanging.Clear();
            }
        });
    }

    [Fact]
    public async Task A_tab_whose_opener_closed_itself_before_the_tab_loaded_joins_the_tabs()
    {
        await WithAttemptAsync(async (session, browser) =>
        {
            await browser.WaitForPopupAsync(() => TapAsync(session, "Open window"));
            var orphan = await browser.WaitForPopupAsync(() => TapAsync(session, "Hand off"));
            Assert.Equal($"{_url}/slow", await orphan.UrlAsync());
            Assert.True(orphan.IsActive);
            Assert.Equal(2, browser.Tabs().Count);

            // It has no opener left, so its close hands the screen to the tab shown before it.
            await orphan.CloseAsync();
            Assert.Equal($"{_url}/opener", await browser.UrlAsync());
        });
    }

    [Fact]
    public async Task A_tab_that_never_gets_a_document_never_joins_the_tabs()
    {
        await WithAttemptAsync(async (session, browser) =>
        {
            foreach (var link in new[] { "Open nothing", "Email us", "Export" })
            {
                await TapAsync(session, link);
                var started = Stopwatch.StartNew();
                var snapshot = await session.ObserveAsync(None);
                Assert.Equal($"{_url}/opener", snapshot.Location);
                Assert.True(started.Elapsed < TimeSpan.FromSeconds(1), link);
                await Task.Delay(500);
                Assert.Single(browser.Tabs());
                Assert.Null((await session.ObserveAsync(None)).Tabs);
            }
        });
    }

    [Fact]
    public async Task Reads_in_flight_when_the_app_closes_the_shown_tab_repeat_on_the_tab_shown_next()
    {
        await WithAttemptAsync(async (session, browser) =>
        {
            await browser.WaitForPopupAsync(() => TapAsync(session, "Open briefly"));
            await ScreenAtAsync(session, "/popup");

            // A retryable stale read is the runner's to repeat, as the locator engine does.
            static async Task<T?> Repeated<T>(Func<Task<T>> read)
                where T : class
            {
                try
                {
                    return await read();
                }
                catch (EngineException cause)
                {
                    Assert.Equal((EngineErrorCodes.NodeStale, true, "the tab closed while it was read"), (cause.Code, cause.Retryable, cause.Message));
                    return null;
                }
            }

            var until = Stopwatch.StartNew();
            var locations = new List<string>();
            while (until.Elapsed < TimeSpan.FromMilliseconds(1_500))
            {
                var observing = Repeated(() => session.ObserveAsync(None));
                var locating = Repeated(() => session.LocateAsync(Heading, TimeSpan.FromSeconds(30), None));
                await Task.WhenAll(observing, locating);
                if (await observing is { } snapshot && !locations.Contains(snapshot.Location ?? ""))
                {
                    locations.Add(snapshot.Location ?? "");
                }
            }

            Assert.Equal([$"{_url}/popup", $"{_url}/opener"], locations);
        });
    }

    [Fact]
    public async Task A_target_blank_link_opens_a_tab_and_a_tab_the_app_wrote_with_no_navigation_reads_once_switched_to()
    {
        await WithAttemptAsync(async (session, _) =>
        {
            await TapAsync(session, "Open link");
            await WithTabsAsync(session, 2);
            await TapAsync(session, "Write tab");
            Assert.Equal(new EngineTabs(3, 0), (await WithTabsAsync(session, 3)).Tabs);
            await session.SwitchTabAsync(2, OperationContext.Test, None);
            var written = await PollAsync(() => session.ObserveAsync(None), observed => observed.Roots.SelectMany(Flatten).Any(node => node.Text == "Code TAB-88421"));
            Assert.Contains(written.Roots.SelectMany(Flatten), node => node.Text == "Code TAB-88421");
            Assert.Equal(new EngineTabs(3, 2), (await session.ObserveAsync(None)).Tabs);
        });
    }

    [Fact]
    public async Task WaitForPopup_resolves_with_the_tab_once_it_is_active_activate_and_close_move_between_tabs()
    {
        await WithAttemptAsync(async (_, browser) =>
        {
            var popup = await browser.WaitForPopupAsync(async () => await browser.EvaluateAsync<object>("document.querySelector('a').click()"));
            Assert.True(popup.IsActive);
            Assert.Equal($"{_url}/popup", await popup.UrlAsync());
            Assert.Equal($"{_url}/popup", await browser.UrlAsync());
            var tabs = browser.Tabs();
            var (opener, second) = (tabs[0], tabs[1]);
            Assert.Same(popup, second);
            await opener.ActivateAsync();
            Assert.Equal("Opener", await browser.TitleAsync());
            Assert.False(popup.IsActive);
            await popup.ActivateAsync();
            Assert.Equal("Popup", await browser.TitleAsync());
            await popup.CloseAsync();
            Assert.True(popup.IsClosed);
            Assert.True(opener.IsActive);
            Assert.Equal($"{_url}/opener", await browser.UrlAsync());
            Assert.Equal([opener], browser.Tabs());
            Assert.Equal("ACTION_FAILED", (await Assert.ThrowsAsync<TestException>(() => popup.ActivateAsync())).Code);
        });
    }

    [Fact]
    public async Task WaitForPopup_fails_the_wait_when_no_tab_opens()
    {
        await WithAttemptAsync(async (_, browser) =>
        {
            var failure = await Assert.ThrowsAsync<TestException>(() => browser.WaitForPopupAsync(() => Task.CompletedTask, TimeSpan.FromMilliseconds(500)));
            Assert.Equal("ACTION_FAILED", failure.Code);
            Assert.StartsWith("no tab opened within 500ms", failure.Message, StringComparison.Ordinal);
            Assert.Single(browser.Tabs());
        });
    }

    [Fact]
    public async Task SwitchTab_and_closeTab_take_a_tab_by_its_place_in_opening_order_and_refuse_one_that_is_not_open()
    {
        await WithAttemptAsync(async (session, _) =>
        {
            await TapAsync(session, "Open window");
            await WithTabsAsync(session, 2);
            await session.SwitchTabAsync(1, OperationContext.Test, None);
            Assert.Equal(new EngineTabs(2, 1), (await ScreenAtAsync(session, "/popup")).Tabs);
            var missing = await Assert.ThrowsAsync<TestException>(() => session.SwitchTabAsync(2, OperationContext.Test, None));
            Assert.Equal("ACTION_FAILED", missing.Code);
            Assert.Contains("there is no tab 3: 2 tabs are open", missing.Message, StringComparison.Ordinal);

            // Closing the tab on screen shows its opener.
            await session.CloseTabAsync(1, OperationContext.Test, None);
            Assert.Equal($"{_url}/opener", (await session.ObserveAsync(None)).Location);
            var only = await Assert.ThrowsAsync<TestException>(() => session.CloseTabAsync(0, OperationContext.Test, None));
            Assert.Equal("ACTION_FAILED", only.Code);
            Assert.Contains("tab 1 is the only open tab", only.Message, StringComparison.Ordinal);

            // Closing a tab in the background leaves the screen where it is.
            await TapAsync(session, "Open window");
            await WithTabsAsync(session, 2);
            await session.CloseTabAsync(1, OperationContext.Test, None);
            var after = await session.ObserveAsync(None);
            Assert.Equal($"{_url}/opener", after.Location);
            Assert.Null(after.Tabs);

            // Two closes at once leave one tab open: the second sees the first.
            await TapAsync(session, "Open window");
            await WithTabsAsync(session, 2);
            var closes = new[] { session.CloseTabAsync(1, OperationContext.Test, None), session.CloseTabAsync(0, OperationContext.Test, None) };
            await Task.WhenAny(Task.WhenAll(closes.Select(close => close.ContinueWith(static _ => { }, TaskScheduler.Default))));
            Assert.Equal([TaskStatus.RanToCompletion, TaskStatus.Faulted], closes.Select(close => close.Status));
            var second = Assert.IsType<TestException>(closes[1].Exception!.InnerException);
            Assert.Equal("ACTION_FAILED", second.Code);
            Assert.Contains("is the only open tab", second.Message, StringComparison.Ordinal);
            Assert.Equal($"{_url}/opener", (await session.ObserveAsync(None)).Location);
        });
    }

    [Fact]
    public async Task A_tab_closed_out_of_order_hands_the_screen_to_its_nearest_open_opener()
    {
        await WithAttemptAsync(async (session, browser) =>
        {
            var middle = await browser.WaitForPopupAsync(async () => await browser.EvaluateAsync<object>("document.querySelector('a').click()"));
            var last = await browser.WaitForPopupAsync(async () => await browser.EvaluateAsync<object>("document.querySelectorAll('button')[1].click()"));
            Assert.Equal($"{_url}/nested", await browser.UrlAsync());

            // The tab in the middle closes in the background: the screen stays where it is.
            await middle.CloseAsync();
            Assert.True(last.IsActive);
            Assert.Equal(2, browser.Tabs().Count);

            // The shown tab closes and its opener is gone: the screen returns to the opener's opener.
            await TapAsync(session, "Close nested");
            await ScreenAtAsync(session, "/opener");
            Assert.Single(browser.Tabs());
        });
    }

    [Fact]
    public async Task NewTab_opens_a_blank_tab_in_the_same_context_and_shows_it()
    {
        await WithAttemptAsync(async (_, browser) =>
        {
            await browser.EvaluateAsync<object>("document.cookie = 'shared=yes'");
            var tab = await browser.NewTabAsync();
            Assert.True(tab.IsActive);
            Assert.Equal("about:blank", await browser.UrlAsync());
            await browser.GotoAsync("/nested");
            Assert.Equal("shared=yes", await browser.EvaluateAsync<string>("document.cookie"));
            Assert.Equal(2, browser.Tabs().Count);
        });
    }

    [Fact]
    public async Task The_video_keeps_a_segment_per_tab_shown_and_drops_the_one_a_tab_that_closed_itself_could_not_flush()
    {
        await WithAttemptAsync(async (session, browser) =>
        {
            await session.StartVideoAsync(None);
            await browser.WaitForPopupAsync(() => TapAsync(session, "Open window"));
            await ScreenAtAsync(session, "/popup");
            await TapAsync(session, "Close me");
            await ScreenAtAsync(session, "/opener");
            var segments = await session.StopVideoAsync(None);
            var files = segments.Select(segment => segment is VideoFile file ? file.Path : throw new InvalidOperationException("the screencast writes files")).ToList();
            Assert.Equal("video/video.webm", files[0]);
            Assert.Equal("video/video-part3.webm", files[^1]);
            foreach (var file in files)
            {
                var absolute = Path.Combine(_artifactsDir, file);
                Assert.True(File.Exists(absolute), file);
                Assert.True(new FileInfo(absolute).Length > 0, file);
            }
        });
    }
}
