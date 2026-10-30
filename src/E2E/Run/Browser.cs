// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>
/// The browser the attempt drives, showing one active tab. It needs an engine
/// session that implements <see cref="IBrowserSession"/>, such as
/// <see cref="WebEngine"/>; on any other engine each member fails with
/// <c>UNSUPPORTED_CAPABILITY</c>.
/// </summary>
public sealed class Browser
{
    // An init script argument is page source, so a Secret anywhere in it is refused.
    // Names and nulls are kept as written, as JSON.stringify and EvaluateAsync keep them.
    private static readonly JsonSerializerOptions InitScriptArgumentOptions = new()
    {
        Converters = { new SecretRefusal() },
    };

    private readonly IEngineSession _session;
    private readonly string _platform;
    private readonly string? _baseUrl;
    private readonly string _projectRoot;
    private readonly TimeSpan _actionTimeout;
    private readonly TimeSpan _assertionTimeout;
    private readonly Func<CancellationToken> _token;
    private readonly Action _verified;
    private readonly List<(object Pattern, Func<IBrowserRoute, Task> Handler)> _routes = [];

    internal Browser(IEngineSession session, string platform, string? baseUrl, string projectRoot, TimeSpan actionTimeout, TimeSpan assertionTimeout, Func<CancellationToken> token, Action verified, SoftFailures softFailures)
    {
        _session = session;
        _platform = platform;
        _baseUrl = baseUrl;
        _projectRoot = projectRoot;
        _actionTimeout = actionTimeout;
        _assertionTimeout = assertionTimeout;
        _token = token;
        _verified = verified;
        SoftFailures = softFailures;
        Keyboard = new BrowserKeyboard(this);
        Mouse = new BrowserMouse(this);
    }

    /// <summary>Viewport-level keyboard, for whatever has focus.</summary>
    public BrowserKeyboard Keyboard { get; }

    /// <summary>Direct pointer input from test code, independent of agent observations.</summary>
    public BrowserMouse Mouse { get; }

    internal SoftFailures SoftFailures { get; }

    /// <summary>Replaces the secrets the attempt knows in text a failure quotes from the page; the text as it is for a browser outside a session.</summary>
    internal Func<string, string> Redact { get; set; } = static text => text;

    internal TimeSpan AssertionTimeout => _assertionTimeout;

    internal string? BaseUrl => _baseUrl;

    internal void NotifyVerified() => _verified();

    /// <summary>The attempt's step timeline; null for a browser outside a session, whose calls then run unrecorded.</summary>
    internal StepRecorder? Steps { get; set; }

    /// <summary>Runs one call that touches the app as a recorded step of the attempt, or as it is without a timeline.</summary>
    internal Task RunStepAsync(StepKind kind, string api, string label, Func<Task> body, bool verifies = false) =>
        Steps is null ? body() : Steps.RunAsync(kind, api, label, body, new StepRunOptions { Verifies = verifies });

    /// <summary>Runs one call that touches the app and returns a value as a recorded step of the attempt, or as it is without a timeline.</summary>
    internal Task<T> RunStepAsync<T>(StepKind kind, string api, string label, Func<Task<T>> body, bool verifies = false) =>
        Steps is null ? body() : Steps.RunAsync(kind, api, label, body, new StepRunOptions { Verifies = verifies });

    /// <summary>Tells the attempt that an assertion failed on the app. Nothing outside a session listens.</summary>
    internal Action Failed { get; set; } = static () => { };

    internal void NotifyFailed() => Failed();

    /// <summary>Keeps a file the fixture wrote as an artifact of the running step (upstream's <c>attachArtifact</c>). Nothing outside a session keeps it.</summary>
    internal Action<string, string> Attach { get; set; } = static (_, _) => { };

    /// <summary>
    /// Navigates to <paramref name="url"/>, resolved against the base URL like <c>App.OpenAsync</c>,
    /// and waits for <see cref="GotoOptions.WaitUntil"/> within <see cref="GotoOptions.Timeout"/>.
    /// </summary>
    public Task GotoAsync(string url, GotoOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (options?.Timeout is { } budget)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(budget, TimeSpan.Zero, nameof(options));
        }

