// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// Who owns the process: the hosts that are open. Upstream's <c>run/public-ownership.ts</c>. Upstream's suite runner reserves the process
/// against them (<c>reserveSuiteRun</c>, which fails with <c>RUNNER_BUSY</c> while a host is open); the port has no suite runner, because a
/// test framework runs the tests, so only the host side is ported and a host reservation never fails.
/// </summary>
internal static class PublicOwnership
{
    private static readonly Lock Gate = new();
    private static readonly HashSet<object> Hosts = [];

    /// <summary>The number of hosts that are open.</summary>
    public static int OpenHosts
    {
        get
        {
            lock (Gate)
            {
                return Hosts.Count;
            }
        }
    }

    /// <summary>Holds a host's process ownership from before config loading through close. Upstream's <c>reserveHostedRun</c>; the release is safe to call twice.</summary>
    public static Action ReserveHostedRun()
    {
        var owner = new object();
        lock (Gate)
        {
            Hosts.Add(owner);
        }

        return () =>
        {
            lock (Gate)
            {
                Hosts.Remove(owner);
            }
        };
    }
}
