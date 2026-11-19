// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Sockets;

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>validateDownloadUrl</c> (<c>@ai-sdk/provider-utils</c>).</summary>
internal static class ValidateDownloadUrl
{
    /// <summary>
    /// Validates that a URL is safe to download from, blocking private and internal addresses to prevent SSRF. It checks the text and literal
    /// IP addresses only: DNS validation must come from the fetch transport or network egress controls.
    /// </summary>
    /// <exception cref="DownloadException">The URL is unsafe.</exception>
    internal static void Validate(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            throw new DownloadException(url, message: "Invalid URL: " + url);
        }

        // data: URLs are inline content, so they do not trigger a network fetch or SSRF risk.
        if (parsed.Scheme == "data")
        {
            return;
        }

        if (parsed.Scheme is not ("http" or "https"))
        {
            throw new DownloadException(
                url,
                message: "URL scheme must be http, https, or data, got " + parsed.Scheme + ":"
            );
        }

        // Strip a trailing dot so a fully-qualified name like `localhost.` cannot bypass the hostname blocklist.
        var hostname = parsed.IdnHost.ToLowerInvariant().TrimEnd('.');
        if (hostname.Length == 0)
        {
            throw new DownloadException(url, message: "URL must have a hostname");
        }

        if (
            hostname == "localhost"
            || hostname.EndsWith(".local", StringComparison.Ordinal)
            || hostname.EndsWith(".localhost", StringComparison.Ordinal)
            || hostname.EndsWith(".internal", StringComparison.Ordinal)
        )
        {
            throw new DownloadException(url, message: "URL with hostname " + hostname + " is not allowed");
        }

        if (parsed.HostNameType == UriHostNameType.IPv6)
        {
            var ipv6 = parsed.Host.Trim('[', ']');
            if (IsPrivateIPv6(ipv6))
            {
                throw new DownloadException(url, message: "URL with IPv6 address [" + ipv6 + "] is not allowed");
            }

            return;
        }

        if (IsIPv4(hostname) && IsPrivateIPv4(IPAddress.Parse(hostname).GetAddressBytes()))
        {
            throw new DownloadException(url, message: "URL with IP address " + hostname + " is not allowed");
        }
    }

    private static bool IsIPv4(string hostname)
    {
        var parts = hostname.Split('.');
        return parts.Length == 4
            && parts.All(part =>
                int.TryParse(part, out var number) && number is >= 0 and <= 255 && number.ToString() == part
            );
    }

    private static bool IsPrivateIPv4(byte[] ip)
    {
        int a = ip[0],
            b = ip[1],
            c = ip[2];
        return a == 0 // 0.0.0.0/8
            || a == 10 // 10.0.0.0/8
            || (a == 100 && b is >= 64 and <= 127) // 100.64.0.0/10 (CGNAT)
            || a == 127 // 127.0.0.0/8
            || (a == 169 && b == 254) // 169.254.0.0/16
            || (a == 172 && b is >= 16 and <= 31) // 172.16.0.0/12
            || (a == 192 && b == 0 && c == 0) // 192.0.0.0/24
            || (a == 192 && b == 0 && c == 2) // 192.0.2.0/24 (TEST-NET-1)
            || (a == 192 && b == 168) // 192.168.0.0/16
            || (a == 198 && b is 18 or 19) // 198.18.0.0/15
            || (a == 198 && b == 51 && c == 100) // 198.51.100.0/24 (TEST-NET-2)
            || (a == 203 && b == 0 && c == 113) // 203.0.113.0/24 (TEST-NET-3)
            || a >= 224; // multicast and reserved, including the broadcast
    }

    private static bool IsPrivateIPv6(string ip)
    {
        // Fail closed: an address that cannot be parsed is unsafe.
        if (!IPAddress.TryParse(ip, out var address) || address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        var groups = new int[8];
        for (var i = 0; i < 8; i++)
        {
            groups[i] = (bytes[2 * i] << 8) | bytes[(2 * i) + 1];
        }

        bool TopZero(int count) => groups.Take(count).All(group => group == 0);

        // ::1 (loopback) and :: (unspecified)
        if (TopZero(7) && groups[7] is 0 or 1)
        {
            return true;
        }

        if (
            (groups[0] & 0xfe00) == 0xfc00 // fc00::/7 (unique local)
            || (groups[0] & 0xffc0) == 0xfe80 // fe80::/10 (link-local)
            || (groups[0] & 0xffc0) == 0xfec0 // fec0::/10 (site-local)
            || (groups[0] & 0xff00) == 0xff00 // ff00::/8 (multicast)
            || (groups[0] == 0x2001 && groups[1] == 0x0db8) // 2001:db8::/32 (documentation)
            || (groups[0] == 0x3fff && (groups[1] & 0xf000) == 0) // 3fff::/20 (documentation)
        )
        {
            return true;
        }

        // Addresses that embed an IPv4 address in their last 32 bits reuse the IPv4 checks.
        var embedsIPv4 =
            TopZero(6) // ::/96, IPv4-compatible
            || (TopZero(5) && groups[5] == 0xffff) // ::ffff:0:0/96, IPv4-mapped
            || (TopZero(4) && groups[4] == 0xffff && groups[5] == 0) // IPv4-translated
            || (
                groups[0] == 0x0064
                && groups[1] == 0xff9b
                && groups[2] == 0
                && groups[3] == 0
                && groups[4] == 0
                && groups[5] == 0
            ) // 64:ff9b::/96 (NAT64)
        ;
        // 64:ff9b:1::/48 (NAT64 local-use) places the IPv4 address by the translator's prefix length, so the whole prefix fails closed.
        if (groups[0] == 0x0064 && groups[1] == 0xff9b && groups[2] == 0x0001)
        {
            return true;
        }

        return embedsIPv4 && IsPrivateIPv4(bytes[12..16]);
    }
}
