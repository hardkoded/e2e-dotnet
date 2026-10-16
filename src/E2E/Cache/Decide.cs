// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cache;

/// <summary>
/// Upstream's <c>TraceReplayDecision</c> (<c>cache/decide.ts</c>): <c>replay</c>, or <c>miss</c> for a reason. The port records no
/// truncated trace, so the only reason it gives is <c>wrong-context</c>.
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
    /// precondition. Upstream skips the project-tool gaps before the first action; the port reads the first action as it is.
    /// </summary>
    public static bool OpensWithNavigate(CacheEntry entry) =>
        string.Equals(entry.Actions[0].Kind, "navigate", StringComparison.Ordinal);

    /// <summary>
    /// Decides whether one entry replays for the current step. A recording that does not open with a navigate carries a start
    /// precondition: the app must be on the screen where the recording began, as a route (<see cref="CacheRoute.Same"/>), or the
    /// recorded actions would run against a different screen than they were proven on. The recorded route is read back through
    /// <paramref name="parameters"/>, where upstream's entry already holds it expanded.
    /// </summary>
    public static TraceReplayDecision DecideTraceReplay(CacheEntry entry, string? currentPath, IReadOnlyDictionary<string, object?>? parameters = null) =>
        OpensWithNavigate(entry) || CacheRoute.Same(entry.Route is null ? null : CacheKeys.Detemplate(entry.Route, parameters), currentPath)
            ? TraceReplayDecision.Replay
            : TraceReplayDecision.Miss("wrong-context");
}
