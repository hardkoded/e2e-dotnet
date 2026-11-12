// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.LocatorEngine;

/// <summary>One scripted answer of a fake locate: what it resolves, given the budget of that locate.</summary>
internal delegate Task<IReadOnlyList<SemanticNode>> ResolveStep(TimeSpan budget);

/// <summary>
/// The <c>makeEngine</c> of upstream's <c>locator-engine.test.ts</c>: a <see cref="E2E.LocatorEngine"/> over a fake session whose locate and
/// perform answer the next entry of a script, and a default past its end (the one match, an action that succeeds). The port's locate answers
/// the nodes themselves, so there is no <c>read</c> script: a node is read as it is resolved.
/// </summary>
internal sealed class ScriptedLocatorEngine
{
    /// <summary>The one node a default locate answers, upstream's <c>NODE</c>.</summary>
    public static readonly SemanticNode Node = new() { Ref = "node-1", Role = "button", Name = "Submit" };

    /// <summary>The expression every test resolves, upstream's <c>EXPRESSION</c>: a role query for a button.</summary>
    public static readonly LocatorExpression Expression = new Screen(
        _ => Task.FromResult(new Observation { Route = "/", Roots = [] }),
        (_, _, _) => Task.CompletedTask,
        () => CancellationToken.None,
        () => { },
        TimeSpan.FromMilliseconds(300),
        TimeSpan.FromMilliseconds(300)).GetByRole("button").Query;

    private readonly List<ResolveStep> _resolve;
    private readonly List<Func<Task>> _perform;
    private readonly List<TimeSpan> _resolveTimeouts = [];
    private readonly List<OperationOrigin> _origins = [];
    private readonly List<LocatorAction> _performed = [];
    private readonly List<(Point Point, PointerAction Action)> _performedAt = [];
    private readonly object _gate = new();
    private int _resolves;
    private int _performs;

    private ScriptedLocatorEngine(List<ResolveStep> resolve, List<Func<Task>> perform, TimeSpan actionTimeout, TimeProvider? clock)
    {
        _resolve = resolve;
        _perform = perform;
        Engine = new E2E.LocatorEngine(new LocatorEngineOptions
        {
            Observe = _ => Task.FromResult(new Observation { Route = "/", Roots = [] }),
            Locate = (_, budget, _) => LocateAsync(budget),
            Perform = (_, action, context, _) => PerformAsync(action, context),
            PerformAt = (point, action, _, _) =>
            {
                lock (_gate)
                {
                    _performedAt.Add((point, action));
                }

                return Task.CompletedTask;
            },
            Cancellation = () => CancellationToken.None,
            ActionTimeout = actionTimeout,
            AssertionTimeout = TimeSpan.FromMilliseconds(1_000),
            Clock = clock ?? TimeProvider.System,
        });
    }

    /// <summary>The engine under test.</summary>
    public E2E.LocatorEngine Engine { get; }

    /// <summary>How many locates, and how many performs, the session has answered.</summary>
    public int Resolves => Locked(() => _resolves);

    /// <inheritdoc cref="Resolves"/>
    public int Performs => Locked(() => _performs);

    /// <summary>The budget each locate was given, in order (upstream's <c>resolveTimeouts</c>).</summary>
    public IReadOnlyList<TimeSpan> ResolveTimeouts => Locked(() => (IReadOnlyList<TimeSpan>)[.. _resolveTimeouts]);

    /// <summary>The origin each perform was marked with (upstream's <c>origins</c>).</summary>
    public IReadOnlyList<OperationOrigin> Origins => Locked(() => (IReadOnlyList<OperationOrigin>)[.. _origins]);

    /// <summary>The actions the session performed, in order (upstream's <c>performed</c>).</summary>
    public IReadOnlyList<LocatorAction> Performed => Locked(() => (IReadOnlyList<LocatorAction>)[.. _performed]);

    /// <summary>The pointer actions the session performed at a point, in order.</summary>
    public IReadOnlyList<(Point Point, PointerAction Action)> PerformedAt => Locked(() => (IReadOnlyList<(Point Point, PointerAction Action)>)[.. _performedAt]);

