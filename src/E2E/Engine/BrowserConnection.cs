// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

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

/// <summary>The browser connection, as upstream <c>browser-connection.ts</c>: every attach goes through one bounded dial.</summary>
internal static class BrowserConnection
{
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
    public static Task<IBrowser> ConnectCdpAsync(IPlaywright playwright, string endpoint, TimeSpan timeout, CancellationToken cancellationToken)
    {
        // Upstream registers its Playwright selector engines first; the port reads the page with its own script and registers none.
        cancellationToken.ThrowIfCancellationRequested();
        return playwright.Chromium.ConnectOverCDPAsync(endpoint, new BrowserTypeConnectOverCDPOptions { Timeout = (float)timeout.TotalMilliseconds });
    }

    /// <summary>
    /// Attaches to a Playwright server, or a grid that hosts one, with Playwright's own protocol, under the same bound as
    /// <see cref="ConnectCdpAsync"/>. .NET-only. The browser has no context yet: the caller opens its own.
    /// </summary>
    public static Task<IBrowser> ConnectPlaywrightAsync(IPlaywright playwright, string endpoint, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return playwright.Chromium.ConnectAsync(endpoint, new BrowserTypeConnectOptions { Timeout = (float)timeout.TotalMilliseconds });
    }
}