        return RunStepAsync(StepKind.Resource, "browser.goto", url, () =>
        {
            var resolved = Routes.Resolve(_baseUrl, url);
            return AppNotOpen.GuardAsync(() => Require("goto").GotoAsync(resolved, options?.WaitUntil ?? GotoWaitUntil.Load, options?.Timeout ?? _actionTimeout, Token(cancellationToken)));
        });
    }

    /// <summary>Reloads the current document.</summary>
    public Task ReloadAsync(CancellationToken cancellationToken = default) =>
        RunStepAsync(StepKind.Resource, "browser.reload", "", () => AppNotOpen.GuardAsync(() => Require("reload").ReloadAsync(Token(cancellationToken))));

    /// <summary>Navigates the history back once.</summary>
    public Task BackAsync(CancellationToken cancellationToken = default) =>
        RunStepAsync(StepKind.Resource, "browser.back", "", () => AppNotOpen.GuardAsync(() => Require("back").BackAsync(Token(cancellationToken))));

    /// <summary>Navigates the history forward once.</summary>
    public Task ForwardAsync(CancellationToken cancellationToken = default) =>
        RunStepAsync(StepKind.Resource, "browser.forward", "", () => AppNotOpen.GuardAsync(() => Require("forward").ForwardAsync(Token(cancellationToken))));

    /// <summary>Returns the current URL.</summary>
    public Task<string> UrlAsync(CancellationToken cancellationToken = default) =>
        RunStepAsync(StepKind.Resource, "browser.url", "", () => ReadUrlAsync(cancellationToken));

    /// <summary>The URL read the <c>expect(browser)</c> matchers poll with, which records no step of its own (upstream's <c>currentUrl</c>).</summary>
    internal Task<string> ReadUrlAsync(CancellationToken cancellationToken) =>
        AppNotOpen.GuardAsync(() => Require("url").GetUrlAsync(Token(cancellationToken)));

    /// <summary>The title read the <c>expect(browser)</c> matchers poll with, which records no step of its own (upstream's <c>currentTitle</c>).</summary>
    internal Task<string> ReadTitleAsync(CancellationToken cancellationToken) =>
        AppNotOpen.GuardAsync(() => Require("title").GetTitleAsync(Token(cancellationToken)));

    /// <summary>Returns the current title. A page that does not answer within the action timeout fails with <c>OPERATION_TIMEOUT</c>.</summary>
    public Task<string> TitleAsync(CancellationToken cancellationToken = default) =>
        RunStepAsync(StepKind.Resource, "browser.title", "", () => ReadTitleAsync(cancellationToken));

    /// <summary>
    /// Waits until the current URL equals <paramref name="url"/>, resolved
    /// against the base URL, as <see cref="BrowserExpect.ToHaveURLAsync(string, bool?, TimeSpan?, CancellationToken)"/>.
    /// The default timeout is the assertion timeout.
    /// </summary>
    public Task WaitForURLAsync(string url, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        return RunStepAsync(StepKind.Resource, "browser.waitForURL", url, () =>
        {
            Require("waitForURL");
            return new BrowserExpect(this, negated: false).ToHaveURLAsync(url, timeout: timeout, cancellationToken: cancellationToken);
        }, verifies: true);
    }

    /// <summary>Waits until the current URL matches <paramref name="pattern"/>. The default timeout is the assertion timeout.</summary>
    public Task WaitForURLAsync(Regex pattern, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return RunStepAsync(StepKind.Resource, "browser.waitForURL", Routes.Label(pattern), () =>
        {
            Require("waitForURL");
            return new BrowserExpect(this, negated: false).ToHaveURLAsync(pattern, timeout: timeout, cancellationToken: cancellationToken);
        }, verifies: true);
    }

    /// <summary>
    /// Creates a web-only locator from a CSS or XPath selector, for a control with no accessible name. Same polling and strictness as any
    /// locator. A selector that captures with <c>*</c> before its last part (<c>*css=article &gt;&gt; text=Hello</c>) is refused with
    /// <c>INVALID_LOCATOR</c> when the locator resolves: narrow with <see cref="Locator.Filter(Locator)"/> instead. Upstream's <c>browser.locator</c>.
    /// </summary>
    /// <exception cref="TestException"><c>INVALID_LOCATOR</c>: <paramref name="selector"/> is empty.</exception>
    public Locator Locator(string selector)
    {
        var chosen = FrameScreen.RequireSelector("browser.locator", selector);
        Require("locator");
        return Fixture().Locator(new LocatorExpression { Kind = "selector", Selector = chosen });
    }

    /// <summary>
    /// Creates a screen query scope inside one iframe: every <see cref="Screen"/> query answers from inside the frame. The frame is the
    /// one element <paramref name="selector"/> names; none yet is polled for by an action, and answers no match to a read. Upstream's
    /// <c>browser.frameLocator</c>.
    /// </summary>
    /// <exception cref="TestException"><c>INVALID_LOCATOR</c>: <paramref name="selector"/> is empty.</exception>
    public FrameScreen FrameLocator(string selector)
    {
        var chosen = FrameScreen.RequireSelector("browser.frameLocator", selector);
        Require("frameLocator");
        return new FrameScreen(Fixture().Root, [chosen]);
    }

    /// <summary>The attempt's engine fixture context, which mints this browser's locators. Set once the attempt's screen exists.</summary>
    internal EngineFixtureContext? Context { get; set; }

    private EngineFixtureContext Fixture() =>
        Context ?? throw new InvalidOperationException("This browser belongs to no attempt, so it has no screen to resolve locators on.");

    /// <summary>
    /// Evaluates trusted test code in the page: an expression, or a function
    /// source that is called. The result must be JSON-safe and is deserialized to
    /// <typeparamref name="T"/>. A throwing script fails with <c>EVALUATE_FAILED</c>, and a
    /// script that does not finish within the action timeout with <c>OPERATION_TIMEOUT</c>.
    /// </summary>
    public Task<T?> EvaluateAsync<T>(string expression, CancellationToken cancellationToken = default) =>
        EvaluateCoreAsync<T>(expression, null, hasArg: false, cancellationToken);

    /// <summary>Evaluates a function source in the page with one JSON-safe argument.</summary>
    public Task<T?> EvaluateAsync<T>(string expression, object? arg, CancellationToken cancellationToken = default) =>
        EvaluateCoreAsync<T>(expression, arg, hasArg: true, cancellationToken);

    /// <summary>
    /// Waits for a response whose complete URL matches the glob <paramref name="pattern"/>:
    /// <c>*</c> matches within one path segment, <c>**</c> crosses <c>/</c>, <c>?</c>
    /// matches one character, and <c>\</c> escapes the next one. The timeout bounds
    /// the match only, and defaults to the action timeout.
    /// </summary>
    public Task<WebResponse> WaitForResponseAsync(string pattern, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return WaitForResponseCoreAsync(pattern, () => Routes.CompilePattern(pattern).IsMatch, timeout, cancellationToken);
    }

    /// <summary>Waits for a response whose URL matches <paramref name="pattern"/>. The timeout bounds the match only, and defaults to the action timeout.</summary>
    public Task<WebResponse> WaitForResponseAsync(Regex pattern, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return WaitForResponseCoreAsync(pattern, () => pattern.IsMatch, timeout, cancellationToken);
    }

    /// <summary>
    /// Adds a network route for the rest of the attempt, matching a complete URL with
    /// the glob <paramref name="pattern"/> that <see cref="WaitForResponseAsync(string, TimeSpan?, CancellationToken)"/>
    /// takes. The route registered last runs first. <paramref name="handler"/> must take
    /// exactly one decision on the <see cref="WebRoute"/>. A handler that takes none,
    /// takes two, or throws fails the next step with that error, and an undecided
    /// request is aborted.
    /// </summary>
    public Task RouteAsync(string pattern, Func<WebRoute, Task> handler, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return RouteCoreAsync(pattern, handler, cancellationToken);
    }

    /// <summary>Adds a network route for the URLs <paramref name="pattern"/> matches. See <see cref="RouteAsync(string, Func{WebRoute, Task}, CancellationToken)"/>.</summary>
    public Task RouteAsync(Regex pattern, Func<WebRoute, Task> handler, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return RouteCoreAsync(pattern, handler, cancellationToken);
    }

    /// <summary>Removes every route added with the same glob.</summary>
    public Task UnrouteAsync(string pattern, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return UnrouteCoreAsync(pattern, cancellationToken);
    }

    /// <summary>Removes every route added with a regex of the same source and options.</summary>
    public Task UnrouteAsync(Regex pattern, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return UnrouteCoreAsync(pattern, cancellationToken);
    }

    /// <summary>Removes every route added through this browser. The configured site headers stay.</summary>
    public Task UnrouteAllAsync(CancellationToken cancellationToken = default) =>
        RunStepAsync(StepKind.Resource, "browser.unrouteAll", "", () => RemoveRoutesAsync("unrouteAll", _ => true, cancellationToken));

    /// <summary>
    /// Adds JavaScript source every document runs before the page's own, in
    /// every tab and frame, from the next navigation on, for the rest of the attempt.
    /// </summary>
    public Task AddInitScriptAsync(string script, CancellationToken cancellationToken = default) =>
        AddInitScriptAsync(WebInitScript.FromSource(script), cancellationToken);

    /// <summary>
    /// Adds an init script: source, a file of it (<see cref="WebInitScript.FromPath"/>),
    /// or a function the page calls with no argument (<see cref="WebInitScript.FromFunction"/>).
    /// </summary>
    public Task AddInitScriptAsync(WebInitScript script, CancellationToken cancellationToken = default) =>
        AddInitScriptCoreAsync(script, null, hasArg: false, cancellationToken);

    /// <summary>
    /// Adds an init script function (<see cref="WebInitScript.FromFunction"/>) the
    /// page calls with one JSON-safe argument. Only a function takes an argument.
    /// </summary>
    public Task AddInitScriptAsync(WebInitScript script, object? arg, CancellationToken cancellationToken = default) =>
        AddInitScriptCoreAsync(script, arg, hasArg: true, cancellationToken);

    /// <summary>Returns the cookies visible to the browser context.</summary>
    public Task<IReadOnlyList<BrowserCookie>> CookiesAsync(CancellationToken cancellationToken = default) =>
        RunStepAsync(StepKind.Resource, "browser.cookies", "", () => Require("cookies").GetCookiesAsync(Token(cancellationToken)));

    /// <summary>
    /// Sets cookies. Each target URL, or the origin a domain cookie is sent to, must pass the URL rule. A relative url resolves against the base URL.
    /// A cookie URL that does not parse, or uses a scheme other than <c>http:</c> or <c>https:</c> (<c>about:blank</c> included),
    /// is <c>POLICY_DENIED</c>.
    /// </summary>
    public Task SetCookiesAsync(IReadOnlyList<BrowserCookie> cookies, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cookies);
        return RunStepAsync(StepKind.Resource, "browser.setCookies", "", () => SetCookiesStepAsync(cookies, cancellationToken));
    }

    /// <summary>
    /// Removes the cookies matching every filter in <paramref name="options"/>. A string matches a field exactly and a regular expression
    /// matches its text (Playwright hands the source to the browser, which runs it as JavaScript). With no options it removes every cookie, HttpOnly ones included. Local and session storage stay.
    /// </summary>
    public Task ClearCookiesAsync(CookieFilter? options = null, CancellationToken cancellationToken = default) =>
        RunStepAsync(StepKind.Resource, "browser.clearCookies", "", () => Require("clearCookies").ClearCookiesAsync(options, Token(cancellationToken)));

    private Task SetCookiesStepAsync(IReadOnlyList<BrowserCookie> cookies, CancellationToken cancellationToken)
    {
        var session = Require("setCookies");
        var scheme = _baseUrl is null ? "http" : new Uri(_baseUrl).Scheme;
        var resolved = new List<BrowserCookie>(cookies.Count);
        foreach (var cookie in cookies)
        {
            ArgumentNullException.ThrowIfNull(cookie);
            if ((cookie.Url is null) == (cookie.Domain is null) || (cookie.Url is not null && cookie.Path is not null))
            {
                throw new TestException("INVALID_ARGUMENT", "Cookie \"" + cookie.Name + "\" needs a url, or a domain with an optional path, not both.");
            }

            // A url cookie is set on the URL the rule resolved, so a relative one
            // lands on the base URL the way OpenAsync would. The rule admits
            // about:blank for navigation, which holds no cookie.
            var target = Routes.Resolve(_baseUrl, cookie.Url ?? scheme + "://" + cookie.Domain!.TrimStart('.'));
            if (!target.StartsWith("http:", StringComparison.Ordinal) && !target.StartsWith("https:", StringComparison.Ordinal))
            {
                throw new TestException("POLICY_DENIED", "cookie URL must be http(s): " + target);
            }

            resolved.Add(cookie.Url is null ? cookie : cookie with { Url = target });
        }

        return session.SetCookiesAsync(resolved, Token(cancellationToken));
    }

    /// <summary>Sets the viewport size for the rest of the attempt.</summary>
    public Task SetViewportAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        return RunStepAsync(StepKind.Resource, "browser.setViewport", "", () => Require("setViewport").SetViewportAsync(width, height, Token(cancellationToken)));
    }

    /// <summary>
    /// Emulates CSS media features on every open tab and every tab opened after them, the app's own included,
    /// for the rest of the attempt, through <c>App.RestartAsync</c> and <c>App.ClearStateAsync</c>: the page's
    /// <c>@media</c> rules and <c>matchMedia</c> both read them. A feature left out keeps its value. It works
    /// before the first navigation, so the page loads with them.
    /// </summary>
    public Task EmulateMediaAsync(WebMedia media, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(media);
        return RunStepAsync(StepKind.Resource, "browser.emulateMedia", media.Label(), () =>
        {
            if (WebMedia.Problem(name => "browser.emulateMedia({ " + name + " })", media) is { } problem)
            {
                throw new TestException("INVALID_ARGUMENT", problem);
            }

            return Require("emulateMedia").EmulateMediaAsync(media, Token(cancellationToken));
        });
    }

    /// <summary>
    /// Registers a handler for native dialogs, of every tab, for the rest of the attempt, and returns its unsubscribe. The newest
    /// registration answers. A dialog with no handler fails the next step and is dismissed; a handler that returns without calling
    /// <see cref="Dialog.AcceptAsync"/> or <see cref="Dialog.DismissAsync"/>, or throws, fails the next step with that error, and its
    /// dialog is dismissed. Pass <see cref="DialogHandler.Accept"/> or <see cref="DialogHandler.Dismiss"/> to answer them all alike.
    /// </summary>
    public async Task<Func<Task>> OnDialogAsync(DialogHandler handler, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var session = Require("onDialog");
        Action unsubscribe = static () => { };
        await RunStepAsync(StepKind.Resource, "browser.onDialog", "", async () => unsubscribe = await session.OnDialogAsync(handler, Token(cancellationToken)).ConfigureAwait(false)).ConfigureAwait(false);
        return () =>
        {
            unsubscribe();
            return Task.CompletedTask;
        };
    }

    /// <summary>Registers a handler that runs <paramref name="handler"/> for each dialog. See <see cref="OnDialogAsync(DialogHandler, CancellationToken)"/>.</summary>
    public Task<Func<Task>> OnDialogAsync(Func<Dialog, Task> handler, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return OnDialogAsync((DialogHandler)handler, cancellationToken);
    }

    /// <summary>
    /// Runs <paramref name="trigger"/> and waits for the download it starts, within <paramref name="timeout"/> (the action timeout by
    /// default). The file is kept under the attempt's <c>downloads</c> artifact folder, as an artifact of this step. A text download
    /// (<c>text/*</c> or <c>application/json</c>) is rewritten in place through the session's secrets when it holds any, and the report
    /// labels it <c>complete</c>; any other download stays as the app served it, <c>incomplete</c>. A trigger that
    /// throws fails the call with its own error, and one that starts no download fails with <c>ACTION_FAILED</c>, naming the wait and
    /// how long the trigger took.
    /// </summary>
    public async Task<BrowserDownload> WaitForDownloadAsync(Func<Task> trigger, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        if (timeout is { } budget)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(budget, TimeSpan.Zero, nameof(timeout));
        }

        var session = Require("waitForDownload");
        BrowserDownload? download = null;
        await RunStepAsync(StepKind.Resource, "browser.waitForDownload", "", async () =>
        {
            download = await AppNotOpen.GuardAsync(() => session.WaitForDownloadAsync(trigger, timeout ?? _actionTimeout, Token(cancellationToken))).ConfigureAwait(false);
            Attach("download", download.Path);
        }).ConfigureAwait(false);
        return download!;
    }

    /// <summary>
    /// Runs <paramref name="trigger"/> once and waits for the first <paramref name="count"/> downloads it starts, within
    /// <paramref name="timeout"/> (the action timeout by default), and returns them in start order. Each file is kept under the attempt's
    /// <c>downloads</c> artifact folder as its own artifact of this step, and a text download is rewritten through the session's secrets
    /// as <c>WaitForDownloadAsync</c> does. One timeout bounds the start, completion and
    /// save of every file. When fewer start in time the call fails with <c>ACTION_FAILED</c> naming both counts; extra downloads are
    /// ignored. A <paramref name="count"/> below one fails with <c>INVALID_ARGUMENT</c> before the trigger runs. A trigger that throws
    /// fails the call with its own error.
    /// </summary>
    public async Task<IReadOnlyList<BrowserDownload>> WaitForDownloadsAsync(Func<Task> trigger, int count, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        if (timeout is { } budget)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(budget, TimeSpan.Zero, nameof(timeout));
        }

        var session = Require("waitForDownloads");
        IReadOnlyList<BrowserDownload> downloads = [];
        await RunStepAsync(StepKind.Resource, "browser.waitForDownloads", "", async () =>
        {
            if (count < 1)
            {
                throw new TestException("INVALID_ARGUMENT", "waitForDownloads: count must be a positive safe integer");
            }

            downloads = await AppNotOpen.GuardAsync(() => session.WaitForDownloadsAsync(trigger, count, timeout ?? _actionTimeout, Token(cancellationToken))).ConfigureAwait(false);
            foreach (var download in downloads)
            {
                Attach("download", download.Path);
            }
        }).ConfigureAwait(false);
        return downloads;
    }

    internal IBrowserSession Require(string operation)
    {
        return _session as IBrowserSession
            ?? throw new EngineException(
                "UNSUPPORTED_CAPABILITY",
                "browser." + operation + " needs a browser engine; the " + _platform + " engine does not provide one.");
    }

    internal CancellationToken Token(CancellationToken cancellationToken) =>
        Time.Link(cancellationToken, _token());

    private Task AddInitScriptCoreAsync(WebInitScript script, object? arg, bool hasArg, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(script);
        return RunStepAsync(StepKind.Resource, "browser.addInitScript", script.Label(), () => AddInitScriptStepAsync(script, arg, hasArg, cancellationToken));
    }

    private async Task AddInitScriptStepAsync(WebInitScript script, object? arg, bool hasArg, CancellationToken cancellationToken)
    {
        if (script.Problem() is { } problem)
        {
            throw new TestException("INVALID_ARGUMENT", "browser.addInitScript script " + problem);
        }

        if (hasArg && script.Function is null)
        {
            throw new TestException("INVALID_ARGUMENT", "browser.addInitScript takes an argument only with a function script");
        }

        string? argument = null;
        try
        {
            argument = hasArg ? JsonSerializer.Serialize(arg, InitScriptArgumentOptions) : null;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new TestException("INVALID_ARGUMENT", "browser.addInitScript argument is not JSON: " + ex.Message, ex);
        }

        var session = Require("addInitScript");
        var token = Token(cancellationToken);
        var source = await script.ReadAsync(argument, _projectRoot, (message, cause) => new TestException("INVALID_ARGUMENT", "browser.addInitScript " + message, cause), token).ConfigureAwait(false);
        await session.AddInitScriptAsync(source, token).ConfigureAwait(false);
    }

    private async Task<T?> EvaluateCoreAsync<T>(string expression, object? arg, bool hasArg, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        var result = await RunStepAsync(StepKind.Resource, "browser.evaluate", "", () =>
            AppNotOpen.GuardAsync(() => Require("evaluate").EvaluateAsync(expression, arg, hasArg, Token(cancellationToken)))).ConfigureAwait(false);
        if (result is not JsonElement element || element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return default;
        }

        try
        {
            return element.Deserialize<T>(JsonDefaults.Options);
        }
        catch (JsonException ex)
        {
            throw new TestException("EVALUATE_FAILED", "evaluate result does not convert to " + typeof(T).Name + ": " + ex.Message, ex);
        }
    }

    private Task<WebResponse> WaitForResponseCoreAsync(object pattern, Func<Func<string, bool>> matcher, TimeSpan? timeout, CancellationToken cancellationToken) =>
        RunStepAsync(StepKind.Resource, "browser.waitForResponse", Routes.Label(pattern), async () =>
        {
            var session = Require("waitForResponse");
            var response = await session.WaitForResponseAsync(matcher(), timeout ?? _actionTimeout, Token(cancellationToken)).ConfigureAwait(false);
            return new WebResponse(response, _actionTimeout, _token);
        });

    private Task RouteCoreAsync(object pattern, Func<WebRoute, Task> handler, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return RunStepAsync(StepKind.Resource, "browser.route", Routes.Label(pattern), () => RouteStepAsync(pattern, handler, cancellationToken));
    }

    private async Task RouteStepAsync(object pattern, Func<WebRoute, Task> handler, CancellationToken cancellationToken)
    {
        var session = Require("route");
        async Task Decide(IBrowserRoute engineRoute)
        {
            var route = new WebRoute(engineRoute, url => Routes.Resolve(_baseUrl, url), file => System.IO.Path.GetFullPath(file, _projectRoot), _actionTimeout, _token);
            await handler(route).ConfigureAwait(false);
            if (!route.Decided)
            {
                throw new TestException("ACTION_FAILED", "route handler returned without calling fulfill, continue, fallback, or abort");
            }
        }

        await session.RouteAsync(Routes.Matcher(pattern), Decide, Token(cancellationToken)).ConfigureAwait(false);
        _routes.Add((pattern, Decide));
    }

    private Task UnrouteCoreAsync(object pattern, CancellationToken cancellationToken) =>
        RunStepAsync(StepKind.Resource, "browser.unroute", Routes.Label(pattern), () =>
            RemoveRoutesAsync("unroute", stored => Routes.PatternsEqual(stored.Pattern, pattern), cancellationToken));

    /// <summary>Unregisters the stored routes <paramref name="match"/> selects, newest first.</summary>
    private async Task RemoveRoutesAsync(string operation, Func<(object Pattern, Func<IBrowserRoute, Task> Handler), bool> match, CancellationToken cancellationToken)
    {
        var session = Require(operation);
        for (var i = _routes.Count - 1; i >= 0; i--)
        {
            if (match(_routes[i]))
            {
                await session.UnrouteAsync(_routes[i].Handler, Token(cancellationToken)).ConfigureAwait(false);
                _routes.RemoveAt(i);
            }
        }
    }

    private sealed class SecretRefusal : System.Text.Json.Serialization.JsonConverter<Secret>
    {
        public override Secret Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, Secret value, JsonSerializerOptions options) =>
            throw new TestException("POLICY_DENIED", "addInitScript argument must not contain a Secret");
    }
}