    /// <summary>A resolve that answers <paramref name="nodes"/>, as a default locate does.</summary>
    public static ResolveStep Nodes(params SemanticNode[] nodes) => _ => Task.FromResult<IReadOnlyList<SemanticNode>>(nodes);

    /// <summary>A resolve that answers <paramref name="nodes"/> after <paramref name="after"/>, ended at its budget (upstream's <c>slowResolve</c>).</summary>
    public static ResolveStep Slow(TimeSpan after, params SemanticNode[] nodes) => budget => SlowLocate.AnswerAsync(after, nodes, budget);

    /// <summary>A resolve that never answers on its own, ended at its budget.</summary>
    public static ResolveStep Hang => SlowLocate.HangAsync;

    /// <summary>A resolve that fails with a retryable <c>NODE_STALE</c>.</summary>
    public static ResolveStep Stale => Fail(new EngineException(EngineErrorCodes.NodeStale, "stale", retryable: true));

    /// <summary>A resolve that fails with a retryable <c>FRAME_NOT_FOUND</c>.</summary>
    public static ResolveStep Frame => Fail(new EngineException(EngineErrorCodes.FrameNotFound, "frame missing", retryable: true));

    /// <summary>A resolve that fails with a non-retryable <c>ENGINE_FAILURE</c>.</summary>
    public static ResolveStep Failure => Fail(new EngineException(EngineErrorCodes.EngineFailure, "engine died", retryable: false));

    /// <summary>A resolve that fails with a non-retryable <c>OPERATION_TIMEOUT</c>, as an engine does at the end of its budget.</summary>
    public static ResolveStep TimedOut => Fail(new EngineException(EngineErrorCodes.OperationTimeout, "locate timed out", retryable: false));

    /// <summary>A perform that fails with a retryable <c>NODE_STALE</c>.</summary>
    public static Func<Task> PerformStale { get; } = () => Task.FromException(new EngineException(EngineErrorCodes.NodeStale, "stale", retryable: true));

    /// <summary>A perform that fails with a non-retryable <c>ACTION_MAY_HAVE_COMMITTED</c>.</summary>
    public static Func<Task> Committed { get; } = () => Task.FromException(new EngineException(EngineErrorCodes.ActionMayHaveCommitted, "maybe committed", retryable: false));

    /// <summary>A perform that fails with a non-retryable <c>NOT_ACTIONABLE</c>.</summary>
    public static Func<Task> NotActionable { get; } = () => Task.FromException(new EngineException(EngineErrorCodes.NotActionable, "covered by overlay", retryable: false));

    /// <summary>A perform that succeeds.</summary>
    public static Func<Task> Succeeds { get; } = () => Task.CompletedTask;

    /// <summary>Builds the engine over a script, 1 s of action timeout unless <paramref name="actionTimeout"/> says otherwise.</summary>
    public static ScriptedLocatorEngine Make(IEnumerable<ResolveStep>? resolve = null, IEnumerable<Func<Task>>? perform = null, TimeSpan? actionTimeout = null, TimeProvider? clock = null) =>
        new([.. resolve ?? []], [.. perform ?? []], actionTimeout ?? TimeSpan.FromMilliseconds(1_000), clock);

    private T Locked<T>(Func<T> read)
    {
        lock (_gate)
        {
            return read();
        }
    }

    private static ResolveStep Fail(Exception failure) => _ => Task.FromException<IReadOnlyList<SemanticNode>>(failure);

    private Task<IReadOnlyList<SemanticNode>> LocateAsync(TimeSpan budget)
    {
        ResolveStep? step;
        lock (_gate)
        {
            _resolveTimeouts.Add(budget);
            step = _resolves < _resolve.Count ? _resolve[_resolves] : null;
            _resolves++;
        }

        return (step ?? Nodes(Node))(budget);
    }

    private Task PerformAsync(LocatorAction action, OperationContext context)
    {
        Func<Task>? step;
        lock (_gate)
        {
            _origins.Add(context.Origin);
            _performed.Add(action);
            step = _performs < _perform.Count ? _perform[_performs] : null;
            _performs++;
        }

        return (step ?? Succeeds)();
    }
}
