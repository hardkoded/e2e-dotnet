// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Engine;

/// <summary>What an app that sits behind headers, basic auth, or a user agent may be sent.</summary>
internal static partial class ProtectedApp
{
    // An HTTP header field name: one or more token characters (RFC 9110).
    [GeneratedRegex(@"^[!#$%&'*+.^_`|~0-9A-Za-z-]+\z")]
    internal static partial Regex HeaderNamePattern();

    // A control character no HTTP field value may carry; a horizontal tab is the one the grammar allows.
    [GeneratedRegex(@"[\u0000-\u0008\u000A-\u001F\u007F]")]
    internal static partial Regex FieldValueControlPattern();

    /// <summary>
    /// The origin of <paramref name="url"/> as <c>URL.origin</c> serializes it (scheme and host lowercased, a default port
    /// dropped), or null when it does not parse or is not an <c>http:</c> or <c>https:</c> URL.
    /// </summary>
    internal static string? OriginOf(Uri? url)
    {
        if (url is null || !url.IsAbsoluteUri || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        // An IPv6 host keeps its brackets, as the origin of a URL writes it; IdnHost drops them.
        var host = url.HostNameType == UriHostNameType.IPv6 ? url.Host : url.IdnHost;
        return url.Scheme + "://" + host + (url.IsDefaultPort ? "" : ":" + url.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The configured headers a request to <paramref name="url"/> carries: all of them when its origin is one of
    /// <paramref name="origins"/>, none otherwise.
    /// </summary>
    internal static IReadOnlyDictionary<string, string>? ConfiguredHeadersFor(string url, IReadOnlySet<string> origins, IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return null;
        }

        var origin = Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? OriginOf(parsed) : null;
        return origin is not null && origins.Contains(origin) ? headers : null;
    }

    /// <summary>
    /// The exact origins the credentials go to: the app's own, when there is one, and each of <paramref name="extras"/>,
    /// normalized as <c>URL.origin</c> serializes it. Config load has checked every extra is an origin.
    /// </summary>
    internal static IReadOnlySet<string> CredentialOrigins(string? appOrigin, IReadOnlyList<string>? extras)
    {
        var origins = new HashSet<string>(StringComparer.Ordinal);
        if (appOrigin is not null)
        {
            origins.Add(appOrigin);
        }

        foreach (var extra in extras ?? [])
        {
            origins.Add(OriginOf(new Uri(extra))!);
        }

        return origins;
    }

    /// <summary>Whether WebKit drops a credential scoped to <paramref name="origin"/>: a default port, which the origin leaves out, or an IPv6 host.</summary>
    internal static bool WebkitCannotScope(string origin)
    {
        var url = new Uri(origin);
        return url.IsDefaultPort || url.Host.StartsWith('[');
    }

    /// <summary>
    /// Why <paramref name="value"/> is not an origin a credential can be scoped to, or null when it is: an <c>http:</c> or
    /// <c>https:</c> URL with no user info, path, query, or fragment (<c>https://api.example.com</c>, a trailing <c>/</c>
    /// allowed). A <c>*</c> in the host and port <c>0</c> parse as URLs but match no request, so they are refused too, rather
    /// than loading a config that sends nothing.
    /// </summary>
    internal static string? OriginProblem(string? value)
    {
        const string Expected = "must be an origin such as \"https://preview.example.com\": an http or https URL with no path, query, or fragment";
        if (value is null)
        {
            return Expected + ", got null";
        }

        if (WildcardHost().IsMatch(value))
        {
            return "must be an exact origin, got \"" + value + "\": a wildcard matches no request, so list each origin";
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var url))
        {
            return Expected + ", got \"" + value + "\"";
        }

        if (url.Port == 0)
        {
            return "must name the port requests use, got \"" + value + "\": port 0 matches no request";
        }

        var bare = url.UserInfo.Length == 0 && url.AbsolutePath == "/" && url.Query.Length == 0 && url.Fragment.Length == 0
            && !value.EndsWith('?') && !value.EndsWith('#');
        return (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps) && bare ? null : Expected + ", got \"" + value + "\"";
    }

    // A `*` in the authority's host, after any user info.
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.-]*://([^/?#@]*@)?[^/?#]*\*")]
    private static partial Regex WildcardHost();
}