/// <summary>
/// One request a <see cref="Browser.RouteAsync(string, Func{WebRoute, Task}, CancellationToken)"/>
/// handler intercepted. Take exactly one decision. Each validates its options before
/// it decides, so a rejected option leaves the request undecided, and the request is aborted.
/// </summary>
public sealed class WebRoute
{
    private readonly IBrowserRoute _route;
    private readonly Func<string, string> _resolveUrl;
    private readonly Func<string, string> _resolvePath;
    private readonly TimeSpan _actionTimeout;
    private readonly Func<CancellationToken> _token;

    internal WebRoute(IBrowserRoute route, Func<string, string> resolveUrl, Func<string, string> resolvePath, TimeSpan actionTimeout, Func<CancellationToken> token)
    {
        _route = route;
        _resolveUrl = resolveUrl;
        _resolvePath = resolvePath;
        _actionTimeout = actionTimeout;
        _token = token;
    }

    /// <summary>The intercepted request.</summary>
    public WebRouteRequest Request => _route.Request;

    internal bool Decided { get; private set; }

    /// <summary>
    /// Reads the upstream response without deciding the route or following redirects, under the action
    /// timeout. The site's configured headers are sent. A redirect comes back unfollowed, because
    /// following it could take the request past the navigation rule. Decide the route afterwards
    /// with <see cref="FulfillAsync"/>, <see cref="ContinueAsync"/>, <see cref="FallbackAsync"/>, or <see cref="AbortAsync"/>.
    /// </summary>
    public async Task<WebResponse> FetchAsync()
    {
        var response = await _route.FetchAsync(_actionTimeout, _token()).ConfigureAwait(false);
        return new WebResponse(response, _actionTimeout, _token);
    }

