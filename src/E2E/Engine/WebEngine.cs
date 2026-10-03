// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>A page size in CSS pixels.</summary>
public sealed record WebViewport(int Width, int Height)
{
    /// <summary>The size every attempt's page starts with unless the options say otherwise: 1280 by 720.</summary>
    public static WebViewport Default { get; } = new(1280, 720);
}

/// <summary>HTTP basic authentication the browser answers a <c>401</c> challenge with.</summary>
public sealed class WebBasicAuth
{
    public WebBasicAuth(string username, string password)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);
        Username = username;
        Password = password;
    }

    public WebBasicAuth(string username, Secret password)
        : this(username, (password ?? throw new ArgumentNullException(nameof(password))).Value)
    {
    }

    /// <summary>The user name. RFC 7617 does not allow <c>:</c> in one.</summary>
    public string Username { get; }

    internal string Password { get; }
}

/// <summary>Attach to a remote Chromium over CDP instead of launching one.</summary>
public sealed class WebConnectOptions
{
    /// <summary>
    /// Resolves the CDP endpoint (a <c>ws://</c>, <c>wss://</c>, or <c>http://</c> DevTools URL).
    /// Called when each attempt starts, so a hosted endpoint provisioned per run can be used.
    /// </summary>
    public required Func<CancellationToken, Task<string>> CdpEndpoint { get; init; }
}

/// <summary>Options of the browser engine: how it drives the app.</summary>
public sealed class WebEngineOptions
{
    /// <summary>Runs Chromium without a window. When unset, <c>E2E_HEADLESS=0</c> or <c>false</c> shows the window; headless otherwise.</summary>
    public bool? Headless { get; init; }

    /// <summary>
    /// Initial viewport of every attempt's page; default 1280 by 720. <see langword="null"/>
    /// emulates no size: the page fills the browser window, whatever size it has.
    /// </summary>
    public WebViewport? Viewport { get; init; } = WebViewport.Default;

    /// <summary>The attribute that carries an element's test id: what <c>GetByTestId</c> resolves and <see cref="SemanticNode.TestId"/> reports.</summary>
    public string TestIdAttribute { get; init; } = "data-testid";

    /// <summary>
    /// Headers added to every request bound for the app's host (the host of the
    /// session's base URL). Requests to any other host never carry them, so a
    /// secret header stays with the app it unlocks. Names are case-insensitive
    /// and replace a header the page already sends under the same name.
    /// Routing every request turns the browser's HTTP cache off.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>Basic-auth credentials the browser answers a challenge with, wherever one is issued.</summary>
    public WebBasicAuth? BasicAuth { get; init; }

    /// <summary>The <c>User-Agent</c> the browser sends and <c>navigator.userAgent</c> reports. Defaults to the browser's own.</summary>
    public string? UserAgent { get; init; }

    /// <summary>Attach to a remote Chromium over CDP instead of launching a local one.</summary>
    public WebConnectOptions? Connect { get; init; }
}

/// <summary>
/// Browser engine for the web target. It drives Chromium through Playwright,
/// reads a semantic tree (role, name, text, test id, state), and performs the
/// same node actions the document engine does. Install browsers once with
/// <c>playwright.ps1 install chromium</c> from this project's build output.
/// </summary>
public sealed class WebEngine : IEngine
{
    public const string EngineVersion = "1.1.0";

    private readonly WebEngineOptions _options;
    private readonly bool _headless;

    public WebEngine(bool? headless = null)
        : this(new WebEngineOptions { Headless = headless })
    {
    }

    public WebEngine(WebEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Validate(options);
        _options = options;
        if (options.Headless is bool chosen)
        {
            _headless = chosen;
        }
        else
        {
            var env = Environment.GetEnvironmentVariable("E2E_HEADLESS");
            _headless = !string.Equals(env, "0", StringComparison.Ordinal) && !string.Equals(env, "false", StringComparison.OrdinalIgnoreCase);
        }
    }

    public string Platform => "web";

    public string Version => EngineVersion;

