// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;

namespace E2E.Mobile.Lease;

/// <summary>The session a lease's daemon names (upstream's <c>session.ts</c>).</summary>
internal static class LeaseSessions
{
    /// <summary>
    /// The session a lease's daemon allocates and renews the lease under (upstream's <c>leaseSession</c>). agent-device refuses a
    /// provider lease to a client that names no session, and the daemon in <paramref name="stateDirectory"/> holds one lease.
    /// </summary>
    public static string LeaseSession(string stateDirectory) =>
        $"e2e-lease-{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(stateDirectory)))[..8]}";
}