    /// <summary>Answers the request.</summary>
    public async Task FulfillAsync(RouteFulfillResponse response)
    {
        var decision = RouteOptions.ParseFulfill(response, _resolvePath);
        Decide("fulfill");
        await _route.FulfillAsync(decision).ConfigureAwait(false);
    }

    /// <summary>Sends the request to the network once, skipping every other route.</summary>
    public async Task ContinueAsync(RouteContinueOverrides? overrides = null)
    {
        var decision = RouteOptions.ParseContinue(overrides, _route.Request.Url, _resolveUrl);
        Decide("continue");
        await _route.ContinueAsync(decision).ConfigureAwait(false);
    }

    /// <summary>Hands the request to the route registered before this one, or the network when none matches.</summary>
    public async Task FallbackAsync()
    {
        Decide("fallback");
        await _route.FallbackAsync().ConfigureAwait(false);
    }

    /// <summary>Aborts the request.</summary>
    public async Task AbortAsync()
    {
        Decide("abort");
        await _route.AbortAsync().ConfigureAwait(false);
    }

    private void Decide(string name)
    {
        if (Decided)
        {
            throw new TestException("ACTION_FAILED", "route handler already decided; " + name + " called twice");
        }

        Decided = true;
    }
}

/// <summary>A response <see cref="Browser.WaitForResponseAsync(string, TimeSpan?, CancellationToken)"/> matched.</summary>
public sealed class WebResponse
{
    private readonly Task<string> _body;
    private readonly TimeSpan _actionTimeout;
    private readonly Func<CancellationToken> _token;

