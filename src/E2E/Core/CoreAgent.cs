// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E;

/// <summary>Where <see cref="CoreAgent.ActAsync"/> keeps the flows it recorded, and whether it may write new ones. Upstream's <c>AgentCacheOptions</c> (<c>core/agent.ts</c>).</summary>
public sealed class AgentCacheOptions
{
    /// <summary>The file store's directory, from the <see cref="LaunchOptions.Cwd"/> the host was launched with; default <c>.e2e/cache</c>. Ignored with <see cref="Store"/>.</summary>
    public string? Dir { get; init; }

    /// <summary><see cref="CacheMode.ReadWrite"/> (the default) replays and records; <see cref="CacheMode.ReadOnly"/> replays and never writes. <see cref="CacheMode.Off"/> is <c>INVALID_ARGUMENT</c>: leave <see cref="CreateAgentOptions.Cache"/> unset.</summary>
    public CacheMode Mode { get; init; } = CacheMode.ReadWrite;

    /// <summary>A custom store in place of the file store, the seam a shared cache plugs into.</summary>
    public IStepCache? Store { get; init; }

    /// <summary>Replays only: a recording that no longer replays fails its step with <c>REPLAY_STALE</c> instead of calling the model, and nothing is written.</summary>
    public bool Strict { get; init; }
}

/// <summary>The agent's model, budgets, and context, as an <c>agents</c> entry of a config takes them (<see cref="AgentOptions"/>), plus what only a program sets. Upstream's <c>CreateAgentOptions</c> (<c>core/agent.ts</c>).</summary>
public sealed class CreateAgentOptions : AgentOptions
{
    /// <summary>Replays recorded <c>ActAsync</c> flows; every <c>ActAsync</c> then names its recording with <see cref="CachingActOptions.CacheKey"/>. Unset, every <c>ActAsync</c> calls the model.</summary>
    public AgentCacheOptions? Cache { get; init; }
}

/// <summary>
/// <see cref="ActOptions"/> for <see cref="CoreAgent.ActAsync"/>: each call names the recording it replays and records, or runs live. Upstream's
/// <c>CachingActOptions</c> (<c>core/agent.ts</c>), which TypeScript types as a union; C# holds both members, and the agent refuses the one that
/// does not fit it at runtime. <see cref="ActOptions.Agent"/> is <c>INVALID_ARGUMENT</c>: create another agent for another configuration.
/// </summary>
public sealed class CachingActOptions : ActOptions
{
    /// <summary>
    /// The recording this call replays and records, unique within what the program does: <c>checkout</c>, <c>cart-best-coupon</c>. The key is
    /// this name, the instruction, its params, and how many times the session made the same call, so a reworded instruction records afresh.
    /// </summary>
    public string? CacheKey { get; init; }

    /// <summary><c>false</c> runs this call live, neither replaying nor recording. Any other value is <c>INVALID_ARGUMENT</c>.</summary>
    public bool? Cache { get; init; }
}

/// <summary>
/// What <see cref="Agent"/> is, for a core agent: the agent a program created, not one a config names. It is the one agent, so a call that names
/// another is refused, and it has no model until the program passes one. Upstream's <c>AgentContext</c> override of <c>createAgent</c>.
/// </summary>
internal sealed class CoreAgentBinding(ResolvedAgent agent, AgentCacheContext? cache)
{
    /// <summary>The cache context of this agent's calls, or null for a call that runs live.</summary>
    public AgentCacheContext? Cache { get; } = cache;

    public ResolvedAgent Select(string? requested)
    {
        if (requested is not null)
        {
            throw new TestException(
                "INVALID_ARGUMENT",
                "an E2E core agent takes no Agent option: create another agent for another configuration"
            );
        }

        if (agent.Model is null && agent.Executor is null)
        {
            throw new AgentException(
                "MODEL_UNAVAILABLE",
                "the agent has no model: pass one to CreateAgent, an IAgentModel such as CoreAgents.CreateAgent(session, new CreateAgentOptions { Model = model })"
            );
        }

        return agent;
    }
}

