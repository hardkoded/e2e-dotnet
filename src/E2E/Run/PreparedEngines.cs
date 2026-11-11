// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics;
using E2E.Engine;

namespace E2E;

/// <summary>What <see cref="PreparedEngines.PrepareAsync"/> is given: the run it provisions for. Upstream's <c>PrepareScope</c> (<c>run/provision.ts</c>).</summary>
internal sealed record PrepareScope
{
    public required string RunId { get; init; }

    /// <summary>The config's directory, where an engine resolves a relative option.</summary>
    public required string ProjectRoot { get; init; }

    /// <summary>The same environment the workers are started with: what prepare provisions must be where a launch will look for it.</summary>
    public required IReadOnlyDictionary<string, string?> Env { get; init; }

    /// <summary>Where a hook's progress lines go, under the target they concern. The caller redacts the line.</summary>
    public required Action<string, string> Notice { get; init; }
}

/// <summary>What <see cref="PreparedEngines.FinishAsync"/> is given. Upstream's <c>FinishScope</c> (<c>run/provision.ts</c>).</summary>
internal sealed record FinishScope
{
    public required string RunId { get; init; }

    public required IReadOnlyDictionary<string, string?> Env { get; init; }

    /// <summary>The cleanup budget every prepared target shares.</summary>
    public required TimeSpan CleanupTimeout { get; init; }

    /// <summary>Where a hook's progress lines go, under the target they concern. The caller redacts the line.</summary>
    public required Action<string, string> Notice { get; init; }

    /// <summary>Takes every failure; none skips another target's release.</summary>
    public required Action<Exception> OnFailure { get; init; }
}

/// <summary>
/// The prepare and finish pairing of a run or a standalone attempt, in one place: every engine whose <c>prepare</c> hook was called gets its
/// <c>finish</c>, whatever happened in between, so what a hook acquired before it failed is still released. Upstream's class of that name
/// in <c>run/provision.ts</c>.
/// </summary>
internal sealed class PreparedEngines
{
    private readonly List<(IPreparableEngine Engine, string TargetName)> _targets = [];

    private readonly Dictionary<IEngine, Lazy<Task<EnginePrepareResult?>>> _prepared = new(ReferenceEqualityComparer.Instance);

    // Cancels the shared prepares still running when the run ends. Upstream's prepare is cancelled by an interrupt of the run, which is the one caller there is.
    private readonly CancellationTokenSource _runEnd = new();

    /// <summary>
    /// Runs one target's <c>prepare</c> hook for <paramref name="slots"/> worker slots, once per engine: a later call for the same engine waits for the
    /// first call's result, so a process that starts many attempts on one declared engine prepares it once (upstream's once per run and target).
    /// Null when the engine declares no hook. The engine is registered before the hook runs, so a <c>prepare</c> that throws part-way still gets its
    /// <c>finish</c>. A failed prepare is not kept: the next call runs the hook again. A caller who cancels stops waiting, and does not wait for a hook
    /// that ignores the token: what that hook grants later is released by <see cref="FinishAsync"/> where it lands. A <paramref name="shared"/> prepare,
    /// one for the whole process, is cancelled by the run's end, not by any one caller.
    /// </summary>
    public Task<EnginePrepareResult?> PrepareAsync(IEngine engine, string targetName, int slots, PrepareScope scope, CancellationToken cancellationToken, bool shared = false)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(scope);
        if (engine is not IPreparableEngine preparable)
        {
            return Task.FromResult<EnginePrepareResult?>(null);
        }

        Lazy<Task<EnginePrepareResult?>>? joined = null;
        Lazy<Task<EnginePrepareResult?>>? started = null;
        lock (_targets)
        {
            if (_prepared.TryGetValue(engine, out var existing) && !IsFailed(existing))
            {
                joined = existing;
            }
            else
            {
                if (!_targets.Any(target => ReferenceEquals(target.Engine, preparable)))
                {
                    _targets.Add((preparable, targetName));
                }

                // The hook starts when its value is first read, which is after the lock is released: a hook that does work before its first await does not hold the lock.
                started = new Lazy<Task<EnginePrepareResult?>>(
                    () => PrepareOneAsync(preparable, targetName, slots, scope, shared ? _runEnd.Token : cancellationToken),
                    LazyThreadSafetyMode.ExecutionAndPublication);
                _prepared[engine] = started;
            }
        }