    public EngineCapabilities Capabilities =>
        EngineCapabilities.Observation
        | EngineCapabilities.Actions
        | EngineCapabilities.Location
        | EngineCapabilities.Keyboard;

    public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            IBrowser browser;
            if (_options.Connect is { } connect)
            {
                var endpoint = await connect.CdpEndpoint(cancellationToken).ConfigureAwait(false);
                browser = await playwright.Chromium.ConnectOverCDPAsync(endpoint).ConfigureAwait(false);
            }
            else
            {
                browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = _headless }).ConfigureAwait(false);
            }

            var context = await browser.NewContextAsync(ContextOptions()).ConfigureAwait(false);
            await context.AddInitScriptAsync(PageScript.RecordClosedShadowRoots).ConfigureAwait(false);
            await InstallSiteHeadersAsync(context, options.BaseUrl).ConfigureAwait(false);
            var page = await context.NewPageAsync().ConfigureAwait(false);
            page.SetDefaultTimeout((float)options.ActionTimeout.TotalMilliseconds);
            return new WebSession(playwright, browser, context, page, options.ActionTimeout, _options.TestIdAttribute);
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist", StringComparison.Ordinal) || ex.Message.Contains("browserType.launch", StringComparison.Ordinal))
        {
            throw new EngineException(
                "ENVIRONMENT_UNAVAILABLE",
                "Chromium is not installed for Playwright. From the build output run: playwright.ps1 install chromium",
                ex);
        }
    }

    /// <summary>True when <paramref name="url"/> is bound for the app's host, the only requests the configured headers ride.</summary>
    internal static bool IsAppRequest(string url, Uri app)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var target)
            && (target.Scheme == Uri.UriSchemeHttp || target.Scheme == Uri.UriSchemeHttps)
            && string.Equals(target.Host, app.Host, StringComparison.OrdinalIgnoreCase);
    }

    private static void Validate(WebEngineOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TestIdAttribute))
        {
            throw new EngineException("INVALID_CONFIG", "testIdAttribute must name an attribute.");
        }

        if (options.Viewport is { } viewport && (viewport.Width <= 0 || viewport.Height <= 0))
        {
            throw new EngineException("INVALID_CONFIG", "viewport width and height must be positive.");
        }

        if (options.BasicAuth is { } auth && auth.Username.Contains(':', StringComparison.Ordinal))
        {
            throw new EngineException("INVALID_CONFIG", "basicAuth username cannot contain ':'.");
        }

        if (options.UserAgent is not null && options.Headers is { } headers
            && headers.Keys.Any(name => string.Equals(name, "user-agent", StringComparison.OrdinalIgnoreCase)))
        {
            throw new EngineException("INVALID_CONFIG", "Set the user agent with userAgent or a user-agent header, not both.");
        }
    }

    private BrowserNewContextOptions ContextOptions()
    {
        var context = new BrowserNewContextOptions
        {
            ViewportSize = _options.Viewport is { } viewport
                ? new ViewportSize { Width = viewport.Width, Height = viewport.Height }
                : ViewportSize.NoViewport,
        };
        if (_options.UserAgent is not null)
        {
            context.UserAgent = _options.UserAgent;
        }

        if (_options.BasicAuth is { } auth)
        {
            context.HttpCredentials = new HttpCredentials { Username = auth.Username, Password = auth.Password };
        }

        return context;
    }

    private async Task InstallSiteHeadersAsync(IBrowserContext context, string? baseUrl)
    {
        if (_options.Headers is not { Count: > 0 } headers || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var app))
        {
            return;
        }

        var lowered = headers.ToDictionary(pair => pair.Key.ToLowerInvariant(), pair => pair.Value, StringComparer.Ordinal);
        await context.RouteAsync(url => IsAppRequest(url, app), async route =>
        {
            var merged = new Dictionary<string, string>(route.Request.Headers, StringComparer.Ordinal);
            foreach (var (name, value) in lowered)
            {
                merged[name] = value;
            }

            try
            {
                await route.FallbackAsync(new RouteFallbackOptions { Headers = merged }).ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                // The page closed under the request; nothing waits for it.
            }
        }).ConfigureAwait(false);
    }

    private sealed class WebSession : IEngineSession
    {
        private readonly IPlaywright _playwright;
        private readonly IBrowser _browser;
        private readonly IBrowserContext _context;
        private readonly IPage _page;
        private readonly TimeSpan _actionTimeout;
        private readonly string _testIdAttribute;
        private Dictionary<string, IFrame> _frames = new(StringComparer.Ordinal);
        private int _nextRef = 1;

        public WebSession(IPlaywright playwright, IBrowser browser, IBrowserContext context, IPage page, TimeSpan actionTimeout, string testIdAttribute)
        {
            _playwright = playwright;
            _browser = browser;
            _context = context;
            _page = page;
            _actionTimeout = actionTimeout;
            _testIdAttribute = testIdAttribute;
        }

        public string Route => Routes.PathOf(_page.Url);

        public async Task OpenAsync(string url, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Load,
                Timeout = (float)_actionTimeout.TotalMilliseconds,
            }).ConfigureAwait(false);
        }

        public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var walk = new FrameWalk();
            var roots = await CollectAsync(_page.MainFrame, walk, cancellationToken).ConfigureAwait(false);
            _frames = walk.Frames;
            return new Observation { Route = Route, Roots = roots.Select(ToNode).ToList(), Truncated = walk.Truncated };
        }

        public async Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(action);
            var frame = _frames.GetValueOrDefault(node.Ref) ?? _page.MainFrame;
            if (frame.IsDetached)
            {
                throw new EngineException("NOT_FOUND", $"Node {node.Ref} is not on the page.");
            }

            var handle = await frame.EvaluateHandleAsync(PageScript.Find, node.Ref).ConfigureAwait(false);
            var element = handle.AsElement();
            if (element is null)
            {
                await handle.DisposeAsync().ConfigureAwait(false);
                throw new EngineException("NOT_FOUND", $"Node {node.Ref} is not on the page.");
            }

            await using var disposeElement = element.ConfigureAwait(false);
            var timeout = (float)_actionTimeout.TotalMilliseconds;
            try
            {
                switch (action)
                {
                    case LocatorAction.Tap:
                        await element.ClickAsync(new ElementHandleClickOptions { Timeout = timeout }).ConfigureAwait(false);
                        break;
                    case LocatorAction.Fill fill:
                        await element.FillAsync(fill.Value, new ElementHandleFillOptions { Timeout = timeout }).ConfigureAwait(false);
                        break;
                    case LocatorAction.Press press:
                        await element.PressAsync(press.Key, new ElementHandlePressOptions { Timeout = timeout }).ConfigureAwait(false);
                        break;
                    case LocatorAction.Select select:
                        await element.SelectOptionAsync(select.Value, new ElementHandleSelectOptionOptions { Timeout = timeout }).ConfigureAwait(false);
                        break;
                    case LocatorAction.Check:
                        await element.CheckAsync(new ElementHandleCheckOptions { Timeout = timeout }).ConfigureAwait(false);
                        break;
                    case LocatorAction.Uncheck:
                        await element.UncheckAsync(new ElementHandleUncheckOptions { Timeout = timeout }).ConfigureAwait(false);
                        break;
                    case LocatorAction.Clear:
                        await element.FillAsync("", new ElementHandleFillOptions { Timeout = timeout }).ConfigureAwait(false);
                        break;
                    default:
                        throw new EngineException("UNSUPPORTED_CAPABILITY", "Web engine cannot perform " + action.GetType().Name + ".");
                }
            }
            catch (PlaywrightException ex) when (ex.Message.Contains("not attached", StringComparison.Ordinal))
            {
                throw new EngineException("NOT_FOUND", $"Node {node.Ref} left the page before the action.", ex);
            }
        }

        public Task PressAsync(string key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _page.Keyboard.PressAsync(key);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _context.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                await _browser.CloseAsync().ConfigureAwait(false);
                _playwright.Dispose();
            }
        }

        /// <summary>
        /// Reads one frame's document, then each child frame's into the iframe
        /// node that owns it, sharing one node budget across all of them. A
        /// frame that detaches or navigates mid-read is left empty.
        /// </summary>
        private async Task<List<WebNode>> CollectAsync(IFrame frame, FrameWalk walk, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (walk.Remaining <= 0)
            {
                walk.Truncated = true;
                return [];
            }

            string json;
            try
            {
                json = await frame.EvaluateAsync<string>(PageScript.Collect, new { seed = _nextRef, max = walk.Remaining, testIdAttribute = _testIdAttribute }).ConfigureAwait(false);
            }
            catch (PlaywrightException) when (frame != _page.MainFrame)
            {
                return [];
            }

            var dto = System.Text.Json.JsonSerializer.Deserialize<WebObservation>(json, JsonDefaults.Options);
            if (dto is null)
            {
                return [];
            }

            _nextRef = dto.Next;
            walk.Remaining -= dto.Count;
            walk.Truncated |= dto.Truncated;
            var roots = dto.Roots ?? [];
            var owners = new Dictionary<string, WebNode>(StringComparer.Ordinal);
            Index(roots, frame, walk, owners);
            foreach (var child in frame.ChildFrames)
            {
                var owner = await OwnerOfAsync(child).ConfigureAwait(false);
                if (owner is not null && owners.TryGetValue(owner, out var node))
                {
                    node.Children = await CollectAsync(child, walk, cancellationToken).ConfigureAwait(false);
                }
            }

            return roots;
        }

        private static async Task<string?> OwnerOfAsync(IFrame frame)
        {
            try
            {
                var element = await frame.FrameElementAsync().ConfigureAwait(false);
                await using var dispose = element.ConfigureAwait(false);
                return await element.EvaluateAsync<string?>(PageScript.RefOf).ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                return null;
            }
        }

        private static void Index(List<WebNode> nodes, IFrame frame, FrameWalk walk, Dictionary<string, WebNode> owners)
        {
            foreach (var node in nodes)
            {
                walk.Frames[node.Ref] = frame;
                if (node.Frame)
                {
                    owners[node.Ref] = node;
                }

                Index(node.Children ?? [], frame, walk, owners);
            }
        }

        private static SemanticNode ToNode(WebNode dto)
        {
            return new SemanticNode
            {
                Ref = dto.Ref,
                Role = dto.Role,
                Name = dto.Name,
                Text = dto.Text,
                Value = dto.Secure ? null : dto.Value,
                TestId = dto.TestId,
                Placeholder = dto.Placeholder,
                InputPurpose = dto.InputPurpose,
                Level = dto.Level,
                States = new NodeStates
                {
                    Checked = dto.Checked,
                    Disabled = dto.Disabled,
                    Expanded = dto.Expanded,
                    Focused = dto.Focused,
                    Hidden = dto.Hidden,
                    Pressed = dto.Pressed,
                    Secure = dto.Secure,
                    Selected = dto.Selected,
                },
                Children = dto.Children?.Select(ToNode).ToList() ?? [],
            };
        }
    }

    private sealed class FrameWalk
    {
        public int Remaining { get; set; } = ObservationLimits.Nodes;

        public bool Truncated { get; set; }

        public Dictionary<string, IFrame> Frames { get; } = new(StringComparer.Ordinal);
    }

    private sealed class WebObservation
    {
        public int Next { get; set; }

        public int Count { get; set; }

        public bool Truncated { get; set; }

        public List<WebNode>? Roots { get; set; }
    }

    private sealed class WebNode
    {
        public string Ref { get; set; } = "";

        public string? Role { get; set; }

        public string? Name { get; set; }

        public string? Text { get; set; }

        public string? Value { get; set; }

        public string? TestId { get; set; }

        public string? Placeholder { get; set; }

        public string? InputPurpose { get; set; }

        public int? Level { get; set; }

        public bool Disabled { get; set; }

        public bool Checked { get; set; }

        public bool Expanded { get; set; }

        public bool Selected { get; set; }

        public bool Pressed { get; set; }

        public bool Focused { get; set; }

        public bool Hidden { get; set; }

        public bool Secure { get; set; }

        public bool Frame { get; set; }

        public List<WebNode>? Children { get; set; }
    }
}
