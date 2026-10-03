// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
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
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 720 },
            }).ConfigureAwait(false);
            var page = await context.NewPageAsync().ConfigureAwait(false);
            page.SetDefaultTimeout((float)options.ActionTimeout.TotalMilliseconds);
            return new WebSession(playwright, browser, page, options.ActionTimeout);
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist", StringComparison.Ordinal) || ex.Message.Contains("browserType.launch", StringComparison.Ordinal))
        {
            throw new EngineException(
                "ENVIRONMENT_UNAVAILABLE",
                "Chromium is not installed for Playwright. From the build output run: playwright.ps1 install chromium",
                ex);
        }
        catch (Exception ex) when (WebErrors.IsPlaywright(ex))
        {
            throw new EngineException(EngineErrorCodes.EngineFailure, "browser launch failed: " + WebErrors.Message(ex), ex);
        }
    }

    private sealed class WebSession : IEngineSession
    {
        private readonly IPlaywright _playwright;
        private readonly IBrowser _browser;
        private readonly IPage _page;
        private readonly TimeSpan _actionTimeout;
        private int _nextRef = 1;

        public WebSession(IPlaywright playwright, IBrowser browser, IPage page, TimeSpan actionTimeout)
        {
            _playwright = playwright;
            _browser = browser;
            _page = page;
            _actionTimeout = actionTimeout;
        }

        public string Route => Routes.PathOf(_page.Url);

        public async Task OpenAsync(string url, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = (float)_actionTimeout.TotalMilliseconds,
                }).ConfigureAwait(false);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.Translate(ex, "navigate to " + url);
            }
        }

        public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string json;
            try
            {
                json = await _page.EvaluateAsync<string>(PageScript.Collect, _nextRef).ConfigureAwait(false);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.NavigationStaleOr(ex, "observe");
            }

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
            IJSHandle handle;
            try
            {
                handle = await _page.EvaluateHandleAsync(PageScript.Find, node.Ref).ConfigureAwait(false);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.NavigationStaleOr(ex, "locate " + node.Ref);
            }

            var element = handle.AsElement();
            if (element is null)
            {
                await handle.DisposeAsync().ConfigureAwait(false);
                throw new EngineException(EngineErrorCodes.NodeStale, $"Node {node.Ref} is not on the page. Observe again.", retryable: true);
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
                        throw new EngineException(EngineErrorCodes.UnsupportedCapability, "Web engine cannot perform " + action.GetType().Name + ".");
                }
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.ClassifyAction(ex, action);
            }
        }

        public async Task PressAsync(string key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _page.Keyboard.PressAsync(key).ConfigureAwait(false);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.Translate(ex, "press " + key);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _browser.CloseAsync().ConfigureAwait(false);
            _playwright.Dispose();
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

/// <summary>
/// Maps Playwright failures to stable engine codes, following upstream <c>@e2e-dev/web</c>
/// (<c>classifyActionError</c>, <c>translatePwError</c>, and <c>navigationStaleOr</c>).
/// </summary>
internal static partial class WebErrors
{
    private const string Redacted = "[redacted]";

    /// <summary>Whether Playwright raised <paramref name="cause"/>. Playwright for .NET reports timeouts as <see cref="System.TimeoutException"/>.</summary>
    public static bool IsPlaywright(Exception cause)
    {
        return cause is PlaywrightException or System.TimeoutException;
    }

    /// <summary>A failure's message without terminal color sequences.</summary>
    public static string Message(Exception cause)
    {
        return AnsiPattern().Replace(cause.Message, "");
    }

    /// <summary>An unexpected failure: a timeout becomes <c>OPERATION_TIMEOUT</c>, anything else <c>ENGINE_FAILURE</c>.</summary>
    public static EngineException Translate(Exception cause, string label)
    {
        var text = label + ": " + Message(cause);
        return cause is System.TimeoutException
            ? new EngineException(EngineErrorCodes.OperationTimeout, text, cause)
            : new EngineException(EngineErrorCodes.EngineFailure, text, cause);
    }

    /// <summary>A read that lost its document to a navigation is a retryable <c>NODE_STALE</c>, so the caller observes the new document.</summary>
    public static EngineException NavigationStaleOr(Exception cause, string label)
    {
        if (NavigationRacePattern().IsMatch(Message(cause)))
        {
            return new EngineException(EngineErrorCodes.NodeStale, label + ": " + Message(cause), retryable: true, cause);
        }

        return Translate(cause, label);
    }

    /// <summary>Classifies a failed locator action. A timeout after the input was dispatched may have committed.</summary>
    public static EngineException ClassifyAction(Exception rawCause, LocatorAction action)
    {
        var sensitive = action is LocatorAction.Fill { Sensitive: true };
        var text = Message(rawCause);
        if (action is LocatorAction.Fill fill && sensitive && fill.Value.Length > 0)
        {
            text = text.Replace(fill.Value, Redacted, StringComparison.Ordinal);
        }

        var cause = sensitive ? null : rawCause;
        var kind = action.GetType().Name.ToLowerInvariant();
        if (StrictModePattern().IsMatch(text))
        {
            return new EngineException(EngineErrorCodes.EngineFailure, text, retryable: false, cause);
        }

        if (DetachedPattern().IsMatch(text) || NavigationRacePattern().IsMatch(text))
        {
            return new EngineException(EngineErrorCodes.NodeStale, text, retryable: true, cause);
        }

        if (TimeoutPattern().IsMatch(text) || rawCause is System.TimeoutException)
        {
            if (PostDispatchPattern().IsMatch(text))
            {
                return new EngineException(EngineErrorCodes.ActionMayHaveCommitted, kind + " timed out after its input was dispatched: " + text, retryable: false, cause);
            }

            return new EngineException(EngineErrorCodes.NotActionable, kind + " did not become actionable in time: " + Summary(text), retryable: false, cause);
        }

        if (NotEditablePattern().IsMatch(text))
        {
            return new EngineException(EngineErrorCodes.NotActionable, text, retryable: false, cause);
        }

        return new EngineException(EngineErrorCodes.EngineFailure, text, retryable: false, cause);
    }

    // The headline and the last call log line, which names what blocked the action.
    private static string Summary(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
        {
            return text;
        }

        var last = lines[^1].TrimStart('-', ' ');
        return lines.Length > 1 && !string.Equals(last, "Call log:", StringComparison.Ordinal)
            ? lines[0] + " Last: " + last
            : lines[0];
    }

    [GeneratedRegex(@"\u001b\[\d+(?:;\d+)*m")]
    private static partial Regex AnsiPattern();

    [GeneratedRegex("strict mode violation", RegexOptions.IgnoreCase)]
    private static partial Regex StrictModePattern();

    [GeneratedRegex("element (is |was )?(detached|not attached)", RegexOptions.IgnoreCase)]
    private static partial Regex DetachedPattern();

    [GeneratedRegex("Timeout .*exceeded", RegexOptions.IgnoreCase)]
    private static partial Regex TimeoutPattern();

    [GeneratedRegex(@"performing \w+ action|\w+ action done|waiting for scheduled navigations to finish", RegexOptions.IgnoreCase)]
    private static partial Regex PostDispatchPattern();

    [GeneratedRegex("not an? <?(input|checkbox|radio|select)|not editable|not checkable", RegexOptions.IgnoreCase)]
    private static partial Regex NotEditablePattern();

    [GeneratedRegex("execution context was destroyed|because of a navigation|navigating and changing the content|frame was detached|frame got detached|node is detached from document", RegexOptions.IgnoreCase)]
    private static partial Regex NavigationRacePattern();
}