        if (joined is not null)
        {
            return AwaitAsync(joined.Value, engine, targetName, cancellationToken);
        }

        var preparing = started!.Value;

        // A failed prepare is forgotten, so the next caller runs the hook again. Its finish is still owed: the engine stays in _targets.
        _ = preparing.ContinueWith(
            failed =>
            {
                _ = failed.Exception;
                lock (_targets)
                {
                    // Only this prepare's own entry: a finish may have cleared it and a newer prepare taken its place.
                    if (_prepared.TryGetValue(engine, out var current) && ReferenceEquals(current, started))
                    {
                        _prepared.Remove(engine);
                    }
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return AwaitAsync(preparing, engine, targetName, cancellationToken);
    }

    /// <summary>A memoized prepare that has already faulted or been cancelled: a caller starts the hook again rather than share the failure.</summary>
    private static bool IsFailed(Lazy<Task<EnginePrepareResult?>> prepared) =>
        prepared.IsValueCreated && (prepared.Value.IsFaulted || prepared.Value.IsCanceled);

    /// <summary>
    /// Waits for a prepare on one caller's token. This is the one place a prepare's failure is translated, so the message names the engine and target once,
    /// and a cancel stays a cancel.
    /// </summary>
    private static async Task<EnginePrepareResult?> AwaitAsync(Task<EnginePrepareResult?> preparing, IEngine engine, string targetName, CancellationToken cancellationToken)
    {
        try
        {
            return await preparing.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception cause)
        {
            var translated = ErrorSerializer.TranslateProvisioningError(cause, $" while preparing engine {engine.Platform} for target \"{targetName}\"", cancellationToken);
            if (ReferenceEquals(translated, cause))
            {
                throw;
            }

            throw translated;
        }
    }

    private static async Task<EnginePrepareResult?> PrepareOneAsync(IPreparableEngine engine, string targetName, int slots, PrepareScope scope, CancellationToken hookToken) =>
        await engine.PrepareAsync(
            new EnginePrepareInfo
            {
                RunId = scope.RunId,
                TargetName = targetName,
                ProjectRoot = scope.ProjectRoot,
                Slots = slots,
                Env = scope.Env,
                Log = line => scope.Notice(targetName, line),
            },
            hookToken).ConfigureAwait(false);

    /// <summary>
    /// Runs the <c>finish</c> hook of every prepared target, all at once under one cleanup deadline: releases are independent, and a run
    /// should not wait one budget per target. Each target's worker shutdown (its <see cref="IDisposableEngine"/> dispose, with no reuse grant) runs
    /// first, within half of that deadline, and a dispose that fails or hangs is reported without skipping the finish. A hook that fails, or outlives
    /// the deadline (it is abandoned there with <c>CLEANUP_TIMEOUT</c>, whether or not it honors its token), is reported to <see cref="FinishScope.OnFailure"/>.
    /// A shared prepare still running is cancelled first, since the run is over.
    /// </summary>
    public async Task FinishAsync(FinishScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        (IPreparableEngine Engine, string TargetName)[] targets;
        lock (_targets)
        {
            targets = [.. _targets];
            _targets.Clear();
            foreach (var target in targets)
            {
                _prepared.Remove(target.Engine);
            }
        }

        _runEnd.Cancel();
        var clock = Stopwatch.StartNew();
        await Task.WhenAll(targets.Select(target => FinishOneAsync(target.Engine, target.TargetName, scope, clock))).ConfigureAwait(false);
    }

    /// <summary>
    /// The environment a worker starts with after prepare: <paramref name="prepared"/> on top of <paramref name="run"/>, where the run's own variables win a clash
    /// (upstream's <c>EnginePrepareResult.env</c> merged under the run's environment).
    /// </summary>
    public static IReadOnlyDictionary<string, string?> MergeWorkerEnv(IReadOnlyDictionary<string, string?> run, IReadOnlyDictionary<string, string> prepared)
    {
        var merged = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (name, value) in prepared)
        {
            merged[name] = value;
        }

        foreach (var (name, value) in run)
        {
            merged[name] = value;
        }

        return merged;
    }

    /// <summary>The environment of this process, where a provider reads its token from when no one passes another.</summary>
    public static IReadOnlyDictionary<string, string?> ProcessEnvironment()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            env[(string)entry.Key] = entry.Value as string;
        }

