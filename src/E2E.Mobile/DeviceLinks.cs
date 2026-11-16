// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;

namespace E2E.Mobile;

/// <summary>
/// The links <c>device.OpenLinkAsync</c> takes: the scheme rule the runner applies to every navigation,
/// the label a link enters the report under, and the guard that keeps a link out of
/// <c>device.OpenAppAsync</c>.
/// <para>
/// The runner's own rule first demands an app URL, which a device engine never declares, and admits
/// http(s) alone, where a device link is any app's custom scheme. So the check here is a list of refused
/// schemes: the ones that read local content or inline a document, and the browser wrappers
/// (<c>view-source:file:...</c>, <c>blob:</c>, <c>filesystem:</c>) that carry one inside.
/// </para>
/// </summary>
public static partial class DeviceLinks
{
    /// <summary>The C0 controls and the space, which a browser trims off both ends of a URL.</summary>
    private static readonly char[] EdgeCharacters = [.. Enumerable.Range(0, 0x21).Select(code => (char)code)];

    /// <summary>What the report shows for a link that did not parse, or an app id that looks like a link.</summary>
    private const string LinkPlaceholder = "<link>";

    private static readonly Dictionary<string, int> DefaultPorts = new() { ["http"] = 80, ["https"] = 443, ["ftp"] = 21, ["ws"] = 80, ["wss"] = 443 };