    internal WebResponse(BrowserResponse response, TimeSpan actionTimeout, Func<CancellationToken> token)
    {
        Url = response.Url;
        Status = response.Status;
        Headers = response.Headers;
        _body = response.Body;
        _actionTimeout = actionTimeout;
        _token = token;
    }

    /// <summary>Response URL.</summary>
    public string Url { get; }

    /// <summary>HTTP status.</summary>
    public int Status { get; }

    /// <summary>Response headers, with lower-cased names.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>
    /// Waits for the body, up to the action timeout, and reads it as text. Fails with
    /// <c>ACTION_FAILED</c> when the body could not be read or did not finish in time.
    /// </summary>
    public async Task<string> TextAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _body.WaitAsync(_actionTimeout, cancellationToken == default ? _token() : cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException) when (!_body.IsCompleted)
        {
            var budget = ((long)_actionTimeout.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
            throw new TestException("ACTION_FAILED", "waitForResponse: response body did not finish within " + budget + "ms");
        }
    }

    /// <summary>Waits for the body as <see cref="TextAsync"/> does and deserializes it as JSON.</summary>
    public async Task<T?> JsonAsync<T>(CancellationToken cancellationToken = default) =>
        JsonSerializer.Deserialize<T>(await TextAsync(cancellationToken).ConfigureAwait(false), JsonDefaults.Options);
}

