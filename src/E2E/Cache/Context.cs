// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E;

/// <summary>
/// Upstream's <c>strict</c> object of <c>AgentCacheContext</c> (<c>cache/context.ts</c>): a recording that no longer replays fails its step
/// (<c>REPLAY_STALE</c>) with <see cref="Advice"/> on how to re-record it. Upstream's <c>recordings</c> (the listing that finds a rekeyed
/// recording) has no counterpart: the step session lists the cache directory itself.
/// </summary>
internal sealed record AgentCacheStrict(string Advice);

/// <summary>
/// Upstream's <c>AgentCacheContext</c> (<c>cache/context.ts</c>), as far as the port has it: the resolved store, and whether the attempt
/// may replay. Upstream's <c>claimKey</c> and <c>staged</c> stay on <c>AttemptScope</c> and the step session.
/// </summary>
internal sealed record AgentCacheContext(CacheMode Mode, IStepCache Store, bool ReplayEligible, AgentCacheStrict? Strict);

/// <summary>Upstream's <c>cache/context.ts</c>: the per-attempt cache context. .NET-only name: <c>Context</c> would collide with <c>E2ESession.Context</c>.</summary>
internal static class CacheContexts
{
    /// <summary>
    /// Upstream's <c>createAgentCacheContext</c>: one attempt's cache context, or null when the cache is off. A configured custom store
    /// replaces the file store wholesale. Replay is eligible only on a first attempt (<paramref name="attemptIndex"/> 0) unless
    /// <c>cache.strict</c> is on, where every attempt replays, nothing is written or evicted, and the mode is <see cref="CacheMode.ReadOnly"/>.
    /// The port has no <c>projectId</c>, <c>testId</c>, or <c>target</c> parameter: <c>AttemptScope</c> claims the keys.
    /// </summary>
    public static AgentCacheContext? CreateAgentCacheContext(CacheConfig cache, string projectRoot, int attemptIndex)
    {
        ArgumentNullException.ThrowIfNull(cache);
        if (cache.Mode == CacheMode.Off)
        {
            return null;
        }

        var strict = cache.Strict;
        var mode = strict ? CacheMode.ReadOnly : cache.Mode;
        var store = cache.Store ?? new FileStepCache(cache.Directory, FileStepCache.MaxCacheWireBytes, mode == CacheMode.ReadWrite);
        return new AgentCacheContext(
            mode,
            store,
            strict || attemptIndex == 0,
            strict ? new AgentCacheStrict(StaleAdvice(cache, cache.StrictSource ?? new CacheStrictSource(true, false), projectRoot)) : null);
    }

    /// <summary>
    /// Upstream's <c>staleAdvice</c>: how to re-record a recording <c>cache.strict</c> failed on, a read-write run with every knob that
    /// turned strict on turned off, and where the new entry lands, as the project names it.
    /// </summary>
    private static string StaleAdvice(CacheConfig cache, CacheStrictSource source, string projectRoot)
    {
        var off = string.Join(" and ", new[] { source.Flag ? "without --strict-cache" : null, source.Config ? "with cache.strict set to false" : null }.OfType<string>());
        var run = "re-record it with a read-write run " + off;
        if (cache.Store is not null)
        {
            return run + "; the run writes the new entry to the configured cache.store";
        }

        var where = Paths.DisplayPath(projectRoot, cache.Directory);
        return Paths.InsideProjectRoot(projectRoot, cache.Directory)
            ? run + " and commit the changed entry under " + where
            : run + "; the run writes the new entry under " + where;
    }
}
