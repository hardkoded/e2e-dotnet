// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>Where the app log tells where the page went, and what it asks the engine to tell its navigations from the app's (upstream's <c>NavigationLog</c>).</summary>
/// <param name="Navigated">Takes one line saying where the page went.</param>
/// <param name="OwnNavigation">Whether a navigation of the test's page to the URL is one the engine started, which its step already tells.</param>
/// <param name="TestPage">The page the test drives; every other page is a tab the app opened.</param>
internal sealed record NavigationLog(Action<string> Navigated, Func<string, bool> OwnNavigation, Func<IPage?> TestPage);

/// <summary>
/// What the page did on its own during an attempt, for the harness's app log (upstream's <c>installAppLog</c>, <c>web/src/app-log.ts</c>):
/// console output, exceptions nothing caught, requests that failed, and responses with an error status; apart from it, where the
/// page went (a navigation, a new tab, a frame loading). These are the first things to read when a step fails because the app
/// broke rather than the test.
/// </summary>
internal static class AppLog
{
    /// <summary>Documents that are no place the app went: a fresh tab, an inline frame.</summary>
    private static readonly HashSet<string> NoPlace = new(StringComparer.Ordinal) { "about:blank", "about:srcdoc" };

    /// <summary>Chrome's console echo of a failed load, which the network lines already tell.</summary>
    private const string LoadEcho = "Failed to load resource:";

    /// <summary>Console message types worth a line; <c>debug</c> and the rest are the app talking to itself.</summary>
    private static AppLogLevel? ConsoleLevel(string type) => type switch
    {
        "error" or "assert" => AppLogLevel.Error,
        "warning" => AppLogLevel.Warning,
        "log" or "info" => AppLogLevel.Info,
        _ => null,
    };

    /// <summary>Reports the context's console output, uncaught exceptions, failed requests, and navigations to <paramref name="log"/>, from now on.</summary>
    public static void Install(IBrowserContext context, Action<AppLogEntry> log, NavigationLog navigation)
    {
        // The harness's sink never fails a page event.
        void Report(AppLogEntry entry)
        {
            try
            {
                log(entry);
            }
            catch (Exception)
            {
                // Dropped.
            }
        }

        void Went(string line)
        {
            try
            {
                navigation.Navigated(line);
            }
            catch (Exception)
            {
                // Dropped.
            }
        }

        context.Console += (_, message) =>
        {
            if (ConsoleLevel(message.Type) is { } level && !message.Text.StartsWith(LoadEcho, StringComparison.Ordinal))
            {
                Report(new AppLogEntry(AppLogSource.Console, level, ConsoleText(message)));
            }
        };
        context.WebError += (_, webError) => Report(new AppLogEntry(AppLogSource.Error, AppLogLevel.Error, ErrorText(webError.Error)));
        context.RequestFailed += (_, request) =>
        {
            var failure = request.Failure ?? "failed";
            if (!IsCancelled(failure))
            {
                Report(new AppLogEntry(AppLogSource.Network, AppLogLevel.Error, RequestLine(request) + " " + failure));
            }
        };
        context.Response += (_, response) =>
        {
            if (response.Status >= 400 && !IsMissingFavicon(response))
            {
                Report(new AppLogEntry(AppLogSource.Network, response.Status >= 500 ? AppLogLevel.Error : AppLogLevel.Warning, ResponseLine(response)));
            }
        };

        void Watch(IPage page)
        {
            // A tab's first document is told by its opening.
            var opened = false;
            page.FrameNavigated += (_, frame) =>
            {
                var tab = !ReferenceEquals(page, navigation.TestPage());
                if (tab && ReferenceEquals(frame, page.MainFrame) && !opened && !NoPlace.Contains(frame.Url))
                {
                    opened = true;
                    return;
                }

                if (NavigationLine(page, frame, tab, navigation) is { } line)
                {
                    Went(line);
                }
            };

            // A tab the app opened (window.open, a target="_blank" link); the test stays on its own page.
            page.Popup += (_, tab) =>
            {
                var at = NoPlace.Contains(tab.Url) ? "" : " at " + tab.Url;
                Went("the app opened a new tab" + at + "; the test stays on its page");
            };
        }

        foreach (var page in context.Pages)
        {
            Watch(page);
        }

        context.Page += (_, page) => Watch(page);
    }