/// <summary>
/// Keyboard input for whatever holds focus in the active tab. A call the page does not
/// answer within the action timeout fails with <c>ACTION_MAY_HAVE_COMMITTED</c>: the input may have reached the page.
/// </summary>
public sealed class BrowserKeyboard
{
    private readonly Browser _browser;

    internal BrowserKeyboard(Browser browser)
    {
        _browser = browser;
    }

    /// <summary>Sends one key, such as <c>Enter</c> or <c>Control+A</c>.</summary>
    public Task PressAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return _browser.RunStepAsync(StepKind.Resource, "browser.keyboard.press", key, () => AppNotOpen.GuardAsync(() => _browser.Require("keyboard.press").KeyboardPressAsync(key, _browser.Token(cancellationToken))));
    }

    /// <summary>Holds one key until <see cref="UpAsync"/>; a repeated down sends a repeat event.</summary>
    public Task DownAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return _browser.RunStepAsync(StepKind.Resource, "browser.keyboard.down", key, () => AppNotOpen.GuardAsync(() => _browser.Require("keyboard.down").KeyboardDownAsync(key, _browser.Token(cancellationToken))));
    }

    /// <summary>Releases a held key.</summary>
    public Task UpAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return _browser.RunStepAsync(StepKind.Resource, "browser.keyboard.up", key, () => AppNotOpen.GuardAsync(() => _browser.Require("keyboard.up").KeyboardUpAsync(key, _browser.Token(cancellationToken))));
    }

    /// <summary>Types plain text.</summary>
    public Task TypeAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        return _browser.RunStepAsync(StepKind.Resource, "browser.keyboard.type", text.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + " chars", () => AppNotOpen.GuardAsync(() => _browser.Require("keyboard.type").KeyboardTypeAsync(text, _browser.Token(cancellationToken))));
    }
}

