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
/// Upstream's <c>AgentCacheContext</c> (<c>cache/context.ts</c>), as far as the port has it: the resolved store, whether the attempt
/// may replay, and, for a context a program created (<see cref="CoreAgents"/>), the <c>claimKey</c> that names its recordings and the
/// <c>staged</c> acts it settles. A test's attempt keeps both on <c>AttemptScope</c>, which builds its context per act.
/// </summary>
internal sealed record AgentCacheContext(
    CacheMode Mode,
    IStepCache Store,
    bool ReplayEligible,
    AgentCacheStrict? Strict
)
{
    /// <summary>The test id its keys and entries carry: the test's title, or <c>core:&lt;cacheKey&gt;</c> for a program's agent.</summary>
    public string TestId { get; init; } = "";

    /// <summary>Upstream's <c>claimKey</c>: claims one act's key, advancing its occurrence count.</summary>
    public Func<
        string,
        IReadOnlyDictionary<string, object?>?,
        CacheAgentIdentity,
        ClaimedTraceKey
    > ClaimKey { get; init; } = static (_, _, _) => throw new InvalidOperationException("the context claims no keys");

    /// <summary>Upstream's <c>staged</c>: the acts this context's agent ran, which settle when the context's session ends. Null when the attempt settles them.</summary>
    public List<PendingAct>? Staged { get; init; }
}

/// <summary>What <see cref="AgentCacheContext.ClaimKey"/> claimed: the key's hash, the zero-based repeat of its signature, and the parts of the key outside the step.</summary>
internal sealed record ClaimedTraceKey(string Key, int CallIndex, TraceKeyContext Context);

/// <summary>How an attempt ended, as the settlement of its staged entries reads it. Upstream's <c>AttemptSettlement</c> (<c>cache/context.ts</c>).</summary>
/// <param name="LastVerifiedStepIndex">Entries staged before this step index were verified, and are confirmed.</param>
/// <param name="ImplicatesUnconfirmed">
/// Whether the attempt implicates the entries it did not confirm. False when its failure is that no model answered, a verdict on the provider and
/// never on the app: unconfirmed entries are then left exactly as they are, neither written nor evicted.
/// </param>
/// <param name="Survived">
/// Failures the attempt went on after, which only a program without a test around it does: each settles the entries it found unconfirmed as that
/// failure does, whatever passed later. Entries staged after it are judged by the checks that follow them.
/// </param>
internal sealed record AttemptSettlement(
    int LastVerifiedStepIndex,
    bool ImplicatesUnconfirmed,
    IReadOnlyList<SurvivedFailure> Survived
);

/// <summary>One failed step an attempt went on after, and the staged entries it found unconfirmed. Upstream's <c>SurvivedFailure</c> (<c>cache/context.ts</c>).</summary>
/// <param name="From">The first step index no verification had confirmed when the step failed.</param>
/// <param name="Through">The failed step's index, the last the failure covers.</param>
/// <param name="Implicates">Whether the failure implicates those entries; see <see cref="AttemptSettlement.ImplicatesUnconfirmed"/>.</param>
internal sealed record SurvivedFailure(int From, int Through, bool Implicates);

/// <summary>Upstream's <c>cache/context.ts</c>: the per-attempt cache context. .NET-only name: <c>Context</c> would collide with <c>E2ESession.Context</c>.</summary>
internal static class CacheContexts
{
    /// <summary>
    /// Upstream's <c>createAgentCacheContext</c>: one attempt's cache context, or null when the cache is off. A configured custom store
    /// replaces the file store wholesale. Replay is eligible only on a first attempt (<paramref name="attemptIndex"/> 0) unless
    /// <c>cache.strict</c> is on, where every attempt replays, nothing is written or evicted, and the mode is <see cref="CacheMode.ReadOnly"/>.
    /// The port has no <c>projectId</c>, <c>testId</c>, or <c>target</c> parameter: <c>AttemptScope</c> claims the keys.
    /// </summary>
    public static AgentCacheContext? CreateAgentCacheContext(
        CacheConfig cache,
        string projectRoot,
        int attemptIndex,
        string projectId = "",
        string testId = "",
        CacheTargetIdentity? target = null
    )
    {
        ArgumentNullException.ThrowIfNull(cache);
        if (cache.Mode == CacheMode.Off)
        {
            return null;
        }

        var strict = cache.Strict;
        var mode = strict ? CacheMode.ReadOnly : cache.Mode;
        var store = cache.Store
            ?? new FileStepCache(cache.Directory, FileStepCache.MaxCacheWireBytes, mode == CacheMode.ReadWrite);
        return new AgentCacheContext(
            mode,
            store,
            strict || attemptIndex == 0,
            strict
                ? new AgentCacheStrict(
                    StaleAdvice(cache, cache.StrictSource ?? new CacheStrictSource(true, false), projectRoot)
                )
                : null
        )
        {
            TestId = testId,
            ClaimKey = target is null
                ? static (_, _, _) => throw new InvalidOperationException("the context claims no keys")
                : KeyClaimer(projectId, testId, target),
            Staged = target is null ? null : [],
        };
    }

