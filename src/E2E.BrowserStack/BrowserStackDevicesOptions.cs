// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.BrowserStack;

/// <summary>The orientation a BrowserStack device starts in.</summary>
public enum BrowserStackOrientation
{
    Portrait,
    Landscape,
}

/// <summary>
/// What <see cref="BrowserStackDevices"/> takes (upstream's <c>BrowserStackOptions</c>): the device, its OS version, and the app
/// BrowserStack installs on it.
/// </summary>
public sealed record BrowserStackDevicesOptions
{
    /// <summary>Device name exactly as BrowserStack's App Automate catalog lists it: <c>Google Pixel 9</c>, <c>iPhone 16</c>.</summary>
    public required string Device { get; init; }

    /// <summary>OS version exactly as the catalog lists it for that device: <c>16.0</c> on Android, <c>18</c> on iOS.</summary>
    public required string OsVersion { get; init; }

    /// <summary>
    /// The build BrowserStack installs on every session: a <c>bs://</c> app id; an <c>https</c> URL, which BrowserStack fetches once
    /// per run; or a local path, resolved against the project root and uploaded when the session starts.
    /// </summary>
    public required string App { get; init; }

    /// <summary>Dashboard project the sessions are grouped under. Defaults to <c>e2e</c>.</summary>
    public string? Project { get; init; }

    /// <summary>Dashboard build the sessions are grouped under. Defaults to the run id.</summary>
    public string? Build { get; init; }

    /// <summary>
    /// Name of every session on the dashboard, with <c>-&lt;slot&gt;</c> appended when the target leases more than one device, so each
    /// slot's session has its own. Defaults to <c>e2e-&lt;run id&gt;-&lt;target&gt;-&lt;slot&gt;</c>. Slots count from 1.
    /// </summary>
    public string? SessionName { get; init; }

    /// <summary>Orientation the device starts in; absent, the device's default.</summary>
    public BrowserStackOrientation? Orientation { get; init; }

    /// <summary>Country the device's IP geolocates to, as a code BrowserStack takes: <c>US</c>, <c>FR</c>.</summary>
    public string? GeoLocation { get; init; }

    /// <summary>The device's time zone, as BrowserStack takes it: <c>New_York</c>.</summary>
    public string? Timezone { get; init; }

    /// <summary>The app's language, as BrowserStack takes it: <c>fr</c>.</summary>
    public string? Language { get; init; }

    /// <summary>The device's locale, as BrowserStack takes it: <c>fr_FR</c>.</summary>
    public string? Locale { get; init; }

    /// <summary>
    /// Appium version BrowserStack starts for the session, such as <c>3.2.0</c>. Absent, BrowserStack's default Appium 1.x, which lacks
    /// the <c>mobile:</c> commands deep links and hardware buttons use.
    /// </summary>
    public string? AppiumVersion { get; init; }

    /// <summary>A named network condition profile, such as <c>4g-lte-advanced-good</c>. Not with <see cref="CustomNetwork"/>.</summary>
    public string? NetworkProfile { get; init; }

    /// <summary>A custom network shape, as BrowserStack takes it. Not with <see cref="NetworkProfile"/>.</summary>
    public string? CustomNetwork { get; init; }

    /// <summary>
    /// iOS only: keep the uploaded app's signature instead of letting BrowserStack re-sign it, which strips entitlements such as push
    /// notifications.
    /// </summary>
    public bool? NoResignApp { get; init; }
}
