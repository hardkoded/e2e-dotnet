// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// The layout of the output directory (<c>output</c>, <c>--output</c>): every result a session, an exploration, or an <c>e2e mcp</c> session
/// writes lives at a fixed name under it, so nothing derives one location from another. Upstream's <c>OutputLayout</c> (<c>run/output.ts</c>).
/// The port has no <c>report.json</c> writer, so <c>report</c> and <c>aiTrace</c> have no member here: the AI trace keeps its own file name
/// (<c>ai-trace-&lt;process id&gt;.json</c>) in the output directory.
/// </summary>
internal sealed class OutputLayout
{
    private OutputLayout(string output)
    {
        Results = Path.Combine(output, "results");
        Sessions = Path.Combine(output, "sessions");
        Videos = sessionId => Path.Combine(output, "videos", sessionId);
    }

    /// <summary>One directory per test, <c>results/&lt;test&gt;/</c>, holding its attempts' artifacts (<c>attempt-&lt;n&gt;/</c>).</summary>
    public string Results { get; }

    /// <summary>The per-run encrypted session stores.</summary>
    public string Sessions { get; }

    /// <summary>Where <c>e2e mcp</c> saves one session's recordings.</summary>
    public Func<string, string> Videos { get; }

    /// <summary>The fixed paths under the absolute output directory <paramref name="output"/> (upstream's <c>outputLayout</c>).</summary>
    public static OutputLayout Of(string output) => new(output);
}
