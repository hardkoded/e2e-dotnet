// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Internal;

/// <summary>An app base URL as <see cref="Urls.NormalizeBaseUrl"/> read it.</summary>
/// <param name="Href">The serialized URL, with a path of at least <c>/</c>.</param>
/// <param name="Origin">The scheme, host, and a non-default port.</param>
/// <param name="BasePath">The path every relative navigation resolves under.</param>
internal sealed record NormalizedBaseUrl(string Href, string Origin, string BasePath);

/// <summary>URL normalization and origin policy helpers.</summary>
internal static partial class Urls
{
    /// <summary>
    /// Parses and normalizes the app base URL. Rejects userinfo, query, and fragment. <see cref="Uri"/> handles
    /// IDNA ASCII hosts, dot segments, and default-port removal. A URL without a scheme gets <c>https://</c>, or
    /// <c>http://</c> for a loopback host, so <c>tester.army</c> and <c>localhost:3000</c> both work as-is.
    /// </summary>
    public static NormalizedBaseUrl NormalizeBaseUrl(string raw)
    {
        var text = raw.Trim();
        var explicitScheme = HasExplicitScheme(text);
        if (!Uri.TryCreate(WithScheme(text), UriKind.Absolute, out var url))
        {
            throw Invalid(explicitScheme && !IsHttp(text) ? "app URL must be http(s): " + raw : "invalid app URL: " + raw);
        }

        if (url.UserInfo.Length > 0)
        {
            throw Invalid("app URL must not contain userinfo");
        }

        if (url.Query.Length > 0)
        {
            throw Invalid("app URL must not contain a query");
        }

        if (url.Fragment.Length > 0)
        {
            throw Invalid("app URL must not contain a fragment");
        }

        if (url.Scheme is not ("http" or "https"))
        {
            throw Invalid("app URL must be http(s): " + raw);
        }

        if (url.Scheme == "http" && !IsLoopbackHost(url.Host))
        {
            throw Invalid("plain HTTP is allowed only for loopback hosts: " + raw);
        }

        if (url.Port == 0 && !IsLoopbackAddress(url.Host))
        {
            throw Invalid("port 0 asks the run for a free port and takes only the loopback address the command will bind, 127.0.0.1 or [::1]; a name like localhost may resolve to another one: " + raw);
        }

        return Describe(url);
    }

    /// <summary>
    /// Normalizes an app URL a session will use as given, one set in code. Port 0 asks the run to start the app on a free port with
    /// <c>app.command</c>, and a session holds the URL the app is already served on, so it is <c>INVALID_CONFIG</c>.
    /// </summary>
    public static NormalizedBaseUrl NormalizeAppUrl(string raw, string where)
    {
        var url = NormalizeBaseUrl(raw);
        if (RequestsFreePort(url))
        {
            throw new ConfigurationException("INVALID_CONFIG", where + " asks for a free port (port 0), which only app.command can start an app on: give the URL the app is served on");
        }

        return url;
    }

    /// <summary>The base URL as a test reads it (upstream's <c>baseUrlOf</c>): <paramref name="href"/> without its trailing slash, so <c>$"{BaseUrl}/path"</c> joins with one slash.</summary>
    public static string BaseUrlOf(string href) => href.EndsWith('/') ? href[..^1] : href;

    /// <summary>
    /// <paramref name="url"/> without userinfo, query, or fragment, any of which may carry a credential: safe to print (upstream's
    /// <c>printableUrl</c>).
    /// </summary>
    public static string PrintableUrl(string url)
    {
        var parsed = new Uri(url);
        return $"{parsed.Scheme}://{parsed.Host}{(parsed.IsDefaultPort ? "" : ":" + parsed.Port.ToString(System.Globalization.CultureInfo.InvariantCulture))}{parsed.AbsolutePath}";
    }

    /// <summary>True when the URL was declared with port 0: the run picks a free port on its address and substitutes it.</summary>
    public static bool RequestsFreePort(NormalizedBaseUrl url) => new Uri(url.Href).Port == 0;

    /// <summary>The port the base URL is served on: the explicit one, else the scheme's default.</summary>
    public static int PortOf(NormalizedBaseUrl url) => new Uri(url.Href).Port;

