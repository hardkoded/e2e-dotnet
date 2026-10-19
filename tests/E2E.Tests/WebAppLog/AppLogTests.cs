// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Tests.WebAppLog;

/// <summary>
/// Upstream's <c>describe('app log')</c> (<c>packages/web/tests/integration/app-log.test.ts</c>; <c>tests/unit/app-log.test.ts</c> is
/// <c>AppLog/</c>): what a page does on its own during an attempt reaches the harness as one line each, from a real browser: console
/// output, exceptions nothing caught, requests that failed, responses with an error status, and where the page went. A console echo of
/// a failed load, a request the page abandoned, and a navigation the engine made itself are left out, so each event is one line.
/// Each action and <c>open</c> hands the harness the screen it left, and the attempt says which browser it ran on.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed partial class AppLogTests
{
    private const string Page = """
        <!doctype html><html><body><h1>Todos</h1><script>
          console.log('ready');
          console.warn('deprecated prop');
          console.error('failed to load todos');
          fetch('/api/todos');
          fetch('/api/missing');
          fetch('/api/reset').catch(() => undefined);
          setTimeout(() => { throw new RangeError('render loop exceeded'); }, 0);
        </script></body></html>
        """;

    private const string Moving = """
        <!doctype html><html><body><h1>Moving</h1><button onclick="window.open('/tab', '_blank')">Help</button><script>
          const frame = document.createElement('iframe');
          frame.name = 'pay';
          frame.src = '/embed';
          document.body.append(frame);
          setTimeout(() => { location.href = '/next'; }, 1000);
        </script></body></html>
        """;

    /// <summary>The pages a navigation lands on, each naming its own path; served from here, never from the request.</summary>
    private static readonly Dictionary<string, string> Landings = new[] { "/next", "/tab", "/embed" }.ToDictionary(landing => landing, landing => $"<!doctype html><h1>{landing}</h1>");

    private static Task<TinySite> StartSiteAsync() => TinySite.StartAsync(async context =>
    {
        var path = context.Request.Url!.AbsolutePath;
        if (path == "/api/todos")
        {
            context.Response.StatusCode = 500;
            context.Response.StatusDescription = "Internal Server Error";
            context.Response.Close(Encoding.UTF8.GetBytes("{}"), willBlock: false);
        }
        else if (path == "/")
        {
            await TinySite.RespondAsync(context, Page);
        }
        else if (path == "/moving")
        {
            await TinySite.RespondAsync(context, Moving);
        }
        else if (Landings.TryGetValue(path, out var landing))
        {
            await TinySite.RespondAsync(context, landing);
        }
        else
        {
            context.Response.StatusCode = 404;
            context.Response.StatusDescription = "Not Found";
            context.Response.Close();
        }
    });

    [Fact]
    public async Task Reports_console_output_uncaught_exceptions_and_error_responses_one_line_each()
    {
        using var site = await StartSiteAsync();
        var url = site.Url.TrimEnd('/');
        var entries = new ConcurrentQueue<AppLogEntry>();
        await using var session = await new WebEngine(headless: true).StartAsync(new EngineStartOptions { BaseUrl = site.Url, AppLog = entries.Enqueue }, CancellationToken.None);

        // Upstream's server destroys the socket of /api/reset. An HttpListener cannot, so a route aborts the request instead.
        await ((IBrowserSession)session).RouteAsync(address => address.EndsWith("/api/reset", StringComparison.Ordinal), route => route.AbortAsync(), CancellationToken.None);
        await session.OpenAsync(site.Url, CancellationToken.None);
        await WaitUntilAsync(() => entries.Count >= 7);

        // A request the page abandoned, the echo of a failed load, and the missing icon add nothing: seven lines, no more.
        await Task.Delay(300);
        var logged = entries.ToArray();
        Assert.Equal(7, logged.Length);
        Assert.Contains(logged, entry => entry is { Source: AppLogSource.Console, Level: AppLogLevel.Info } && ReadyLine().IsMatch(entry.Text));
        Assert.Contains(logged, entry => entry is { Source: AppLogSource.Console, Level: AppLogLevel.Warning } && DeprecatedLine().IsMatch(entry.Text));
        Assert.Contains(logged, entry => entry is { Source: AppLogSource.Console, Level: AppLogLevel.Error } && entry.Text.StartsWith("failed to load todos ", StringComparison.Ordinal));
        Assert.Contains(logged, entry => entry == new AppLogEntry(AppLogSource.Network, AppLogLevel.Error, $"GET {url}/api/todos 500 Internal Server Error"));
        Assert.Contains(logged, entry => entry == new AppLogEntry(AppLogSource.Network, AppLogLevel.Warning, $"GET {url}/api/missing 404 Not Found"));
        Assert.Contains(logged, entry => entry is { Source: AppLogSource.Error, Level: AppLogLevel.Error } && entry.Text.StartsWith("RangeError: render loop exceeded", StringComparison.Ordinal));
        Assert.Contains(logged, entry => entry is { Source: AppLogSource.Network, Level: AppLogLevel.Error } && entry.Text.StartsWith($"GET {url}/api/reset net::ERR_", StringComparison.Ordinal));
        Assert.DoesNotContain(logged, entry => entry.Text.StartsWith("Failed to load resource", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Tells_where_the_page_went_apart_from_its_own_navigations_the_screen_each_action_left_and_the_browser()
    {
        using var site = await StartSiteAsync();
        var url = site.Url.TrimEnd('/');
        var went = new ConcurrentQueue<string>();
        var screens = new ConcurrentQueue<Observation>();
        var facts = new ConcurrentQueue<IReadOnlyDictionary<string, string>>();
        await using var session = await new WebEngine(headless: true).StartAsync(
            new EngineStartOptions { BaseUrl = site.Url, AppLog = _ => { }, Navigation = went.Enqueue, Screen = screens.Enqueue, Environment = facts.Enqueue },
            CancellationToken.None);

        await session.OpenAsync($"{url}/moving", CancellationToken.None);

        var opened = Assert.Single(screens);
        Assert.Equal("/moving", opened.Route);

        // The screen is the trace's, never published: a ref from the observation acts after it, twice.
        var observed = await session.ObserveAsync(CancellationToken.None);
        var help = observed.Roots.SelectMany(Flatten).First(node => node.Role == "button");
        await session.PerformAsync(help, new LocatorAction.Tap(), CancellationToken.None);
        await session.PerformAsync(help, new LocatorAction.Tap(), CancellationToken.None);
        Assert.Equal(3, screens.Count);

        await WaitUntilAsync(() => went.Contains($"navigated to {url}/next"));
        Assert.Contains($"navigated to {url}/next", went);
        Assert.Contains($"frame \"pay\" loaded {url}/embed", went);
        Assert.Contains($"the app opened a new tab at {url}/tab; the test stays on its page", went);
        Assert.DoesNotContain(went, line => line.Contains("/moving", StringComparison.Ordinal));
        Assert.DoesNotContain(went, line => line.StartsWith("the new tab navigated", StringComparison.Ordinal));
        await WaitUntilAsync(() => !facts.IsEmpty);
        var told = Assert.Single(facts);
        Assert.Matches(ChromiumVersion(), told["browser"]);
        Assert.Contains("Chrome/", told["user agent"], StringComparison.Ordinal);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }
    }

    private static IEnumerable<SemanticNode> Flatten(SemanticNode node) => [node, .. node.Children.SelectMany(Flatten)];

    [GeneratedRegex(@"^ready \(at ")]
    private static partial Regex ReadyLine();

    [GeneratedRegex(@"^deprecated prop \(at http://127\.0\.0\.1:\d+/:\d+\)$")]
    private static partial Regex DeprecatedLine();

    [GeneratedRegex(@"^chromium \d+\.")]
    private static partial Regex ChromiumVersion();
}
