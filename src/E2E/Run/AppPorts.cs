// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Sockets;

namespace E2E;

/// <summary>
/// Free ports for app URLs declared with port 0 (upstream's <c>run/app-ports.ts</c>). The run picks one once, after the config loads and before
/// anything spawns, and resolves the target's app on it.
/// </summary>
internal static class AppPorts
{
    /// <summary>
    /// The target's app resolved on a port for its URL when that asked for one: a free port on its loopback host. A target that names its port,
    /// or has no URL, resolves as it is. A loopback host this machine cannot bind is a bad app URL, reported before anything starts. This is
    /// upstream's <c>allocateTargetPorts</c>, which hands the one target back as it now reads.
    /// </summary>
    public static ResolvedApp AllocateTargetPorts(string targetName, AppConfig app, string projectRoot)
    {
        var pending = TargetApps.ResolveTargetApp(targetName, app, projectRoot);
        if (pending.PortRequest is not { } request)
        {
            return pending;
        }

        return TargetApps.ResolveTargetApp(targetName, app, projectRoot, Reserve(targetName, request.Host));
    }

    /// <summary>Binds an ephemeral port on <paramref name="host"/> and releases it, so the command can take it.</summary>
    private static int Reserve(string targetName, string host)
    {
        try
        {
            // A URL spells an IPv6 host in brackets; the socket API wants it bare.
            var address = IPAddress.Parse(host.StartsWith('[') && host.EndsWith(']') ? host[1..^1] : host);
            var listener = new TcpListener(address, 0);
            try
            {
                listener.Start();
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
        catch (Exception ex) when (ex is SocketException or FormatException or ArgumentException)
        {
            throw new ConfigurationException(
                "INVALID_APP_URL",
                "target \"" + targetName + "\" asks for a free port on " + host + ", which this machine cannot bind: " + ex.Message,
                ex);
        }
    }
}
