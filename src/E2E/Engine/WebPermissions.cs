// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Internal;
using Microsoft.Playwright;

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
        "accelerometer",
        "ambient-light-sensor",
        "background-sync",
        "camera",
        "clipboard-read",
        "clipboard-write",
        "geolocation",
        "gyroscope",
        "local-fonts",
        "local-network-access",
        "magnetometer",
        "microphone",
        "midi",
        "midi-sysex",
        "notifications",
        "payment-handler",
        "screen-wake-lock",
        "storage-access",
    ];

    private static readonly string[] Firefox =
    [
        "geolocation",
        "local-network-access",
        "notifications",
        "persistent-storage",
        "push",
        "screen-wake-lock",
    ];

    private static readonly string[] Webkit =
    [
        "camera",
        "clipboard-read",
        "geolocation",
        "microphone",
        "notifications",
        "screen-wake-lock",
    ];

    /// <summary>
    /// Refuses a permission <paramref name="browser"/> cannot grant, and any state but <c>grant</c>: Playwright only grants, and Chromium
    /// denies every permission a grant leaves out, so neither <c>deny</c> nor <c>reset</c> can mean what it does on a device.
    /// </summary>
    /// <exception cref="ConfigurationException"><c>INVALID_CONFIG</c>: an unknown name, or a state that is not <c>grant</c>.</exception>
    public static void ValidatePermissions(
        string targetName,
        BrowserName browser,
        IReadOnlyDictionary<string, AppPermissionState> permissions
    )
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
                    $"{where} on {BrowserConnection.NameOf(browser)} has unknown key \"{name}\""
                        + (hint.Length == 0 ? "; expected one of " + string.Join(", ", names) : hint)
                );
            }
        }

        foreach (var (name, state) in permissions)
        {
            if (state != AppPermissionState.Grant)
            {
                throw new ConfigurationException(
                    "INVALID_CONFIG",
                    $"{where}.{name} is {state.ToString().ToLowerInvariant()}, but a web() target only grants; leave the permission out instead"
                );
            }
        }
    }

    /// <summary>
    /// The permission names a context on <paramref name="browser"/> is granted: each one the browser grants for <c>'all'</c>
    /// (<paramref name="allPermissions"/>), else the map's names. Upstream's <c>grantedPermissions</c>.
    /// </summary>
    public static IReadOnlyList<string> GrantedPermissions(
        BrowserName browser,
        IReadOnlyDictionary<string, AppPermissionState>? permissions,
        bool allPermissions
    ) =>
        allPermissions ? BrowserPermissions(browser)
        : permissions is null ? []
        : [.. permissions.Keys];

    /// <summary>
    /// Grants <paramref name="names"/> on a context. A browser older than the list (a CDP attach to an older Chrome) refuses the whole call over
    /// one permission type it does not know. For <c>'all'</c> (<paramref name="skipUnknown"/>) that name is left out and the rest granted; a
    /// named grant keeps the refusal, since the user can drop the name. Playwright keeps a refused name among the context's grants, so the
    /// context's grants are cleared before each retry. Upstream's <c>grantPermissions</c>.
    /// </summary>
    public static Task GrantPermissionsAsync(
        IBrowserContext context,
        IReadOnlyList<string> names,
        bool skipUnknown
    ) => GrantPermissionsAsync(context, names, skipUnknown, static (context, names) => context.GrantPermissionsAsync(names));

    /// <summary>
    /// <see cref="GrantPermissionsAsync(IBrowserContext, IReadOnlyList{string}, bool)"/> through <paramref name="grant"/>, the call that
    /// grants the names on the context (.NET-only seam: Playwright .NET contexts cannot be spied on through a prototype, as upstream's
    /// integration test does).
    /// </summary>
    internal static async Task GrantPermissionsAsync(
        IBrowserContext context,
        IReadOnlyList<string> names,
        bool skipUnknown,
        Func<IBrowserContext, IReadOnlyList<string>, Task> grant
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(grant);
        var left = names.ToList();
        while (left.Count > 0)
        {
            try
            {
                await grant(context, left).ConfigureAwait(false);
                return;
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                var unknown = skipUnknown ? UnknownPermission(error, left) : null;
                if (unknown is null)
                {
                    throw;
                }

                left = left.Where(name => name != unknown).ToList();
                await context.ClearPermissionsAsync().ConfigureAwait(false);
            }
        }
    }

    private static string[] BrowserPermissions(BrowserName browser) =>
        browser switch
        {
            BrowserName.Firefox => Firefox,
            BrowserName.Webkit => Webkit,
            _ => Chromium,
        };

    /// <summary>
    /// The name among <paramref name="names"/> a browser refused as an unknown permission type, which Playwright sends camel-cased
    /// (<c>localNetworkAccess</c>). Only names whose protocol type is their camel-cased web name match. Upstream's <c>unknownPermission</c>.
    /// </summary>
    private static string? UnknownPermission(Exception error, IReadOnlyList<string> names)
    {
        var match = Regex.Match(
            error.Message,
            "Unknown permission type: ([A-Za-z]+)",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1)
        );
        if (!match.Success)
        {
            return null;
        }

        var type = match.Groups[1].Value;
        return names.FirstOrDefault(name => name == type || CamelCased(name) == type);
    }

    private static string CamelCased(string name)
    {
        var parts = name.Split('-');
        return parts[0]
            + string.Concat(
                parts.Skip(1).Select(part => part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..])
            );
    }
}
