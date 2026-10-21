// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.TestMu;

/// <summary>
/// What <see cref="TestMuBrowsers"/> asks the TestMu AI grid for. Every property is optional. Those with a default below are always sent, and
/// the others are sent only when set. Each one is a capability of the grid's <c>LT:Options</c>, except
/// <see cref="BrowserName"/> and <see cref="BrowserVersion"/>.
/// </summary>
public sealed record TestMuBrowsersOptions
{
    /// <summary>The grid's <c>browserName</c>, such as <c>Chrome</c>, <c>MicrosoftEdge</c>, or <c>pw-chromium</c>. Defaults to <c>Chrome</c>. The engine attaches over CDP, so it must be a Chromium browser.</summary>
    public string? BrowserName { get; init; }

    /// <summary>The grid's <c>browserVersion</c>. Defaults to <c>latest</c>.</summary>
    public string? BrowserVersion { get; init; }

    /// <summary>The operating system, such as <c>Windows 11</c>. Defaults to <c>Windows 10</c>.</summary>
    public string? Platform { get; init; }

    /// <summary>The build the sessions are grouped under in the dashboard. Defaults to <c>e2e-</c> and the run id.</summary>
    public string? Build { get; init; }

    /// <summary>Records a video of the session.</summary>
    public bool? Video { get; init; }

    /// <summary>Records the session's network log.</summary>
    public bool? Network { get; init; }

    /// <summary>Records the session's console log.</summary>
    public bool? Console { get; init; }

    /// <summary>The name of a running TestMu tunnel, to reach a site that is not public.</summary>
    public string? TunnelName { get; init; }

    /// <summary>The country code of the location the browser appears in, such as <c>US</c>.</summary>
    public string? GeoLocation { get; init; }

    /// <summary>
    /// Further <c>LT:Options</c> capabilities, merged last, so they replace any property above and the defaults
    /// (<c>project</c> is <c>e2e</c>, <c>idleTimeout</c> is 600 seconds). <c>TestMuBrowsers</c> refuses <c>LT:Options</c>, <c>browserName</c>,
    /// <c>user</c>, <c>accessKey</c>, <c>name</c>, a <c>browserOptions</c> that is not an array of strings, and a number JSON cannot encode.
    /// For <c>MicrosoftEdge</c>, <c>browserOptions</c> gets <c>--user-data-dir=remote-debug-profile-edge</c> unless it has a <c>--user-data-dir=</c>.
    /// </summary>
    public IReadOnlyDictionary<string, JsonNode?>? Capabilities { get; init; }
}
