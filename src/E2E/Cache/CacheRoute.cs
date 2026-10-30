// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Cache;

/// <summary>
/// Where a screen is, as a route rather than a URL. A location carries the id the app minted for this run's
/// record, a cache buster, a session token, a fragment. All of that changes between two visits to the same
/// screen, and none of it is the screen. So a location is reduced to its origin, the path's segments and the
/// query's parameters, with every segment or value that looks minted per record read as one placeholder.
/// Every other query value is part of the screen. The fragment is dropped, except that an app routing in the
/// fragment (<c>#/companies/1</c>) routes by that path and its query.
/// The origin is relative to the app (<see cref="AppLocation"/>): a location on the app's own origin is kept as its path, so a
/// recording made against one deployment of the app replays on another that shares its <c>app.identity</c>, while a screen on
/// any other origin keeps its origin and is another route.
/// </summary>
internal static partial class CacheRoute
{
    private const string Placeholder = ":id";

    private static readonly Regex[] MintedSegment =
    [
        Uuid(),
        HexRun(),
        DigitRun(),
        LongToken(),
        PrefixedId(),
        Whitespace(),
    ];

    // A query value is minted on the same rules, with the wider alphabet of a signed token or a timestamp, and a date.
    private static readonly Regex[] MintedValue =
    [
        .. MintedSegment,
        LongValueToken(),
        Timestamp(),
    ];

    /// <summary>
    /// A location as the cache compares it: a URL on the app's own origin as its path, query, and fragment, so a recording follows
    /// the app to another deployment of it; anything else (another origin, a device screen title) as it is.
    /// </summary>
    public static string AppLocation(string location, string? appOrigin)
    {
        if (appOrigin is null || !SchemePrefix().IsMatch(location) || !Uri.TryCreate(location, UriKind.Absolute, out var url))
        {
            return location;
        }

        return url.Scheme is "http" or "https" && string.Equals(url.GetLeftPart(UriPartial.Authority), appOrigin, StringComparison.Ordinal)
            ? url.PathAndQuery + url.Fragment
            : location;
    }

    /// <summary>
    /// A navigation as a recording keeps it: a URL on the app's origin without that origin, the rest as typed. Anything else
    /// stays as given, and so does a URL with credentials or a rest that would not open the same URL.
    /// </summary>
    public static string AppNavigation(string url, string? appOrigin)
    {
        // The prefix is the scheme and authority as a URL parser reads them, which skips leading whitespace and any run of slashes.
        var prefix = UrlOriginPrefix().Match(url);
        if (appOrigin is null || !prefix.Success)
        {
            return url;
        }

        // .NET's parser rejects the extra slashes that the web's skips, so it parses the URL with them removed.
        var rest = url[prefix.Length..];
        var colon = prefix.Value.IndexOf(':', StringComparison.Ordinal);
        var scheme = prefix.Value[..colon].TrimStart();
        var authority = prefix.Value[(colon + 1)..].TrimStart('/', '\\');
        if (!Uri.TryCreate($"{scheme}://{authority}{rest}", UriKind.Absolute, out var parsed)
            || parsed.Scheme is not ("http" or "https")
            || !string.Equals(parsed.GetLeftPart(UriPartial.Authority), appOrigin, StringComparison.Ordinal)
            || parsed.UserInfo.Length > 0)
        {
            return url;
        }

        var path = rest.StartsWith('/') ? rest : "/" + rest;
        return Uri.TryCreate(new Uri(appOrigin), path, out var opened) && string.Equals(opened.AbsoluteUri, parsed.AbsoluteUri, StringComparison.Ordinal) ? path : url;
    }

    /// <summary>Whether two locations name the same screen: one origin, and segments and query that read alike.</summary>
    public static bool Same(string? recorded, string? live) =>
        string.Equals(Identity(recorded), Identity(live), StringComparison.Ordinal);

