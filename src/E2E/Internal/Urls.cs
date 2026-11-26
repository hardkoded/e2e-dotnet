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
public static partial class Urls
{
    /// <summary>The schemes a device link may not take: the ones that read local content or inline a document, and the browser wrappers that carry one inside. Upstream's <c>REFUSED_LINK_SCHEMES</c>.</summary>
    private static readonly HashSet<string> RefusedLinkSchemes =
    [
        "file:",
        "data:",
        "javascript:",
        "view-source:",
        "blob:",
        "filesystem:",
    ];

    /// <summary>
    /// Whether a link's scheme (with its colon, as <c>Uri.Scheme + ":"</c>) is one no app link may take, in any case. A device link is any app's
    /// custom scheme, so links are checked against this list rather than the http(s) rule navigation follows. Upstream's <c>isRefusedLinkScheme</c>.
    /// </summary>
    public static bool IsRefusedLinkScheme(string protocol)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        return RefusedLinkSchemes.Contains(protocol.ToLowerInvariant());
    }

    /// <summary>
    /// Parses and normalizes the app base URL. Rejects userinfo, query, and fragment. <see cref="Uri"/> handles
    /// IDNA ASCII hosts, dot segments, and default-port removal. A URL without a scheme gets <c>https://</c>, or
    /// <c>http://</c> for a loopback host, so <c>tester.army</c> and <c>localhost:3000</c> both work as-is.
    /// </summary>
    internal static NormalizedBaseUrl NormalizeBaseUrl(string raw)
    {
        var text = raw.Trim();
        var explicitScheme = HasExplicitScheme(text);
        if (!Uri.TryCreate(WithScheme(text), UriKind.Absolute, out var url))
        {
            throw Invalid(
                explicitScheme && !IsHttp(text)
                    ? "app URL must be http(s), or a file: URL of a file on this machine: " + raw
                    : "invalid app URL: " + raw
            );
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

        if (url.Scheme == "file")
        {
            return FileBase(url, raw);
        }

        if (url.Scheme is not ("http" or "https"))
        {
            throw Invalid("app URL must be http(s), or a file: URL of a file on this machine: " + raw);
        }

        if (url.Scheme == "http" && !IsLoopbackHost(url.Host))
        {
            throw Invalid("plain HTTP is allowed only for loopback hosts: " + raw);
        }

        if (url.Port == 0 && !IsLoopbackAddress(url.Host))
        {
            throw Invalid(
                "port 0 asks the run for a free port and takes only the loopback address the command will bind, 127.0.0.1 or [::1]; a name like localhost may resolve to another one: "
                    + raw
            );
        }

        return Describe(url);
    }

    /// <summary>
    /// Normalizes an app URL a session will use as given, one set in code. Port 0 asks the run to start the app on a free port with
    /// <c>app.command</c>, and a session holds the URL the app is already served on, so it is <c>INVALID_CONFIG</c>.
    /// </summary>
    internal static NormalizedBaseUrl NormalizeAppUrl(string raw, string where)
    {
        var url = NormalizeBaseUrl(raw);
        if (RequestsFreePort(url))
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                where
                    + " asks for a free port (port 0), which only app.command can start an app on: give the URL the app is served on"
            );
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
    internal static bool RequestsFreePort(NormalizedBaseUrl url) => new Uri(url.Href).Port == 0;

    /// <summary>The port the base URL is served on: the explicit one, else the scheme's default; none for a file. Upstream's <c>portOf</c>.</summary>
    internal static int? PortOf(NormalizedBaseUrl url) => IsFileBase(url) ? null : new Uri(url.Href).Port;

    /// <summary>
    /// The origin a file app is given. A file's own origin is opaque, and every <c>data:</c> and <c>blob:</c> URL shares it; the bare scheme
    /// equals no URL's origin, so nothing is ever mistaken for being on the app's. Upstream's <c>FILE_ORIGIN</c>.
    /// </summary>
    private const string FileOrigin = "file://";

    /// <summary>
    /// Whether an app URL, as declared, names a file: the one reading of the scheme, shared with <see cref="NormalizeBaseUrl"/>, so
    /// <c>file:8443/report</c> stays the host <c>file</c> on port 8443. An unparseable URL names none; <see cref="NormalizeBaseUrl"/> says why.
    /// Upstream's <c>namesFile</c>.
    /// </summary>
    internal static bool NamesFile(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        return Uri.TryCreate(WithScheme(raw.Trim()), UriKind.Absolute, out var url) && url.Scheme == "file";
    }

    /// <summary>True when the app is a file the browser opens from disk: it has no origin, port, or host. Upstream's <c>isFileBase</c>.</summary>
    internal static bool IsFileBase(NormalizedBaseUrl url) => url.Origin == FileOrigin;

    /// <summary>
    /// A <c>file:</c> app URL: one file on this machine, which the browser opens itself. A host is refused, since a share is another
    /// machine's file, and so is a directory: a browser lists it with a link to its parent, and from there to every file the user can
    /// read. The file may not exist when the config loads. Upstream's <c>fileBase</c>.
    /// </summary>
    private static NormalizedBaseUrl FileBase(Uri url, string raw)
    {
        // WHATWG reads localhost in a file URL as this machine and drops it from the serialization.
        if (string.Equals(url.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            url = new Uri("file://" + url.AbsolutePath);
        }

        if (url.Host.Length > 0)
        {
            throw Invalid(
                "a file app URL names a file on this machine, with no host, such as file:///path/to/index.html: " + raw
            );
        }

        if (!FileScope(url))
        {
            throw Invalid(
                "a file app URL names one file this machine can reach, not a directory, such as file:///path/to/index.html: "
                    + raw
            );
        }

        return new NormalizedBaseUrl(url.AbsoluteUri, FileOrigin, url.AbsolutePath);
    }

    /// <summary>
    /// Whether <paramref name="url"/> is a file in the directory of the app's file, which is itself: upstream's <c>fileScope</c> reads
    /// <c>inside</c>. Paths are compared decoded and with symbolic links resolved. A path this machine cannot resolve (a NUL, a name too
    /// long, a directory it may not search) is not a file: the rule fails closed.
    /// </summary>
    private static bool FileScope(Uri url)
    {
        var path = LocalFile(url);
        if (path is null)
        {
            return false;
        }

        try
        {
            var target = RealPath(path);
            var relative = Path.GetRelativePath(RealPath(Path.GetDirectoryName(path)!), target);
            if (
                relative == ".."
                || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || Path.IsPathRooted(relative)
            )
            {
                return false;
            }

            var directory = relative == "." || url.AbsolutePath.EndsWith('/') || Directory.Exists(target);
            return !directory;
        }
        catch (Exception ex)
            when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// The path a <c>file:</c> URL names on this machine, or null when it names none: a form with an encoded path separator, which a
    /// browser decodes into a path the URL's own segments do not show. Upstream's <c>localFile</c>.
    /// </summary>
    private static string? LocalFile(Uri url)
    {
        var path = url.AbsolutePath;
        return
            path.Contains("%2f", StringComparison.OrdinalIgnoreCase)
            || path.Contains("%5c", StringComparison.OrdinalIgnoreCase)
            || path.Contains("%00", StringComparison.Ordinal)
            ? null
            : url.LocalPath;
    }

    /// <summary>
    /// <paramref name="file"/> with every symbolic link on the way resolved. A path that does not exist yet resolves as far as it does
    /// exist, so a new file under a linked directory is still placed where it would be created. Upstream's <c>realPath</c>.
    /// </summary>
    private static string RealPath(string file)
    {
        var existing = Path.GetFullPath(file);
        var rest = new Stack<string>();
        // GetAttributes tells a missing entry (not found) from one that exists but cannot be reached (access denied, a directory without
        // the search bit), which throws and so fails the rule closed. The Exists methods report both as false.
        while (true)
        {
            try
            {
                _ = File.GetAttributes(existing);
                break;
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                var parent = Path.GetDirectoryName(existing);
                if (parent is null)
                {
                    break;
                }

                rest.Push(Path.GetFileName(existing));
                existing = parent;
            }
        }

        var resolved = ResolveLinks(existing, 0);
        while (rest.Count > 0)
        {
            resolved = Path.Combine(resolved, rest.Pop());
        }

        return resolved;
    }

    private const int MaxLinkDepth = 40;

    /// <summary>Resolves each component of an existing path, so a link in a directory on the way is followed.</summary>
    private static string ResolveLinks(string existing, int depth)
    {
        if (depth > MaxLinkDepth)
        {
            throw new IOException("too many symbolic links: " + existing);
        }

        var root = Path.GetPathRoot(existing)!;
        var current = root;
        foreach (
            var part in existing[root.Length..]
                .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
        )
        {
            current = Path.Combine(current, part);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget is not null && info.ResolveLinkTarget(true) is { } target)
            {
                // The target's own ancestors may be links too.
                current = ResolveLinks(target.FullName, depth + 1);
            }
        }

        return current;
    }

    /// <summary>The base URL re-serialized on another port; everything else is kept.</summary>
    internal static NormalizedBaseUrl WithPort(NormalizedBaseUrl url, int port) =>
        Describe(new UriBuilder(url.Href) { Port = port }.Uri);

    /// <summary>True for loopback hosts where plain HTTP is allowed.</summary>
    public static bool IsLoopbackHost(string hostname)
    {
        var host = WithoutRootDot(hostname);
        return host == "localhost"
            || host.EndsWith(".localhost", StringComparison.Ordinal)
            || host is "::1" or "[::1]"
            || IsLoopbackV4(host);
    }

    /// <summary>True when environment may default to <c>test</c> (loopback, <c>.localhost</c>, and <c>.test</c> hosts).</summary>
    public static bool IsImplicitTestHost(string hostname) =>
        IsLoopbackHost(hostname) || WithoutRootDot(hostname).EndsWith(".test", StringComparison.Ordinal);

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
    /// True when a URL's host is on <paramref name="site"/> as <see cref="SiteOf"/> reads it (upstream's <c>sameSite</c>): the rule for which
    /// child frames an observation reads. An unparseable URL is off every site.
    /// </summary>
    public static bool SameSite(string url, string site)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(site);
        return Uri.TryCreate(url, UriKind.Absolute, out var parsed) && SiteOf(parsed.Host) == site;
    }

    /// <summary>
    /// True when <paramref name="current"/> matches <paramref name="expected"/> (upstream's <c>urlMatches</c>): a string resolves against
    /// <paramref name="baseHref"/> and compares exactly after serialization, folding case when <paramref name="ignoreCase"/> is true.
    /// </summary>
    public static bool UrlMatches(string current, string expected, string baseHref, bool? ignoreCase = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(baseHref);
        if (
            !Uri.TryCreate(baseHref, UriKind.Absolute, out var baseUri)
            || !Uri.TryCreate(baseUri, expected, out var expectedUrl)
        )
        {
            return false;
        }

        var actual = SerializeForComparison(current);
        return string.Equals(
            actual,
            expectedUrl.AbsoluteUri,
            ignoreCase == true ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
        );
    }

    /// <summary>
    /// True when <paramref name="current"/> matches <paramref name="expected"/>: a regular expression tests the whole serialized URL, with
    /// <paramref name="ignoreCase"/> as Playwright's (true adds the <c>i</c> flag, false removes it).
    /// </summary>
    public static bool UrlMatches(
        string current,
        System.Text.RegularExpressions.Regex expected,
        bool? ignoreCase = null
    )
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(expected);
        var options = ignoreCase switch
        {
            true => expected.Options | System.Text.RegularExpressions.RegexOptions.IgnoreCase,
            false => expected.Options & ~System.Text.RegularExpressions.RegexOptions.IgnoreCase,
            null => expected.Options,
        };
        return new System.Text.RegularExpressions.Regex(expected.ToString(), options).IsMatch(
            SerializeForComparison(current)
        );
    }

    private static string SerializeForComparison(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? parsed.AbsoluteUri : url;

    /// <summary>
    /// True for a literal loopback address, <c>127.x.x.x</c> or <c>[::1]</c>. A free port is free on one address family
    /// only, so a port-0 URL must name the address the command binds rather than a name that may resolve to either.
    /// </summary>
    private static bool IsLoopbackAddress(string hostname) => hostname == "[::1]" || IsLoopbackV4(hostname);

    // A final DNS root dot does not change a hostname's reserved-domain status.
    // Keep the URL itself unchanged so origin serialization remains WHATWG-based.
    private static string WithoutRootDot(string hostname) =>
        hostname.EndsWith(".", StringComparison.Ordinal) ? hostname.Substring(0, hostname.Length - 1) : hostname;

    private static bool IsLoopbackV4(string hostname) => LoopbackV4().IsMatch(hostname);

    private static NormalizedBaseUrl Describe(Uri url) =>
        new(url.AbsoluteUri, url.GetLeftPart(UriPartial.Authority), url.AbsolutePath);

    private static bool IsHttp(string raw) =>
        raw.StartsWith("http:", StringComparison.OrdinalIgnoreCase)
        || raw.StartsWith("https:", StringComparison.OrdinalIgnoreCase);

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
            return FileSlashes().Replace(HttpSlashes().Replace(raw, "$1://"), "file:///");
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

    // WHATWG reads file: with none, one, or three slashes as an empty host; only exactly two followed by a name is a host.
    [GeneratedRegex(@"^file:(?:[/\\]{3,}|[/\\]?)(?=[^/\\])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FileSlashes();

    [GeneratedRegex(@"^127(\.\d{1,3}){3}$", RegexOptions.CultureInvariant)]
    private static partial Regex LoopbackV4();

    [GeneratedRegex(@"^\d{1,3}(\.\d{1,3}){3}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Ipv4Literal();
}
