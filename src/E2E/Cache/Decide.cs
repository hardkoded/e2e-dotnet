// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cache;

/// <summary>
/// Upstream's <c>TraceReplayDecision</c> (<c>cache/decide.ts</c>): <c>replay</c>, or <c>miss</c> for a reason: <c>truncated</c> or
/// <c>wrong-context</c>.
/// </summary>
internal readonly record struct TraceReplayDecision(string Action, string? Reason = null)
{
    public static TraceReplayDecision Replay { get; } = new("replay");

    public static TraceReplayDecision Miss(string reason) => new("miss", reason);
}

/// <summary>
/// Upstream's <c>cache/decide.ts</c>: the per-step replay decision. Pure and fail-to-miss: every reason a validated entry cannot
/// replay is a miss, never an error and never a step failure.
/// </summary>
internal static class Decide
{
    /// <summary>
    /// Whether a recording establishes its own starting point by navigating first. Such a recording needs no start-path
    /// precondition. A resize before the navigate does not depend on the route, and a project-tool gap before it cannot
    /// change where the recording begins, so both are passed over.
    /// </summary>
    public static bool OpensWithNavigate(CacheEntry entry) =>
        string.Equals(entry.Actions.FirstOrDefault(action => action.Kind is not ("tool" or "resize"))?.Kind, "navigate", StringComparison.Ordinal);

    /// <summary>
    /// Decides whether one entry replays for the current step. A truncated recording never replays. A recording that does not open with a navigate carries a start
    /// precondition: the app must be on the screen where the recording began, as a route (<see cref="CacheRoute.Same"/>), or the
    /// recorded actions would run against a different screen than they were proven on. The entry holds its route expanded.
    /// </summary>
    public static TraceReplayDecision DecideTraceReplay(CacheEntry entry, string? currentPath)
    {
        if (entry.Truncated == true)
        {
            return TraceReplayDecision.Miss("truncated");
        }

        return OpensWithNavigate(entry) || CacheRoute.Same(entry.Route, currentPath)
            ? TraceReplayDecision.Replay
            : TraceReplayDecision.Miss("wrong-context");
    }
}