    /// <summary>
    /// What agent-device opens as a URL instead of resolving as an app: a <c>scheme:rest</c> string with
    /// no whitespace. Every string the device would route as a link matches; a display name with a space
    /// (<c>Notes: Pro</c>) does not.
    /// </summary>
    [GeneratedRegex(@"^([A-Za-z][A-Za-z0-9+.-]*):\S+\z")]
    private static partial Regex LinkShape();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.-]*:")]
    private static partial Regex SchemePrefix();

    [GeneratedRegex(@"^([A-Za-z][A-Za-z0-9+.-]*):(.*)\z", RegexOptions.Singleline)]
    private static partial Regex SchemeSplit();

    /// <summary>Whether agent-device would open this string as a URL rather than resolve it as an app.</summary>
    public static bool IsLink(string value) => LinkShape().IsMatch(value.Trim());

    /// <summary>
    /// Parses a link a test asked to open. A string that is not an absolute URL is
    /// <c>INVALID_ARGUMENT</c>; a <c>file:</c>, <c>data:</c>, <c>javascript:</c>, <c>view-source:</c>,
    /// <c>blob:</c>, or <c>filesystem:</c> link is <c>POLICY_DENIED</c>, as it is for <c>app.open</c> on the
    /// web. The message never echoes the input: a malformed link can carry a magic-link token in its query,
    /// path, or userinfo, and nothing about a string that failed to parse says which part is safe to repeat.
    /// </summary>
    public static Uri LinkTarget(string input)
    {
        // As a browser reads a URL: edge whitespace and control characters go, and so do tabs and line breaks anywhere.
        var text = new string(input.Where(character => character is not ('\t' or '\n' or '\r')).ToArray()).Trim(EdgeCharacters);
        if (!SchemePrefix().IsMatch(text) || !Uri.TryCreate(text, UriKind.Absolute, out var url))
        {
            throw new TestException(
                "INVALID_ARGUMENT",
                "openLink needs an absolute URL such as myapp://orders/42 or https://example.com/verify");
        }

        DenyForbiddenScheme(url.Scheme + ":");
        return url;
    }

    /// <summary>
    /// Refuses a link handed to <c>openApp</c>, which opens an app by bundle id, package, or display name.
    /// agent-device opens any <c>scheme:rest</c> string as a URL, so the check runs before the device sees
    /// it: a link with a forbidden scheme is <c>POLICY_DENIED</c> as it is for <c>openLink</c>, any other
    /// link is <c>INVALID_ARGUMENT</c> pointing at <c>openLink</c>. The message never echoes the link,
    /// whose query may carry a magic-link token.
    /// </summary>
    public static void AssertAppId(string app)
    {
        var link = LinkShape().Match(app.Trim());
        if (!link.Success)
        {
            return;
        }

        DenyForbiddenScheme(link.Groups[1].Value.ToLowerInvariant() + ":");
        throw new TestException(
            "INVALID_ARGUMENT",
            "openApp opens an app by bundle id, package, or display name; open a deep link or web link with device.openLink");
    }

    /// <summary>
    /// The link as the report shows it: read as a URL parser reads it, then without its userinfo, query, or
    /// fragment, any of which may carry a credential (a magic link's one-time token, a <c>user:password@</c>
    /// login). A step label is kept for as long as the report is. A string that does not parse is never
    /// echoed, as <see cref="LinkTarget"/> never echoes it, and the label is a placeholder.
    /// </summary>
    public static string LinkLabel(string input)
    {
        // As a URL parser reads it: tabs and line breaks go anywhere, and edge whitespace and control characters go.
        var text = new string(input.Where(character => character is not ('\t' or '\n' or '\r')).ToArray()).Trim(EdgeCharacters);
        var scheme = SchemeSplit().Match(text);
        if (!scheme.Success)
        {
            return LinkPlaceholder;
        }

        var name = scheme.Groups[1].Value.ToLowerInvariant();
        var rest = scheme.Groups[2].Value;
        if (!DefaultPorts.TryGetValue(name, out var defaultPort) && name != "file")
        {
            return NonSpecialLabel(name, rest);
        }

        // A special scheme reads a backslash as a slash and takes any number of slashes before the authority.
        rest = rest.Replace('\\', '/');
        var start = 0;
        while (start < rest.Length && rest[start] == '/')
        {
            start++;
        }

        rest = rest[start..];
        var end = rest.AsSpan().IndexOfAny('/', '?', '#');
        var authority = end < 0 ? rest : rest[..end];
        var path = end < 0 ? string.Empty : rest[end..];
        var host = Authority(authority, out var port, lowercase: true);
        if (host is null || (host.Length == 0 && name != "file"))
        {
            return LinkPlaceholder;
        }

        var portText = port is null || port == defaultPort ? string.Empty : ":" + port.Value.ToString(CultureInfo.InvariantCulture);
        return name + "://" + host + portText + PathLabel(path, special: true);
    }

    /// <summary>
    /// The label of an <c>openApp</c> step: the app id as it is. <see cref="AssertAppId"/> refuses a link,
    /// but the step is labeled before it runs, so a string shaped like a link gets the placeholder instead
    /// of whatever userinfo it carries.
    /// </summary>
    public static string AppLabel(string app) => IsLink(app) ? LinkPlaceholder : app;

    private static string NonSpecialLabel(string scheme, string rest)
    {
        if (!rest.StartsWith("//", StringComparison.Ordinal))
        {
            // No authority: the path (or an opaque path) is all there is, and an @ in it is no userinfo.
            return scheme + ":" + PathLabel(rest, special: false);
        }

        rest = rest[2..];
        var end = rest.AsSpan().IndexOfAny('/', '?', '#');
        var authority = end < 0 ? rest : rest[..end];
        var path = end < 0 ? string.Empty : rest[end..];
        var host = Authority(authority, out var port, lowercase: false);
        if (host is null)
        {
            return LinkPlaceholder;
        }

        return scheme + "://" + host + (port is null ? string.Empty : ":" + port.Value.ToString(CultureInfo.InvariantCulture)) + PathLabel(path, special: false);
    }

    /// <summary>The host and port of an authority, without its userinfo; <c>null</c> when it has a host or port no URL parser accepts.</summary>
    private static string? Authority(string authority, out int? port, bool lowercase)
    {
        port = null;
        var host = authority[(authority.LastIndexOf('@') + 1)..];
        var colon = host.StartsWith('[') ? host.IndexOf("]:", StringComparison.Ordinal) + 1 : host.LastIndexOf(':');
        if (colon > 0)
        {
            var digits = host[(colon + 1)..];
            host = host[..colon];
            if (digits.Length > 0)
            {
                if (!digits.All(char.IsAsciiDigit) || !int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number > 65535)
                {
                    return null;
                }

                port = number;
            }
        }

        if (host.Any(character => character <= ' ' || character is '#' or '/' or '<' or '>' or '?' or '@' or '\\' or '^' or '|' or '%' or '\u007f'))
        {
            return null;
        }

        return lowercase ? host.ToLowerInvariant() : host;
    }

    private static string PathLabel(string rest, bool special)
    {
        var cut = rest.AsSpan().IndexOfAny('?', '#');
        var path = (cut < 0 ? rest : rest[..cut]).Replace(" ", "%20", StringComparison.Ordinal);
        return special && path.Length == 0 ? "/" : path;
    }

    private static void DenyForbiddenScheme(string protocol)
    {
        if (E2E.Internal.Urls.IsRefusedLinkScheme(protocol))
        {
            throw new ConfigurationException("POLICY_DENIED", $"forbidden URL scheme: {protocol}");
        }
    }
}