    /// <summary>
    /// Whether a failed request is one the browser cancelled because the page moved on, not the app failing: Chromium's and
    /// Firefox's abort codes, and WebKit's text, read the way Playwright reads each browser's cancellation.
    /// </summary>
    private static bool IsCancelled(string errorText) =>
        errorText is "net::ERR_ABORTED" or "NS_BINDING_ABORTED" || errorText.Contains("cancelled", StringComparison.Ordinal);

    /// <summary>
    /// <c>navigated to /checkout</c>, <c>the new tab navigated to /help</c>, <c>frame "pay" loaded /embed</c>: one navigation as a
    /// line, or null for one not worth telling.
    /// </summary>
    private static string? NavigationLine(IPage page, IFrame frame, bool tab, NavigationLog navigation)
    {
        var url = frame.Url;
        if (NoPlace.Contains(url) || url.StartsWith("data:", StringComparison.Ordinal))
        {
            return null;
        }

        if (ReferenceEquals(frame, page.MainFrame))
        {
            return tab ? "the new tab navigated to " + url : navigation.OwnNavigation(url) ? null : "navigated to " + url;
        }

        var name = frame.Name;
        return "frame" + (name.Length == 0 ? "" : " \"" + name + "\"") + " loaded " + url;
    }

    /// <summary>
    /// <c>RangeError: render loop exceeded at http://app.test/app.js:9:15</c>: the error as the page threw it, and the first frame
    /// of its stack when the browser says one.
    /// </summary>
    private static string ErrorText(string error)
    {
        var lines = error.Split('\n');
        var top = lines.Select(static line => line.Trim()).FirstOrDefault(static line => line.StartsWith("at ", StringComparison.Ordinal));
        var message = string.Join(' ', lines.TakeWhile(static line => !line.TrimStart().StartsWith("at ", StringComparison.Ordinal)).Select(static line => line.Trim()));
        return top is null ? message : message + " " + top;
    }

    /// <summary><c>message (at /app.js:12)</c>: the text, and where it was logged from when the browser says.</summary>
    private static string ConsoleText(IConsoleMessage message)
    {
        // The location is URL:line:column, both numbers from zero; an empty one has no URL.
        var location = message.Location;
        var column = location.LastIndexOf(':');
        var line = column > 0 ? location.LastIndexOf(':', column - 1) : -1;
        if (line <= 0
            || !int.TryParse(location.AsSpan(line + 1, column - line - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var lineNumber))
        {
            return message.Text;
        }

        return message.Text + " (at " + location[..line] + ":" + (lineNumber + 1).ToString(CultureInfo.InvariantCulture) + ")";
    }

    /// <summary>A browser's own request for a site icon the app never served: noise on every page of most apps.</summary>
    private static bool IsMissingFavicon(IResponse response) =>
        response.Status == 404 && Uri.TryCreate(response.Url, UriKind.Absolute, out var url) && url.AbsolutePath == "/favicon.ico";

    /// <summary><c>GET https://app.test/api/todos</c>: the request line a network entry starts with.</summary>
    private static string RequestLine(IRequest request) => request.Method + " " + request.Url;

    /// <summary><c>GET https://app.test/api/todos 500 Internal Server Error</c>: the request line and the status it got.</summary>
    private static string ResponseLine(IResponse response)
    {
        var text = response.StatusText;
        return RequestLine(response.Request) + " " + response.Status.ToString(CultureInfo.InvariantCulture) + (text.Length == 0 ? "" : " " + text);
    }
}
