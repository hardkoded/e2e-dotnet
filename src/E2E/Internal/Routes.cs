// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace E2E.Internal;

/// <summary>A validated route pattern: one URL pattern (a glob string or a <see cref="Regex"/>), optionally narrowed to one HTTP method.</summary>
/// <param name="Method">Uppercased HTTP method, or <c>null</c> when every method matches.</param>
/// <param name="Url">The glob string or <see cref="Regex"/>.</param>
internal sealed record NormalizedRoutePattern(string? Method, object Url)
{
    /// <summary>The URL test, compiled once so a request does not rebuild the glob.</summary>
    public Regex UrlTest { get; } = Url as Regex ?? Routes.CompilePattern((string)Url);
}

internal static partial class Routes
{
    public static string PathOf(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "/";
        }

        // Checked by scheme because on Unix Uri reads "/x?y" as the absolute file:///x%3Fy.
        if (SchemePattern().IsMatch(url) && Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            return string.IsNullOrEmpty(absolute.AbsolutePath) ? "/" : absolute.AbsolutePath;
        }

        var cut = url.IndexOfAny(['?', '#']);
        var path = cut >= 0 ? url[..cut] : url;
        if (path.Length == 0)
        {
            return "/";
        }

        return path.StartsWith('/') ? path : "/" + path;
    }

    /// <summary>The path, query and fragment of a URL: where the screen is, without the origin.</summary>
    public static string LocationOf(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "/";
        }

        if (SchemePattern().IsMatch(url) && Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            return (string.IsNullOrEmpty(absolute.AbsolutePath) ? "/" : absolute.AbsolutePath)
                + absolute.Query
                + absolute.Fragment;
        }

        return url.StartsWith('/') ? url : "/" + url;
    }

    public static string Resolve(string? baseUrl, string? url)
    {
        var resolved = Absolute(baseUrl, url);

        // An allowlist, because a browser wraps and nests schemes (view-source:file:, blob:, filesystem:)
        // and a list of forbidden ones misses the wrapper. The exact about:blank loads nothing.
        // A target whose app is a file opens a file in that file's directory or below it, never a directory (upstream's resolveNavigationUrl).
        if (resolved.Scheme == "file" && Urls.IsFileAppUrl(baseUrl))
        {
            // The file may have changed since the config loaded (a directory now, or a link out of its directory): the same rule applies again.
            if (Urls.FileScopeProblem(resolved, new Uri(baseUrl!, UriKind.Absolute), "file navigation") is { } problem)
            {
                throw new TestException("POLICY_DENIED", problem);
            }

            return resolved.AbsoluteUri;
        }

        if (resolved.Scheme is not ("http" or "https") && resolved.AbsoluteUri != "about:blank")
        {
            throw new TestException("POLICY_DENIED", "forbidden URL scheme: " + resolved.Scheme + ":");
        }

        return resolved.AbsoluteUri;
    }

    /// <summary>Resolves a URL against the base URL without the navigation scheme rule, for comparing URLs.</summary>
    public static Uri Absolute(string? baseUrl, string? url)
    {
        // No URL opens the base URL itself, as an empty reference does in WHATWG resolution.
        url = url?.Trim() ?? string.Empty;
        Uri? resolved;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            // Checked by scheme because on Unix Uri reads "/x" as the absolute file:///x.
            if (!SchemePattern().IsMatch(url))
            {
                throw new TestException(
                    "APP_URL_REQUIRED",
                    "Navigation to \"" + url + "\" needs an app URL; the test declares no base URL."
                );
            }

            Uri.TryCreate(url, UriKind.Absolute, out resolved);
        }
        else
        {
            Uri.TryCreate(new Uri(baseUrl, UriKind.Absolute), url, out resolved);
        }

        if (resolved is null)
        {
            throw new TestException("POLICY_DENIED", "malformed URL: " + url);
        }

        return resolved;
    }

    /// <summary>
    /// Compiles a URL glob with upstream's route pattern grammar: <c>*</c> matches
    /// within one path segment, <c>**</c> crosses <c>/</c>, <c>?</c> matches one
    /// character, <c>\</c> escapes the next character; everything else is literal.
    /// The pattern matches the complete URL.
    /// </summary>
    public static Regex CompilePattern(string pattern)
    {
        var source = new StringBuilder("^");
        for (var i = 0; i < pattern.Length; i++)
        {
            var ch = pattern[i];
            if (ch == '\\')
            {
                i++;
                if (i < pattern.Length)
                {
                    source.Append(Regex.Escape(pattern[i].ToString()));
                }

                continue;
            }

            if (ch == '*')
            {
                if (i + 1 < pattern.Length && pattern[i + 1] == '*')
                {
                    i++;
                    source.Append(".*");
                }
                else
                {
                    source.Append("[^/]*");
                }

                continue;
            }

            source.Append(ch == '?' ? "." : Regex.Escape(ch.ToString()));
        }

        return new Regex(source.Append('$').ToString(), RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Validates a route pattern (upstream's <c>toRoutePattern</c>): a glob string or <see cref="Regex"/>, or a
    /// <see cref="RouteMatcher"/> where <c>Method</c> is one HTTP method and the URL keeps the glob/regex grammar.
    /// The method is normalized to uppercase. <paramref name="api"/> names the calling member in errors.
    /// </summary>
    public static NormalizedRoutePattern ToRoutePattern(string api, object pattern) =>
        Normalize(api, pattern, "a string, RegExp, or { method, url }");

    /// <summary>
    /// Validates a <c>WaitForResponseAsync</c> or <c>WaitForRequestAsync</c> pattern: a predicate the test wrote, returned as is for the
    /// wait to call with each candidate, or a route pattern as <see cref="ToRoutePattern"/> reads one (upstream's <c>toWaitPattern</c>).
    /// </summary>
    public static object ToWaitPattern(string api, object pattern) =>
        pattern is Delegate
            ? pattern
            : Normalize(api, pattern, "a string, RegExp, { method, url }, or a predicate function");

    /// <summary><see cref="ToRoutePattern"/> with the accepted shapes its errors name (upstream's <c>normalize</c>).</summary>
    private static NormalizedRoutePattern Normalize(string api, object pattern, string shapes)
    {
        if (pattern is string or Regex)
        {
            return new NormalizedRoutePattern(null, pattern);
        }

        if (pattern is not RouteMatcher matcher)
        {
            throw new TestException("INVALID_ARGUMENT", api + " pattern must be " + shapes);
        }

        if (matcher.Method is null && matcher.Url is null && matcher.UrlRegex is null)
        {
            throw new TestException("INVALID_ARGUMENT", api + " pattern must be " + shapes + "; got an object");
        }

        if (matcher.Method is null)
        {
            throw new TestException("INVALID_ARGUMENT", api + " pattern requires method");
        }

        if (!HttpMethodPattern().IsMatch(matcher.Method))
        {
            throw new TestException(
                "INVALID_ARGUMENT",
                api
                    + " pattern method must be an HTTP method such as \"POST\"; got "
                    + JsonSerializer.Serialize(matcher.Method, QuoteOptions)
            );
        }

        if (
            matcher.Method.Contains('*', StringComparison.Ordinal)
            || matcher.Method.Contains('|', StringComparison.Ordinal)
        )
        {
            throw new TestException(
                "INVALID_ARGUMENT",
                api + " pattern method is one HTTP method; use a plain URL pattern to match every method"
            );
        }

        if (matcher.Url is null && matcher.UrlRegex is null)
        {
            throw new TestException("INVALID_ARGUMENT", api + " pattern requires url");
        }

        if (matcher.Url is not null && matcher.UrlRegex is not null)
        {
            throw new TestException("INVALID_ARGUMENT", api + " pattern url must be a string or RegExp, not both");
        }

        return new NormalizedRoutePattern(
            matcher.Method.ToUpperInvariant(),
            matcher.UrlRegex is not null ? matcher.UrlRegex : matcher.Url!
        );
    }

    /// <summary>Matches a URL against the URL half of a validated route pattern, a glob string (compiled by <see cref="CompilePattern"/>) or a <see cref="Regex"/>.</summary>
    public static bool UrlMatches(NormalizedRoutePattern pattern, string url) => pattern.UrlTest.IsMatch(url);

    /// <summary>Matches a request method against the method half of a validated route pattern.</summary>
    public static bool MethodMatches(NormalizedRoutePattern pattern, string method) =>
        pattern.Method is null || string.Equals(pattern.Method, method, StringComparison.OrdinalIgnoreCase);

    /// <summary>Structural equality for route patterns, used by unroute: the same method, and the same glob or the same regex source and options.</summary>
    public static bool PatternsEqual(NormalizedRoutePattern a, NormalizedRoutePattern b) =>
        a.Method == b.Method
        && (a.Url, b.Url) switch
        {
            (string x, string y) => string.Equals(x, y, StringComparison.Ordinal),
            (Regex x, Regex y) => string.Equals(x.ToString(), y.ToString(), StringComparison.Ordinal)
                && x.Options == y.Options,
            _ => false,
        };

    /// <summary>
    /// A route or URL pattern as steps and errors name it, upstream's <c>patternLabel</c>: a glob string quoted (<c>JSON.stringify</c>), a regex as
    /// <c>/source/flags</c> (<c>String(RegExp)</c>), a method-narrowed pattern as <c>POST</c> plus its URL pattern. It runs before the call
    /// validates its arguments, so an invalid shape renders as a description and never throws.
    /// </summary>
    public static string PatternLabel(object pattern) =>
        pattern switch
        {
            string glob => JsonSerializer.Serialize(glob, QuoteOptions),
            // Never the function's source: a predicate may close over test data.
            Delegate => "predicate",
            Regex regex => RegexLabel(regex),
            RouteMatcher { Method: { } method } matcher => method.ToUpperInvariant()
                + " "
                + (
                    matcher.UrlRegex is not null ? RegexLabel(matcher.UrlRegex)
                    : matcher.Url is not null ? JsonSerializer.Serialize(matcher.Url, QuoteOptions)
                    : "undefined"
                ),
            _ => "an object",
        };

    private static string RegexLabel(Regex regex) =>
        "/"
        + regex
        + "/"
        + (regex.Options.HasFlag(RegexOptions.IgnoreCase) ? "i" : "")
        + (regex.Options.HasFlag(RegexOptions.Multiline) ? "m" : "")
        + (regex.Options.HasFlag(RegexOptions.Singleline) ? "s" : "");

    private static readonly JsonSerializerOptions QuoteOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>An HTTP method token: RFC 9110's <c>method = token</c>, extension methods included.</summary>
    [GeneratedRegex(@"^[!#$%&'*+\-.^_`|~0-9A-Za-z]+\z")]
    private static partial Regex HttpMethodPattern();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9+.-]*:")]
    private static partial Regex SchemePattern();
}