/// <summary>
/// An agent over one session, configured in code: <c>ActAsync</c>, <c>AssertAsync</c>, <c>WaitForAsync</c>, and <c>ExtractAsync</c> behave as they
/// do in a test. With a cache, <c>ActAsync</c> replays recorded flows, each keyed by the <see cref="CachingActOptions.CacheKey"/> the call names.
/// Upstream's <c>CachingAgent</c>, <c>CoreAgent</c>, and <c>UncheckedAgent</c> (<c>core/agent.ts</c>), which TypeScript tells apart by the options
/// <c>createAgent</c> was called with: C# has one type, and refuses at runtime the <c>ActAsync</c> call that does not fit the agent.
/// </summary>
public sealed class CoreAgent
{
    private readonly AttemptScope _scope;
    private readonly ResolvedAgent _resolved;
    private readonly Agent _live;
    private readonly Func<string, AgentCacheContext?>? _cacheFor;

    internal CoreAgent(AttemptScope scope, ResolvedAgent resolved, Func<string, AgentCacheContext?>? cacheFor)
    {
        _scope = scope;
        _resolved = resolved;
        _cacheFor = cacheFor;
        _live = new Agent(scope, new CoreAgentBinding(resolved, null));
    }

    /// <summary>
    /// Runs one goal. On an agent with a cache the call names its recording with <see cref="CachingActOptions.CacheKey"/>, or runs live with
    /// <see cref="CachingActOptions.Cache"/> set to <c>false</c>; without one it fails with <c>INVALID_ARGUMENT</c>. On an agent without a cache,
    /// naming either fails with <c>INVALID_ARGUMENT</c>.
    /// </summary>
    public async Task<ActResult> ActAsync(
        string instruction,
        CachingActOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var rest = options is null
            ? null
            : new ActOptions
            {
                Params = options.Params,
                Timeout = options.Timeout,
                MaxSteps = options.MaxSteps,
                MaxModelCalls = options.MaxModelCalls,
                Agent = options.Agent,
                OnAction = options.OnAction,
                OnBeforeAction = options.OnBeforeAction,
            };
        var cacheKey = options?.CacheKey;
        var perCall = options?.Cache;
        if (_cacheFor is null)
        {
            if (cacheKey is not null || perCall is not null)
            {
                throw new TestException(
                    "INVALID_ARGUMENT",
                    "ActAsync CacheKey and Cache need an agent created with CreateAgent(session, new CreateAgentOptions { Cache = ... })"
                );
            }

            return await _live.ActAsync(instruction, rest, cancellationToken).ConfigureAwait(false);
        }

        if (perCall == false && cacheKey is null)
        {
            return await _live.ActAsync(instruction, rest, cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(cacheKey) || perCall is not null)
        {
            throw new TestException(
                "INVALID_ARGUMENT",
                "ActAsync on an agent with a cache names its recording: pass a CacheKey, or Cache = false to run live"
            );
        }

        var context = _cacheFor(cacheKey);
        return await new Agent(_scope, new CoreAgentBinding(_resolved, context))
            .ActAsync(instruction, rest, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Checks that the statement holds on the current screen. See <see cref="Agent.AssertAsync"/>.</summary>
    public Task AssertAsync(
        string statement,
        AssertOptions? options = null,
        CancellationToken cancellationToken = default
    ) => _live.AssertAsync(statement, options, cancellationToken);

    /// <summary>Waits until the statement holds on screen. See <see cref="Agent.WaitForAsync"/>.</summary>
    public Task WaitForAsync(
        string statement,
        WaitForOptions? options = null,
        CancellationToken cancellationToken = default
    ) => _live.WaitForAsync(statement, options, cancellationToken);

    /// <summary>Reads data off the current screen. See <see cref="Agent.ExtractAsync"/>.</summary>
    public Task<T> ExtractAsync<T>(
        string instruction,
        ExtractOptions? options = null,
        CancellationToken cancellationToken = default
    ) => _live.ExtractAsync<T>(instruction, options, cancellationToken);
}

/// <summary><c>createAgent</c> from <c>@e2e-dev/core/agent</c>: the agent over one session, configured in code. Upstream's <c>core/agent.ts</c>.</summary>
public static class CoreAgents
{
    private const string AgentName = "agent";

    /// <summary>
    /// What stands in a recording's key where a run puts its project. A run reads it off the project, or hashes the directory when there is none;
    /// a program's recordings must be found from any directory and on any machine, so a shared store works, and the cache key already names the flow.
    /// </summary>
    private const string CacheProjectId = "@e2e-dev/core";

    /// <summary>
    /// Creates an agent over <paramref name="session"/>. With <see cref="CreateAgentOptions.Cache"/>, its <see cref="CoreAgent.ActAsync"/> replays flows,
    /// each named by its <see cref="CachingActOptions.CacheKey"/>, and records them when the session closes. A missing model fails the first call that
    /// needs one as <c>MODEL_UNAVAILABLE</c>, never the creation.
    /// </summary>
    public static CoreAgent CreateAgent(Session session, CreateAgentOptions options)
    {
        var internals = InternalsOf(session);
        if (options is null)
        {
            throw new TestException("INVALID_ARGUMENT", "CreateAgent options must be an object with a model");
        }

        var resolved = ResolvedAgent.From(AgentName, options);
        var cache = options.Cache is null ? null : ResolveAgentCache(options.Cache, internals.ProjectRoot);
        if (cache is null)
        {
            return new CoreAgent(session.E2E.Scope, resolved, null);
        }

        // One cache context per key, each its own occurrence count; all settle when the session closes.
        var scope = session.E2E.Scope;
        var caches = new Dictionary<string, AgentCacheContext?>(StringComparer.Ordinal);
        var gate = new Lock();
        AgentCacheContext? CacheFor(string cacheKey)
        {
            lock (gate)
            {
                if (!caches.TryGetValue(cacheKey, out var context))
                {
                    caches[cacheKey] = context = CacheContexts.CreateAgentCacheContext(
                        cache,
                        internals.ProjectRoot,
                        0,
                        CacheProjectId,
                        "core:" + cacheKey,
                        new CacheTargetIdentity(
                            scope.TargetName,
                            scope.EnginePlatform,
                            scope.EngineName,
                            BuildReport.EngineSpiVersion,
                            scope.AppIdentity
                        )
                    );
                }

                return context;
            }
        }

        internals.Closers.Add(() =>
        {
            var settlement = internals.Settlement();
            AgentCacheContext?[] contexts;
            lock (gate)
            {
                contexts = [.. caches.Values];
            }

            foreach (var context in contexts)
            {
                if (context is not null)
                {
                    CacheContexts.SettleStagedTraces(context, settlement);
                }
            }

            return Task.CompletedTask;
        });
        return new CoreAgent(scope, resolved, CacheFor);
    }

    private static SessionInternals InternalsOf(Session session) =>
        session?.Internals
        ?? throw new TestException("INVALID_ARGUMENT", "CreateAgent takes a session from host.NewSessionAsync()");

    /// <summary>The cache options as the replay cache reads them: the file store under the project root unless a store replaces it.</summary>
    private static CacheConfig ResolveAgentCache(AgentCacheOptions cache, string projectRoot)
    {
        if (cache.Mode is not (CacheMode.ReadWrite or CacheMode.ReadOnly))
        {
            throw new TestException("INVALID_ARGUMENT", "CreateAgent Cache.Mode must be ReadWrite or ReadOnly");
        }

        if (cache.Dir is not null && string.IsNullOrWhiteSpace(cache.Dir))
        {
            throw new TestException("INVALID_ARGUMENT", "CreateAgent Cache.Dir must be a non-empty path");
        }

        return new CacheConfig
        {
            Mode = cache.Mode,
            Store = cache.Store,
            Directory = Path.GetFullPath(cache.Dir ?? Path.Combine(".e2e", "cache"), projectRoot),
            Strict = cache.Strict,
            StrictSource = cache.Strict ? new CacheStrictSource(true, false) : null,
        };
    }
}