/// <summary>
/// Pointer input for the active tab, in CSS pixels. A call the page does not answer
/// within the action timeout fails with <c>ACTION_MAY_HAVE_COMMITTED</c>: the input may have reached the page.
/// </summary>
public sealed class BrowserMouse
{
    private readonly Browser _browser;

    internal BrowserMouse(Browser browser)
    {
        _browser = browser;
    }

    public Task MoveAsync(float x, float y, CancellationToken cancellationToken = default) =>
        _browser.RunStepAsync(StepKind.Resource, "browser.mouse.move", "", () => AppNotOpen.GuardAsync(() => _browser.Require("mouse.move").MouseMoveAsync(x, y, _browser.Token(cancellationToken))));

    public Task WheelAsync(float deltaX, float deltaY, CancellationToken cancellationToken = default) =>
        _browser.RunStepAsync(StepKind.Resource, "browser.mouse.wheel", "", () => AppNotOpen.GuardAsync(() => _browser.Require("mouse.wheel").MouseWheelAsync(deltaX, deltaY, _browser.Token(cancellationToken))));

    /// <summary>Presses the primary button.</summary>
    public Task DownAsync(CancellationToken cancellationToken = default) =>
        _browser.RunStepAsync(StepKind.Resource, "browser.mouse.down", "", () => AppNotOpen.GuardAsync(() => _browser.Require("mouse.down").MouseDownAsync(_browser.Token(cancellationToken))));

