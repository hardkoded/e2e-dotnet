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
    /// Normalizes an app URL the run will use as given. Port 0 asks the run to start the app on a free port, and the port
    /// has no <c>app.command</c> to do that, so it is <c>INVALID_CONFIG</c> where upstream starts the command.
    /// </summary>
    public static NormalizedBaseUrl NormalizeAppUrl(string raw, string where)
    {
        var url = NormalizeBaseUrl(raw);
        if (RequestsFreePort(url))
        {
            throw new ConfigurationException("INVALID_CONFIG", where + " asks for a free port (port 0), but nothing starts on it: the .NET port has no app.command, so start the app on a fixed port");
        }

        return url;
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
}
