// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Engine;

/// <summary>
/// A web target's <c>app.permissions</c> (upstream <c>permissions.ts</c> in <c>@e2e-dev/web</c>): the browser permissions every page of
/// an attempt holds. The session grants them on each context the attempt opens.
/// </summary>
internal static class WebPermissions
{
    // The permission names Playwright grants on each browser (upstream BROWSER_PERMISSIONS, playwright-core 1.64).
    private static readonly string[] Chromium =
    [
        "accelerometer", "ambient-light-sensor", "background-sync", "camera", "clipboard-read", "clipboard-write",
        "geolocation", "gyroscope", "local-fonts", "local-network-access", "magnetometer", "microphone", "midi",
        "midi-sysex", "notifications", "payment-handler", "screen-wake-lock", "storage-access",
    ];

    private static readonly string[] Firefox = ["geolocation", "local-network-access", "notifications", "persistent-storage", "push", "screen-wake-lock"];

    private static readonly string[] Webkit = ["camera", "clipboard-read", "geolocation", "microphone", "notifications", "screen-wake-lock"];

    /// <summary>
    /// Refuses a permission <paramref name="browser"/> cannot grant, and any state but <c>grant</c>: Playwright only grants, and Chromium
    /// denies every permission a grant leaves out, so neither <c>deny</c> nor <c>reset</c> can mean what it does on a device.
    /// </summary>
    /// <exception cref="ConfigurationException"><c>INVALID_CONFIG</c>: an unknown name, or a state that is not <c>grant</c>.</exception>
    public static void ValidatePermissions(string targetName, BrowserName browser, IReadOnlyDictionary<string, AppPermissionState> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        var where = $"target \"{targetName}\" app.permissions";
        var names = browser switch
        {
            BrowserName.Firefox => Firefox,
            BrowserName.Webkit => Webkit,
            _ => Chromium,
        };

        foreach (var name in permissions.Keys)
        {
            if (!names.Contains(name, StringComparer.Ordinal))
            {
                var hint = Suggestions.DidYouMean(name, names);
                throw new ConfigurationException(
                    "INVALID_CONFIG",
                    $"{where} on {BrowserConnection.NameOf(browser)} has unknown key \"{name}\"" + (hint.Length == 0 ? "; expected one of " + string.Join(", ", names) : hint));
            }
        }

        foreach (var (name, state) in permissions)
        {
            if (state != AppPermissionState.Grant)
            {
                throw new ConfigurationException(
                    "INVALID_CONFIG",
                    $"{where}.{name} is {state.ToString().ToLowerInvariant()}, but a web() target only grants; leave the permission out instead");
            }
        }
    }
}