        return env;
    }

    private static async Task FinishOneAsync(IPreparableEngine engine, string targetName, FinishScope scope, Stopwatch clock)
    {
        var label = $"finishing engine {engine.Platform} for target \"{targetName}\"";
        var bounded = scope.CleanupTimeout > TimeSpan.Zero && scope.CleanupTimeout != Timeout.InfiniteTimeSpan;
        TimeSpan Left() => bounded ? TimeSpan.FromTicks(Math.Max((scope.CleanupTimeout - clock.Elapsed).Ticks, TimeSpan.TicksPerMillisecond)) : Timeout.InfiniteTimeSpan;

        if (engine is IDisposableEngine disposable)
        {
            // Half the remaining budget, so the finish keeps the other half whatever the dispose does.
            var disposeBudget = bounded ? TimeSpan.FromTicks(Left().Ticks / 2) : Timeout.InfiniteTimeSpan;
            await DisposeOneAsync(disposable, targetName, scope, disposeBudget).ConfigureAwait(false);
        }

        var remaining = Left();
        using var budget = new CancellationTokenSource(remaining);
        Task? finishing = null;
        try
        {
            finishing = engine.FinishAsync(
                new EngineFinishInfo
                {
                    RunId = scope.RunId,
                    TargetName = targetName,
                    Env = scope.Env,
                    CleanupTimeout = bounded ? remaining : scope.CleanupTimeout,
                    Log = line => scope.Notice(targetName, line),
                },
                budget.Token);
            await finishing.WaitAsync(Left()).ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is TimeoutException || budget.IsCancellationRequested)
        {
            if (finishing is not null)
            {
                Observe(finishing);
            }

            // A hook that gave up on its token is a timeout too: the budget ended it, and what it held may still bill.
            scope.OnFailure(new EngineException("CLEANUP_TIMEOUT", $"{label} timed out; what it held may stay billed until its own time limit", cause));
        }
        catch (Exception cause)
        {
            scope.OnFailure(ErrorSerializer.TranslateProvisioningError(cause, $" while {label}", CancellationToken.None));
        }
    }

    /// <summary>
    /// The worker shutdown of one engine, with no reuse permission (upstream's <c>dispose</c> at worker shutdown). A failure or an overrun is reported
    /// to <see cref="FinishScope.OnFailure"/>, and the caller goes on to the finish either way.
    /// </summary>
    private static async Task DisposeOneAsync(IDisposableEngine engine, string targetName, FinishScope scope, TimeSpan budget)
    {
        var label = $"disposing engine {engine.Platform} for target \"{targetName}\"";
        using var cancel = new CancellationTokenSource(budget);
        Task? disposing = null;
        try
        {
            disposing = engine.DisposeAsync(new EngineDisposeContext { CleanupTimeout = budget, ReuseSignal = null }, cancel.Token);
            await disposing.WaitAsync(budget).ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is TimeoutException || cancel.IsCancellationRequested)
        {
            if (disposing is not null)
            {
                Observe(disposing);
            }

            scope.OnFailure(new EngineException("CLEANUP_TIMEOUT", $"{label} timed out; what its workers held may stay billed until its own time limit", cause));
        }
        catch (Exception cause)
        {
            scope.OnFailure(ErrorSerializer.TranslateProvisioningError(cause, $" while {label}", CancellationToken.None));
        }
    }

    /// <summary>A hook nobody awaits any more must not leave an unobserved fault behind.</summary>
    private static void Observe(Task task) =>
        _ = task.ContinueWith(static finished => finished.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
}
