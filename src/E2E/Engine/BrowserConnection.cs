// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>A browser the engine launches (upstream <c>BrowserName</c>).</summary>
public enum BrowserName
{
    /// <summary>Chromium, the default.</summary>
    Chromium,

    /// <summary>Firefox.</summary>
    Firefox,

    /// <summary>WebKit.</summary>
    Webkit,
}

/// <summary>A CDP connection endpoint and its handshake headers, resolved together (upstream <c>CdpTarget</c>). A bare endpoint converts implicitly.</summary>
public sealed record CdpTarget
{
    /// <summary>The <c>ws://</c>, <c>wss://</c>, or <c>http://</c> DevTools URL.</summary>
    public required string Endpoint { get; init; }

    /// <summary>Headers sent with the CDP handshake only, never with the page's own requests.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>Wraps an endpoint that needs no handshake headers.</summary>
    public static implicit operator CdpTarget(string endpoint) => new() { Endpoint = endpoint };
}

/// <summary>What <see cref="BrowserConnection.LaunchLocalAsync"/> launches (upstream <c>BrowserAcquireOptions</c>, without its attach seam, which the engine owns).</summary>
internal sealed record BrowserAcquireOptions
{
    /// <summary>Which browser to launch.</summary>
    public BrowserName Name { get; init; } = BrowserName.Chromium;

    /// <summary>Whether the browser opens headed.</summary>
    public bool Headed { get; init; }

    /// <summary>Whether a Chromium launch keeps its OS sandbox on; other browsers ignore it.</summary>
    public bool ChromiumSandbox { get; init; }
}

/// <summary>The browser connection, as upstream <c>browser-connection.ts</c>: every attach goes through one bounded dial.</summary>
internal static class BrowserConnection
{
    /// <summary>What a failed connection with handshake headers says, in place of an error that may echo them.</summary>
    internal const string CdpConnectionFailed =
        "CDP connection failed; check the endpoint, handshake headers, and connection timeout";

    /// <summary>Every browser Playwright installs, in the order the CLI lists them (upstream <c>BROWSER_NAMES</c>). The engine launches each of them.</summary>
    public static readonly IReadOnlyList<string> BrowserNames = ["chromium", "firefox", "webkit"];

    /// <summary>Whether <paramref name="name"/> is a browser Playwright installs (upstream <c>isBrowserName</c>).</summary>
    public static bool IsBrowserName(string name)
    {
        return BrowserNames.Contains(name);
    }

    /// <summary>The name <c>playwright install</c> and upstream's <c>BROWSER_NAMES</c> use for <paramref name="name"/>.</summary>
    public static string NameOf(BrowserName name)
    {
        return name switch
        {
            BrowserName.Firefox => "firefox",
            BrowserName.Webkit => "webkit",
            _ => "chromium",
        };
    }

    /// <summary>
    /// Chromium's sandbox is on where it starts out of the box (upstream <c>defaultChromiumSandbox</c>). On Linux it needs
    /// unprivileged user namespaces, which root, most containers, and Ubuntu 23.10+ refuse, so there it is opt-in.
    /// </summary>
    public static bool DefaultChromiumSandbox(OSPlatform? platform = null) =>
        (platform ?? CurrentPlatform()) != OSPlatform.Linux;

    private static OSPlatform CurrentPlatform() =>
        OperatingSystem.IsLinux() ? OSPlatform.Linux
        : OperatingSystem.IsWindows() ? OSPlatform.Windows
        : OSPlatform.OSX;

    /// <summary>
    /// Launches the browser locally (upstream <c>launchLocal</c>). A sandbox that cannot start fails with Playwright's whole launch
    /// call log; the engine error names the option instead.
    /// </summary>
    public static async Task<IBrowser> LaunchLocalAsync(IPlaywright playwright, BrowserAcquireOptions options)
    {
        var launch = new BrowserTypeLaunchOptions { Headless = !options.Headed };
        if (options.Name == BrowserName.Chromium)
        {
            launch.ChromiumSandbox = options.ChromiumSandbox;
        }

        try
        {
            return await BrowserType(playwright, options.Name).LaunchAsync(launch).ConfigureAwait(false);
        }
        catch (PlaywrightException cause)
            when (options.Name == BrowserName.Chromium
                && options.ChromiumSandbox
                && cause.Message.Contains("Chromium sandboxing failed!", StringComparison.Ordinal)
            )
        {
            // No inner exception: the runner follows the chain into the surfaced message, which would put the call log back.
            throw new EngineException(
                EngineErrorCodes.EngineFailure,
                "Chromium failed to launch because its sandbox cannot start on this host. Set WebEngineOptions.ChromiumSandbox to false to launch without the sandbox.",
                retryable: false
            );
        }
    }

    /// <summary>Maps a browser name to its Playwright <see cref="IBrowserType"/> (upstream <c>browserType</c>).</summary>
    public static IBrowserType BrowserType(IPlaywright playwright, BrowserName name)
    {
        return name switch
        {
            BrowserName.Firefox => playwright.Firefox,
            BrowserName.Webkit => playwright.Webkit,
            _ => playwright.Chromium,
        };
    }

    /// <summary>
    /// Attaches to a Chromium over CDP, failing once <paramref name="timeout"/> passes
    /// without an answer instead of holding the caller.
    /// </summary>
    public static async Task<IBrowser> ConnectCdpAsync(
        IPlaywright playwright,
        CdpTarget target,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        // Upstream registers its Playwright selector engines first; the port reads the page with its own script and registers none.
        cancellationToken.ThrowIfCancellationRequested();
        var options = new BrowserTypeConnectOverCDPOptions { Timeout = (float)timeout.TotalMilliseconds };
        if (target.Headers is { } headers)
        {
            options.Headers = [.. headers];
        }

        try
        {
            return await playwright.Chromium.ConnectOverCDPAsync(target.Endpoint, options).ConfigureAwait(false);
        }
        catch (Exception cause) when (target.Headers is not null && cause is not OperationCanceledException)
        {
            // Connection errors can echo credentials in altered forms, so retain only their type.
            throw cause is TimeoutException
                ? new TimeoutException(CdpConnectionFailed)
                : new PlaywrightException(CdpConnectionFailed);
        }
    }

    /// <summary>
    /// Attaches to a Playwright server, or a grid that hosts one, with Playwright's own protocol, under the same bound as
    /// <see cref="ConnectCdpAsync"/>. .NET-only. The browser has no context yet: the caller opens its own.
    /// </summary>
    public static Task<IBrowser> ConnectPlaywrightAsync(
        IPlaywright playwright,
        string endpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return playwright.Chromium.ConnectAsync(
            endpoint,
            new BrowserTypeConnectOptions { Timeout = (float)timeout.TotalMilliseconds }
        );
    }
}