    private static string? Identity(string? location)
    {
        if (location is null)
        {
            return null;
        }

        var rest = location;
        string? origin = null;
        if (SchemePrefix().IsMatch(location) && Uri.TryCreate(location, UriKind.Absolute, out var url))
        {
            if (url.Scheme is not ("http" or "https"))
            {
                return "\0" + location;
            }

            origin = url.GetLeftPart(UriPartial.Authority);
            rest = url.PathAndQuery + url.Fragment;
        }
        else if (!location.StartsWith('/'))
        {
            return "\0" + location;
        }

        var (pathname, search) = Parts(rest);
        var segments = pathname
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(raw => MintedSegment.Any(pattern => pattern.IsMatch(Decode(raw))) ? Placeholder : Decode(raw));
        var terms = search
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(Term)
            .OrderBy(term => term, StringComparer.Ordinal);

        // Control characters cannot appear in a decoded segment or term that came from a URL, so they keep the parts apart.
        return (origin ?? "") + "\u0001" + string.Join('\u0002', segments) + "\u0001" + string.Join('\u0002', terms);
    }

    // The path and query to route by: the fragment's own when it is a path (#/companies/1), else the document's.
    private static (string Pathname, string Search) Parts(string rest)
    {
        var hashAt = rest.IndexOf('#', StringComparison.Ordinal);
        var fragment = hashAt < 0 ? string.Empty : rest[(hashAt + 1)..];
        var (documentPath, documentSearch) = SplitOnce(hashAt < 0 ? rest : rest[..hashAt], '?');
        if (!fragment.StartsWith('/'))
        {
            return (documentPath, documentSearch);
        }

        var (fragmentPath, fragmentSearch) = SplitOnce(fragment, '?');
        return (fragmentPath, string.Join('&', new[] { documentSearch, fragmentSearch }.Where(part => part.Length > 0)));
    }

    private static (string Head, string Tail) SplitOnce(string text, char separator)
    {
        var at = text.IndexOf(separator, StringComparison.Ordinal);
        return at < 0 ? (text, string.Empty) : (text[..at], text[(at + 1)..]);
    }

    private static string Term(string pair)
    {
        var (key, value) = SplitOnce(pair, '=');
        return Minted(FormDecode(key)) + "=" + Minted(FormDecode(value));
    }

    private static string Minted(string text) =>
        text.Length > 0 && MintedValue.Any(pattern => pattern.IsMatch(text)) ? Placeholder : text;

    private static string Decode(string raw) => Uri.UnescapeDataString(raw);

    private static string FormDecode(string raw) => Decode(raw.Replace('+', ' '));

    // Scheme and authority as a URL parser skips them: leading whitespace and any run of slashes.
    [GeneratedRegex(@"^\s*[a-z][a-z\d+.-]*:[/\\]*[^/?#\\]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlOriginPrefix();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9+.-]*:")]
    private static partial Regex SchemePrefix();

    [GeneratedRegex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Uuid();

    [GeneratedRegex("^[0-9a-f]{8,}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HexRun();

    [GeneratedRegex(@"^[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex DigitRun();

    // A long token carrying letters and at least two digits; a route word such as companies-v2 has one.
    [GeneratedRegex(@"^(?=(?:[^0-9]*[0-9]){2})(?=.*[A-Za-z])[A-Za-z0-9_-]{12,}$", RegexOptions.CultureInvariant)]
    private static partial Regex LongToken();

    // Letters, a dash, then a digit tail of two or more (PROJ-016); a single digit as in page-2 is a route word.
    [GeneratedRegex(@"^[A-Za-z]+-[0-9]{2,}$", RegexOptions.CultureInvariant)]
    private static partial Regex PrefixedId();

    [GeneratedRegex(@"\s", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^(?=(?:[^0-9]*[0-9]){2})(?=.*[A-Za-z])[A-Za-z0-9_.~+/=:-]{12,}$", RegexOptions.CultureInvariant)]
    private static partial Regex LongValueToken();

    [GeneratedRegex(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}(?:[T ][0-9]{2}:[0-9]{2}(?::[0-9]{2}(?:\.[0-9]+)?)?(?:Z|[+-][0-9]{2}:?[0-9]{2})?)?$", RegexOptions.CultureInvariant)]
    private static partial Regex Timestamp();
}
