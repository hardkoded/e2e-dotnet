// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Engine;

namespace E2E.TestMu;

/// <summary>How <see cref="TestMuBrowsers"/> groups and names its sessions on the TestMu AI dashboard (upstream's <c>dashboard.ts</c>).</summary>
internal static class Dashboard
{
    /// <summary>The dashboard project sessions are grouped under when none is set (upstream <c>DEFAULT_PROJECT</c>).</summary>
    internal const string DefaultProject = "e2e";

    /// <summary>
    /// A worker slot's session name when none is set: <c>e2e-&lt;run id&gt;-&lt;target&gt;-&lt;slot&gt;</c>, slots counting from 1
    /// (upstream <c>defaultSessionName</c>). A per-attempt lease, which upstream does not have, is <c>e2e-&lt;run id&gt;-&lt;target&gt;-attempt-&lt;attempt id&gt;</c>.
    /// </summary>
    internal static string DefaultSessionName(BrowserRequest request) =>
        $"e2e-{request.RunId}-{request.TargetName}-{(request.AttemptId is null ? (request.Slot + 1).ToString(CultureInfo.InvariantCulture) : "attempt-" + request.AttemptId)}";
}