    /// <summary>Releases the primary button.</summary>
    public Task UpAsync(CancellationToken cancellationToken = default) =>
        _browser.RunStepAsync(StepKind.Resource, "browser.mouse.up", "", () => AppNotOpen.GuardAsync(() => _browser.Require("mouse.up").MouseUpAsync(_browser.Token(cancellationToken))));
}

/// <summary>
/// The screen scope <see cref="Browser.FrameLocator"/> returns. Every <see cref="Screen"/> query answers from inside the frame, and the
/// web-only escape hatches follow it in: <see cref="Locator(string)"/> for a CSS or XPath selector on a control with no accessible
/// name, and <see cref="FrameLocator(string)"/> for a frame nested inside this one. Upstream's <c>FrameScreen</c>.
/// </summary>
public sealed class FrameScreen : Screen
{
    private readonly IReadOnlyList<string> _frames;

    internal FrameScreen(Screen screen, IReadOnlyList<string> frames)
        : base(screen, Scope(frames))
    {
        _frames = frames;
    }

    /// <summary>Creates a web-only CSS or XPath locator inside this frame.</summary>
    /// <exception cref="TestException"><c>INVALID_LOCATOR</c>: <paramref name="selector"/> is empty.</exception>
    public Locator Locator(string selector) =>
        new(this, Scope(_frames)(new LocatorExpression { Kind = "selector", Selector = RequireSelector("locator", selector) }));

    /// <summary>Creates a screen query scope inside an iframe nested in this frame.</summary>
    /// <exception cref="TestException"><c>INVALID_LOCATOR</c>: <paramref name="selector"/> is empty.</exception>
    public FrameScreen FrameLocator(string selector) => new(Root, [.. _frames, RequireSelector("frameLocator", selector)]);

    /// <summary>
    /// The scope for the innermost of <paramref name="frames"/> (outermost first). Every query and both escape hatches compile through
    /// the same <c>frame</c> chain, so a <c>locator</c> inside it resolves against the innermost document.
    /// </summary>
    private static Func<LocatorExpression, LocatorExpression> Scope(IReadOnlyList<string> frames) => expression =>
    {
        for (var i = frames.Count - 1; i >= 0; i--)
        {
            expression = new LocatorExpression { Kind = "frame", Selector = frames[i], Source = expression };
        }

        return expression;
    };

    internal static string RequireSelector(string method, string? selector)
    {
        if (string.IsNullOrEmpty(selector))
        {
            throw new TestException("INVALID_LOCATOR", method + "() requires a nonempty selector");
        }

        return selector;
    }
}
