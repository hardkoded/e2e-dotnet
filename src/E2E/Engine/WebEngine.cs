// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>
/// Browser engine for the web target. It drives Chromium through Playwright,
/// reads a semantic tree (role, name, text, test id, state), and performs the
/// same node actions the document engine does. Install browsers once with
/// <c>playwright.ps1 install chromium</c> from this project's build output.
/// </summary>
public sealed class WebEngine : IEngine
{
    public const string EngineVersion = "1.0.0";

    private readonly bool _headless;

    public WebEngine(bool? headless = null)
    {
        if (headless is bool chosen)
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
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = _headless }).ConfigureAwait(false);
            var session = new WebSession(playwright, browser, options.ActionTimeout);
            await session.NewContextAsync().ConfigureAwait(false);
            return session;
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist", StringComparison.Ordinal) || ex.Message.Contains("browserType.launch", StringComparison.Ordinal))
        {
            throw new EngineException(
                "ENVIRONMENT_UNAVAILABLE",
                "Chromium is not installed for Playwright. From the build output run: playwright.ps1 install chromium",
                ex);
        }
    }

    private sealed class WebSession : IBrowserSession
    {
        private readonly IPlaywright _playwright;
        private readonly IBrowser _browser;
        private readonly TimeSpan _actionTimeout;
        private IBrowserContext? _context;
        private IPage? _page;
        private ViewportSize _viewport = new() { Width = 1280, Height = 720 };
        private int _nextRef = 1;

        public WebSession(IPlaywright playwright, IBrowser browser, TimeSpan actionTimeout)
        {
            _playwright = playwright;
            _browser = browser;
            _actionTimeout = actionTimeout;
        }

        private IPage Page => _page ?? throw new EngineException("NOT_FOUND", "The browser has no open page.");

        private float ActionMs => (float)_actionTimeout.TotalMilliseconds;

        public string Route => Routes.PathOf(Page.Url);

        public async Task OpenAsync(string url, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = (float)_actionTimeout.TotalMilliseconds,
            }).ConfigureAwait(false);
        }

        public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await Page.EvaluateAsync<string>(PageScript.Collect, _nextRef).ConfigureAwait(false);
            var dto = System.Text.Json.JsonSerializer.Deserialize<WebObservation>(json, JsonDefaults.Options);
            _nextRef = dto?.Next ?? _nextRef;
            var roots = (dto?.Roots ?? []).Select(ToNode).ToList();
            return new Observation { Route = Route, Roots = roots };
        }

        public async Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(action);
            var handle = await Page.EvaluateHandleAsync(PageScript.Find, node.Ref).ConfigureAwait(false);
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
            return Page.Keyboard.PressAsync(key);
        }

        public async Task BackAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Page.GoBackAsync(new PageGoBackOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = ActionMs }).ConfigureAwait(false);
        }

        public async Task ForwardAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Page.GoForwardAsync(new PageGoForwardOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = ActionMs }).ConfigureAwait(false);
        }

        public async Task ReloadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = ActionMs }).ConfigureAwait(false);
        }

        public async Task RestartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = RequireContext();
            _page = null;
            foreach (var page in context.Pages.ToList())
            {
                await page.CloseAsync().ConfigureAwait(false);
            }

            await NewPageAsync(context).ConfigureAwait(false);
        }

        public async Task ClearStateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = _context;
            _context = null;
            _page = null;
            if (context is not null)
            {
                await context.CloseAsync().ConfigureAwait(false);
            }

            await NewContextAsync().ConfigureAwait(false);
        }

        public Task<string> GetUrlAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Page.Url);
        }

        public Task<string> GetTitleAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Page.TitleAsync();
        }

        public async Task<System.Text.Json.JsonElement?> EvaluateAsync(string expression, object? arg, bool hasArg, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return hasArg
                    ? await Page.EvaluateAsync<System.Text.Json.JsonElement?>(expression, arg).ConfigureAwait(false)
                    : await Page.EvaluateAsync<System.Text.Json.JsonElement?>(expression).ConfigureAwait(false);
            }
            catch (PlaywrightException ex)
            {
                throw new TestException("EVALUATE_FAILED", ex.Message, ex);
            }
        }

        public async Task<IReadOnlyList<BrowserCookie>> GetCookiesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cookies = await RequireContext().CookiesAsync().ConfigureAwait(false);
            return cookies.Select(cookie => new BrowserCookie
            {
                Name = cookie.Name,
                Value = cookie.Value,
                Domain = cookie.Domain,
                Path = cookie.Path,
                Expires = cookie.Expires >= 0 ? (long)Math.Floor(cookie.Expires) : null,
                HttpOnly = cookie.HttpOnly,
                Secure = cookie.Secure,
                SameSite = cookie.SameSite.ToString(),
            }).ToList();
        }

        public Task SetCookiesAsync(IReadOnlyList<BrowserCookie> cookies, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(cookies);
            return RequireContext().AddCookiesAsync(cookies.Select(cookie => new Cookie
            {
                Name = cookie.Name,
                Value = cookie.Value,
                Url = cookie.Url,
                Domain = cookie.Url is null ? cookie.Domain : null,
                Path = cookie.Url is null ? cookie.Path ?? "/" : null,
                Expires = cookie.Expires,
                HttpOnly = cookie.HttpOnly,
                Secure = cookie.Secure,
                SameSite = cookie.SameSite switch
                {
                    null => null,
                    "Strict" => SameSiteAttribute.Strict,
                    "Lax" => SameSiteAttribute.Lax,
                    _ => SameSiteAttribute.None,
                },
            }));
        }

        public async Task SetViewportAsync(int width, int height, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _viewport = new ViewportSize { Width = width, Height = height };
            await Page.SetViewportSizeAsync(width, height).ConfigureAwait(false);
        }

        public Task KeyboardTypeAsync(string text, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Page.Keyboard.TypeAsync(text);
        }

        public Task MouseMoveAsync(float x, float y, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Page.Mouse.MoveAsync(x, y);
        }

        public Task MouseWheelAsync(float deltaX, float deltaY, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Page.Mouse.WheelAsync(deltaX, deltaY);
        }

        public Task MouseDownAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Page.Mouse.DownAsync();
        }

        public Task MouseUpAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Page.Mouse.UpAsync();
        }

        /// <summary>Opens a clean context and its first page at the current viewport.</summary>
        public async Task NewContextAsync()
        {
            var context = await _browser.NewContextAsync(new BrowserNewContextOptions { ViewportSize = _viewport }).ConfigureAwait(false);
            _context = context;
            await NewPageAsync(context).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await _browser.CloseAsync().ConfigureAwait(false);
            _playwright.Dispose();
        }

        private async Task NewPageAsync(IBrowserContext context)
        {
            var page = await context.NewPageAsync().ConfigureAwait(false);
            page.SetDefaultTimeout(ActionMs);
            await page.SetViewportSizeAsync(_viewport.Width, _viewport.Height).ConfigureAwait(false);
            _page = page;
        }

        private IBrowserContext RequireContext() =>
            _context ?? throw new EngineException("NOT_FOUND", "The browser has no context.");

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
                    Hidden = dto.Hidden,
                    Secure = dto.Secure,
                },
                Children = dto.Children?.Select(ToNode).ToList() ?? [],
            };
        }
    }

    private sealed class WebObservation
    {
        public int Next { get; set; }

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

        public bool Hidden { get; set; }

        public bool Secure { get; set; }

        public List<WebNode>? Children { get; set; }
    }
}