    /// <summary>The base URL re-serialized on another port; everything else is kept.</summary>
    public static NormalizedBaseUrl WithPort(NormalizedBaseUrl url, int port) =>
        Describe(new UriBuilder(url.Href) { Port = port }.Uri);

    /// <summary>True for loopback hosts where plain HTTP is allowed.</summary>
    public static bool IsLoopbackHost(string hostname) =>
        hostname == "localhost" || hostname.EndsWith(".localhost", StringComparison.Ordinal)
        || hostname is "::1" or "[::1]"
        || IsLoopbackV4(hostname);

    /// <summary>True when environment may default to <c>test</c> (loopback, <c>.localhost</c>, and <c>.test</c> hosts).</summary>
    public static bool IsImplicitTestHost(string hostname) =>
        IsLoopbackHost(hostname) || hostname.EndsWith(".test", StringComparison.Ordinal);

    /// <summary>
    /// The registrable-domain approximation (upstream's <c>siteOf</c>): the last two labels of a host, or the last three when the last
    /// is a two-letter country code and the one before it a short second-level label (<c>example.co.uk</c>, <c>shop.com.au</c>). An IP
    /// literal or a single-label host such as <c>localhost</c> is a site of its own. Where the approximation errs it errs narrow, except
    /// on shared hosting suffixes such as <c>vercel.app</c>, which it reads as one site.
    /// </summary>
    public static string SiteOf(string hostname)
    {
        var host = hostname.ToLowerInvariant();
        if (host.StartsWith('[') || Ipv4Literal().IsMatch(host))
        {
            return host;
        }

        var labels = host.Split('.');
        if (labels.Length <= 2)
        {
            return host;
        }

        var keep = labels[^1].Length == 2 && labels[^2].Length <= 3 ? 3 : 2;
        return string.Join('.', labels[^keep..]);
    }

    /// <summary>
    /// True for a literal loopback address, <c>127.x.x.x</c> or <c>[::1]</c>. A free port is free on one address family
    /// only, so a port-0 URL must name the address the command binds rather than a name that may resolve to either.
    /// </summary>
    private static bool IsLoopbackAddress(string hostname) => hostname == "[::1]" || IsLoopbackV4(hostname);

    private static bool IsLoopbackV4(string hostname) => LoopbackV4().IsMatch(hostname);

    private static NormalizedBaseUrl Describe(Uri url) =>
        new(url.AbsoluteUri, url.GetLeftPart(UriPartial.Authority), url.AbsolutePath);

    private static bool IsHttp(string raw) =>
        raw.StartsWith("http:", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("https:", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A <c>scheme:</c> prefix counts as explicit only when what follows the colon is not a port: <c>localhost:3000/app</c>
    /// is a host, and <c>file:/tmp/app</c> stays a file URL, rejected downstream.
    /// </summary>
    private static bool HasExplicitScheme(string raw) => ExplicitScheme().IsMatch(raw);

    /// <summary>Prepends a scheme to a schemeless URL, <c>http://</c> when the host it would have is loopback.</summary>
    private static string WithScheme(string raw)
    {
        if (HasExplicitScheme(raw))
        {
            // A special scheme reads any run of slashes as the two it needs, so http:/localhost:3000 is a host.
            return HttpSlashes().Replace(raw, "$1://");
        }

        if (!Uri.TryCreate("https://" + raw, UriKind.Absolute, out var probe))
        {
            return raw;
        }

        return (IsLoopbackHost(probe.Host) ? "http://" : "https://") + raw;
    }

    private static ConfigurationException Invalid(string message) => new("INVALID_APP_URL", message);

    [GeneratedRegex(@"^[a-z][a-z0-9+.-]*:(?!\d+(?:[/?#]|$))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitScheme();

    [GeneratedRegex(@"^(https?):[/\\]*(?=[^/\\])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HttpSlashes();

    [GeneratedRegex(@"^127(\.\d{1,3}){3}$", RegexOptions.CultureInvariant)]
    private static partial Regex LoopbackV4();

    [GeneratedRegex(@"^\d{1,3}(\.\d{1,3}){3}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Ipv4Literal();
}
