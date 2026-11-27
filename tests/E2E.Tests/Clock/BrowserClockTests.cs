// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using E2E.Engine;
using Microsoft.Playwright;
using Intersection = E2E.Engine.ViewportIntersection;

namespace E2E.Tests.Clock;

/// <summary>
/// Upstream's <c>describe('browser.clock')</c> (<c>packages/web/tests/integration/clock.test.ts</c>, tester-army/e2e#2256): Playwright's page clock on
/// the attempt's context in a real browser, through navigation, <c>App.RestartAsync</c>, <c>App.ClearStateAsync</c>, and tabs the app opens, and the
/// engine's own page-side waits under a paused clock. Driven through the engine session and the <c>Browser</c>, as the attempt drives them.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class BrowserClockTests
{
    private const long T0 = 1_706_860_800_000; // 2024-02-02T08:00:00Z
    private const long Hour = 3_600_000;

    private static readonly CancellationToken None = CancellationToken.None;

    private static string Iso(long ms) =>
        DateTimeOffset
            .FromUnixTimeMilliseconds(ms)
            .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private static OperationContext Operation(int timeoutMs = 30_000) =>
        OperationContext.Test with
        {
            Timeout = TimeSpan.FromMilliseconds(timeoutMs),
        };

    /// <summary>Runs <paramref name="body"/> in one attempt of a fresh engine, with no page open yet.</summary>
    private static async Task InAttemptAsync(
        Func<IEngineSession, TinySite, BrowserClock, Browser, Task> body,
        int timeoutMs = 30_000
    )
    {
        using var app = await FixtureApp.StartAsync();
        await using var session = await new WebEngine(headless: true).StartAsync(
            new EngineStartOptions { BaseUrl = app.Url, ActionTimeout = TimeSpan.FromMilliseconds(timeoutMs) },
            None
        );
        var browser = new Browser(
            session,
            "web",
            app.Url,
            Directory.GetCurrentDirectory(),
            TimeSpan.FromMilliseconds(timeoutMs),
            TimeSpan.FromSeconds(1),
            () => CancellationToken.None,
            () => { },
            new SoftFailures()
        );
        await body(session, app, browser.Clock, browser);
    }

    private static Task OpenAsync(IEngineSession session, TinySite app) =>
        session.OpenAsync(app.Url + "clock", Operation(), None);

    /// <summary>What the clock page shows: the instant it loaded, the page's time now, and whether its one-second timer fired.</summary>
    private static async Task<Reading> ReadAsync(IEngineSession session) =>
        (
            await WebEngine
                .SurfaceOf(session)!
                .Page()
                .EvaluateAsync<JsonElement>(
                    """
                    () => ({
                      loaded: document.getElementById('loaded').textContent,
                      now: Date.now(),
                      timer: document.getElementById('timer').textContent,
                    })
                    """
                )
        ).Deserialize<Reading>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private static Task WaitForTimerAsync(IPage page) =>
        page.WaitForFunctionAsync("() => document.getElementById('timer').textContent === 'fired'");

    private sealed record Reading(string Loaded, long Now, string Timer);

    [Fact]
    public async Task Pauses_at_a_time_and_fires_a_timer_only_as_runFor_reaches_it_through_a_navigation()
    {
        await InAttemptAsync(
            async (session, app, clock, _) =>
            {
                await clock.InstallAsync(new BrowserClockInstallOptions { Time = T0 });
                await clock.PauseAtAsync(T0 + Hour);
                await OpenAsync(session, app);
                Assert.Equal(new Reading(Iso(T0 + Hour), T0 + Hour, "waiting"), await ReadAsync(session));
                await clock.RunForAsync(999);
                Assert.Equal(new Reading(Iso(T0 + Hour), T0 + Hour + 999, "waiting"), await ReadAsync(session));
                await clock.RunForAsync("00:01");
                Assert.Equal(new Reading(Iso(T0 + Hour), T0 + Hour + 1999, "fired"), await ReadAsync(session));
                await OpenAsync(session, app);
                Assert.Equal(new Reading(Iso(T0 + Hour + 1999), T0 + Hour + 1999, "waiting"), await ReadAsync(session));
            }
        );
    }

    [Fact]
    public async Task Moves_the_wall_time_without_firing_a_timer_on_setSystemTime_and_fires_it_once_on_fastForward()
    {
        await InAttemptAsync(
            async (session, app, clock, _) =>
            {
                await clock.InstallAsync(
                    new BrowserClockInstallOptions { Time = DateTimeOffset.FromUnixTimeMilliseconds(T0) }
                );
                await clock.PauseAtAsync(Iso(T0 + Hour));
                await OpenAsync(session, app);
                await clock.SetSystemTimeAsync(T0 + (2 * Hour));
                var moved = await ReadAsync(session);
                Assert.Equal((T0 + (2 * Hour), "waiting"), (moved.Now, moved.Timer));
                await clock.FastForwardAsync(5000);
                var forwarded = await ReadAsync(session);
                Assert.Equal((T0 + (2 * Hour) + 5000, "fired"), (forwarded.Now, forwarded.Timer));
            }
        );
    }

    [Fact]
    public async Task Holds_Date_at_a_fixed_time_while_timers_keep_running_and_lets_a_paused_clock_resume()
    {
        await InAttemptAsync(
            async (session, app, clock, _) =>
            {
                await clock.SetFixedTimeAsync(T0);
                await OpenAsync(session, app);
                var page = WebEngine.SurfaceOf(session)!.Page();
                await WaitForTimerAsync(page);
                Assert.Equal(new Reading(Iso(T0), T0, "fired"), await ReadAsync(session));

                await clock.PauseAtAsync(T0 + Hour);
                await OpenAsync(session, app);
                await clock.ResumeAsync();
                await WaitForTimerAsync(page);
                Assert.True((await ReadAsync(session)).Now > T0 + Hour);
            }
        );
    }

    [Fact]
    public async Task Keeps_the_clock_through_app_restart_app_clearState_and_in_a_tab_the_app_opens()
    {
        await InAttemptAsync(
            async (session, app, clock, _) =>
            {
                // No time: the clock starts at the runner's now, which the replay keeps.
                var paused = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (24 * Hour);
                await clock.InstallAsync();
                await clock.PauseAtAsync(paused);
                await clock.RunForAsync(500);
                var expected = new Reading(Iso(paused + 500), paused + 500, "waiting");
                await OpenAsync(session, app);
                Assert.Equal(expected, await ReadAsync(session));
                await session.RestartAsync(Operation(), None);
                await OpenAsync(session, app);
                Assert.Equal(expected, await ReadAsync(session));
                await session.ClearStateAsync(Operation(), None);
                await OpenAsync(session, app);
                Assert.Equal(expected, await ReadAsync(session));

                var live = WebEngine.SurfaceOf(session)!;
                var popup = await live.Context()
                    .RunAndWaitForPageAsync(() => live.Page().EvaluateAsync("() => { window.open('/clock'); }"));
                await popup.WaitForLoadStateAsync();
                Assert.Equal(paused + 500, await popup.EvaluateAsync<long>("() => Date.now()"));
            }
        );
    }

    [Fact]
    public async Task Reaches_a_page_already_open_and_its_frames_and_keeps_its_place_among_init_scripts_through_app_clearState()
    {
        await InAttemptAsync(
            async (session, app, clock, browser) =>
            {
                // Before the clock: this script reads the native Date. After it: the clock's.
                await browser.AddInitScriptAsync(
                    WebInitScript.FromFunction("() => { Reflect.set(window, 'before', Date.now()); }")
                );
                await OpenAsync(session, app);
                var page = WebEngine.SurfaceOf(session)!.Page();
                await page.EvaluateAsync(
                    """
                    () => {
                      const frame = document.createElement('iframe');
                      frame.srcdoc = '<p>framed</p>';
                      document.body.append(frame);
                      return new Promise((resolve) => { frame.addEventListener('load', resolve, { once: true }); });
                    }
                    """
                );
                await clock.InstallAsync(new BrowserClockInstallOptions { Time = T0 });
                await clock.PauseAtAsync(T0 + Hour);
                await browser.AddInitScriptAsync(
                    WebInitScript.FromFunction("() => { Reflect.set(window, 'after', Date.now()); }")
                );
                Assert.Equal(T0 + Hour, (await ReadAsync(session)).Now);
                Assert.Equal(T0 + Hour, await page.Frames[1].EvaluateAsync<long>("() => Date.now()"));

                // A reset closes the page, so each read takes the current one.
                foreach (var replace in new[] { false, true })
                {
                    if (replace)
                    {
                        await session.ClearStateAsync(Operation(), None);
                    }

                    await OpenAsync(session, app);
                    var scripts = await WebEngine
                        .SurfaceOf(session)!
                        .Page()
                        .EvaluateAsync<JsonElement>("() => ({ before: window.before, after: window.after })");
                    var before = scripts.GetProperty("before").GetInt64();
                    var after = scripts.GetProperty("after").GetInt64();
                    Assert.True(Math.Abs(before - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) < Hour);
                    Assert.Equal(T0 + Hour, after);
                }
            }
        );
    }

    [Fact]
    public async Task Scrolls_observes_and_reads_the_viewport_while_the_page_clock_is_paused()
    {
        // A wait on the page's own timers would hold a step until this budget ends.
        await InAttemptAsync(
            async (session, app, clock, _) =>
            {
                await clock.InstallAsync(new BrowserClockInstallOptions { Time = T0 });
                await clock.PauseAtAsync(T0 + Hour);
                await OpenAsync(session, app);
                var op = Operation(5_000);
                await session.ObserveAsync(op, None);
                await session.SwipeAsync(ScrollDirection.Down, null, op, None);
                var page = WebEngine.SurfaceOf(session)!.Page();
                Assert.True(await page.EvaluateAsync<double>("() => window.scrollY") > 0);
                var footer = Assert.Single(
                    await session.LocateAsync(
                        new LocatorExpression { Kind = "selector", Selector = "#footer" },
                        TimeSpan.FromSeconds(5),
                        op,
                        None
                    )
                );

                // The engine's own reads queue nothing in the page's clock: a pending timer would change its requestIdleCallback.
                Task<int> Timers() => page.EvaluateAsync<int>("() => window.__pwClock.controller.countTimers()");
                var before = await Timers();
                Assert.Equal(new Intersection(false, 0), await session.ViewportIntersectionAsync(footer, op, None));
                Assert.Equal(before, await Timers());
                var read = await ReadAsync(session);
                Assert.Equal((T0 + Hour, "waiting"), (read.Now, read.Timer));
            },
            5_000
        );
    }

    /// <summary>
    /// Partial: a .NET <c>BrowserClockInstallOptions</c> is a typed record, so upstream's plain-object and unknown-option refusals have no counterpart, and a
    /// <see cref="DateTimeOffset"/> is never an invalid date.
    /// </summary>
    [Fact]
    public async Task Partial_Refuses_an_argument_Playwright_would_misread_or_reject_changing_nothing()
    {
        await InAttemptAsync(
            async (session, app, clock, _) =>
            {
                static async Task RefusedAsync(Task call, string? message = null)
                {
                    var error = await Assert.ThrowsAsync<TestException>(() => call);
                    Assert.Equal("INVALID_ARGUMENT", error.Code);
                    if (message is not null)
                    {
                        Assert.Equal(message, error.Message);
                    }
                }

                await RefusedAsync(
                    clock.PauseAtAsync(""),
                    "browser.clock.pauseAt takes epoch milliseconds, a date string, or a valid Date, got \"\""
                );
                await RefusedAsync(clock.SetFixedTimeAsync(double.NaN));
                await RefusedAsync(clock.SetSystemTimeAsync("soon"));
                await RefusedAsync(
                    clock.RunForAsync(-1),
                    "browser.clock.runFor takes milliseconds of zero or more, or 'ss', 'mm:ss', or 'hh:mm:ss', got -1"
                );
                await RefusedAsync(clock.FastForwardAsync("01:60"));
                await RefusedAsync(clock.RunForAsync("1:2:3:4"));
                await OpenAsync(session, app);
                Assert.Equal("waiting", (await ReadAsync(session)).Timer);
                Assert.True(
                    Math.Abs((await ReadAsync(session)).Now - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) < Hour
                );

                // Back in time is the test's argument too, though only the page knows its clock.
                await clock.InstallAsync(new BrowserClockInstallOptions { Time = T0 });
                await clock.PauseAtAsync(T0 + Hour);
                await RefusedAsync(
                    clock.PauseAtAsync(T0),
                    "browser.clock.pauseAt: the time is before the clock's current time"
                );
            }
        );
    }

    [Fact]
    public async Task Keeps_a_failed_or_refused_calls_effect_the_same_in_the_page_the_next_document_and_after_app_clearState()
    {
        await InAttemptAsync(
            async (session, app, clock, _) =>
            {
                await clock.InstallAsync(new BrowserClockInstallOptions { Time = T0 });
                await clock.PauseAtAsync(T0 + Hour);
                await OpenAsync(session, app);
                await WebEngine
                    .SurfaceOf(session)!
                    .Page()
                    .EvaluateAsync("() => { setTimeout(() => { throw new Error('boom'); }, 10); }");

                // Playwright logs a call for the next document before it runs it, so a failed one counts there too.
                var failed = await Assert.ThrowsAsync<TestException>(() => clock.RunForAsync(100));
                Assert.Equal("EVALUATE_FAILED", failed.Code);

                // A refused one must not reach that log.
                var refused = await Assert.ThrowsAsync<TestException>(() => clock.PauseAtAsync(T0));
                Assert.Equal("INVALID_ARGUMENT", refused.Code);
                Assert.Equal(T0 + Hour + 100, (await ReadAsync(session)).Now);
                await OpenAsync(session, app);
                Assert.Equal(T0 + Hour + 100, (await ReadAsync(session)).Now);
                await session.ClearStateAsync(Operation(), None);
                await OpenAsync(session, app);
                Assert.Equal(T0 + Hour + 100, (await ReadAsync(session)).Now);
            }
        );
    }

    [Fact]
    public async Task Fails_a_call_whose_page_timer_throws_as_the_pages_and_bounds_a_timer_loop_that_never_ends()
    {
        await InAttemptAsync(
            async (session, app, clock, _) =>
            {
                await clock.InstallAsync(new BrowserClockInstallOptions { Time = T0 });
                await clock.PauseAtAsync(T0 + Hour);
                await OpenAsync(session, app);
                var page = WebEngine.SurfaceOf(session)!.Page();
                await page.EvaluateAsync("() => { setTimeout(() => { throw new Error('boom'); }, 10); }");
                var thrown = await Assert.ThrowsAsync<TestException>(() => clock.RunForAsync(100));
                Assert.Equal("EVALUATE_FAILED", thrown.Code);
                Assert.Equal("browser.clock.runFor: a page timer threw: Error: boom", thrown.Message);

                await page.EvaluateAsync("() => { setInterval(() => undefined, 1); }");
                var timeout = await Assert.ThrowsAsync<EngineException>(() => clock.RunForAsync(10 * Hour));
                Assert.Equal("OPERATION_TIMEOUT", timeout.Code);
            },
            2_000
        );
    }
}