    /// <summary>
    /// Upstream's <c>claimKey</c> of <c>createAgentCacheContext</c>: one occurrence count per agent (the agent is part of the key, so another
    /// agent's call of the same instruction must not renumber this one's), and the hash of the trace cache key the call claims.
    /// </summary>
    internal static Func<string, IReadOnlyDictionary<string, object?>?, CacheAgentIdentity, ClaimedTraceKey> KeyClaimer(
        string projectId,
        string testId,
        CacheTargetIdentity target
    )
    {
        var project = Identity.ProjectIdentity(projectId);
        var indexers = new Dictionary<string, Func<TraceCallSignature, int>>(StringComparer.Ordinal);
        var gate = new Lock();
        return (instruction, parameters, agent) =>
        {
            var signature = Identity.TraceCallSignature(Identity.ActKind, instruction, CacheKeys.Project(parameters));
            lock (gate)
            {
                // Folded like the key folds it: no context is the empty one.
                var agentKey = Ids.CanonicalJson(new System.Text.Json.Nodes.JsonArray(agent.Name, agent.Context ?? ""));
                if (!indexers.TryGetValue(agentKey, out var indexer))
                {
                    indexers[agentKey] = indexer = Identity.CreateCallIndexer();
                }

                var callIndex = indexer(signature);
                var key = Identity.BuildTraceCacheKey(
                    new TraceCacheKeyParts(
                        project,
                        testId,
                        target,
                        signature,
                        callIndex,
                        agent,
                        CacheKeys.ReplayPolicyVersion
                    )
                );
                return new ClaimedTraceKey(Identity.TraceCacheKeyHash(key), callIndex, Identity.KeyContext(key));
            }
        };
    }

    /// <summary>
    /// Upstream's <c>settleStagedTraces</c>, for a context a program created: writes the acts the context staged that a check confirmed, and
    /// evicts the ones a failure found unconfirmed. An act is confirmed when it ran before the last verification and no surviving failure covers
    /// it. An unconfirmed one is evicted only when the failure covering it implicates the recording, or the settlement does; otherwise it is left
    /// exactly as it was. Nothing settles in a read-only context, or without a settlement.
    /// </summary>
    public static void SettleStagedTraces(AgentCacheContext context, AttemptSettlement? settlement)
    {
        var staged = context.Staged;
        if (staged is null)
        {
            return;
        }

        List<PendingAct> acts;
        lock (staged)
        {
            acts = [.. staged];
            staged.Clear();
        }

        if (settlement is null || context.Mode != CacheMode.ReadWrite)
        {
            return;
        }

        var covers = new Dictionary<PendingAct, SurvivedFailure?>();
        foreach (var act in acts)
        {
            var failure = act.StepIndex is { } index
                ? settlement.Survived.FirstOrDefault(survived => index >= survived.From && index <= survived.Through)
                : null;
            covers[act] = failure;
            act.Verified = failure is null && act.StepIndex is { } at && at < settlement.LastVerifiedStepIndex;
        }

        E2ESession.Flush(context.Store, acts, act => !(covers[act]?.Implicates ?? settlement.ImplicatesUnconfirmed));
    }

    /// <summary>
    /// Upstream's <c>staleAdvice</c>: how to re-record a recording <c>cache.strict</c> failed on, a read-write run with every knob that
    /// turned strict on turned off, and where the new entry lands, as the project names it.
    /// </summary>
    private static string StaleAdvice(CacheConfig cache, CacheStrictSource source, string projectRoot)
    {
        var off = string.Join(
            " and ",
            new[]
            {
                source.Flag ? "without --strict-cache" : null,
                source.Config ? "with cache.strict set to false" : null,
            }.OfType<string>()
        );
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
