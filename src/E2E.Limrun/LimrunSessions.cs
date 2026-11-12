// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;

namespace E2E.Limrun;

/// <summary>The session a Limrun lease's daemon is named under (upstream's <c>session.ts</c>).</summary>
internal static class LimrunSessions
{
    /// <summary>
    /// The session the driver allocates and renews the lease under: agent-device refuses a provider lease to a client that names no
    /// session, and the daemon in <paramref name="stateDirectory"/> holds one lease. The lease the engine receives never carries it:
    /// the engine names its own session, so the driver and the heartbeat derive this one from the state directory.
    /// </summary>
    public static string LeaseSession(string stateDirectory) =>
        $"e2e-lease-{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(stateDirectory)))[..8]}";
}
