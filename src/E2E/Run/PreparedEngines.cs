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

    /// <summary>Where a hook's progress lines go, under the target they concern.</summary>
    public required Action<string, string> Notice { get; init; }
}

/// <summary>What <see cref="PreparedEngines.FinishAsync"/> is given. Upstream's <c>FinishScope</c> (<c>run/provision.ts</c>).</summary>
internal sealed record FinishScope
{
    public required string RunId { get; init; }

    public required IReadOnlyDictionary<string, string?> Env { get; init; }

    /// <summary>The cleanup budget every prepared target shares.</summary>
    public required TimeSpan CleanupTimeout { get; init; }

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

    /// <summary>
    /// Runs one target's <c>prepare</c> hook for <paramref name="slots"/> worker slots. Null when the engine declares no hook. The engine is
    /// registered before the hook runs, so a <c>prepare</c> that throws part-way still gets its <c>finish</c>. A caller who cancels does not
    /// wait for a hook that ignores the token: what that hook grants later is released by <see cref="FinishAsync"/> where it lands.
    /// </summary>
    public async Task<EnginePrepareResult?> PrepareAsync(IEngine engine, string targetName, int slots, PrepareScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(scope);
        if (engine is not IPreparableEngine preparable)
        {
            return null;
        }

        lock (_targets)
        {
            _targets.Add((preparable, targetName));
        }

        Task<EnginePrepareResult>? preparing = null;
        try
        {
            preparing = preparable.PrepareAsync(
                new EnginePrepareInfo
                {
                    RunId = scope.RunId,
                    TargetName = targetName,
                    ProjectRoot = scope.ProjectRoot,
                    Slots = slots,
                    Env = scope.Env,
                    Log = line => scope.Notice(targetName, line),
                },
                cancellationToken);
            return await preparing.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception cause)
        {
            if (preparing is not null)
            {
                Observe(preparing);
            }

            var translated = ErrorSerializer.TranslateProvisioningError(cause, $" while preparing engine {engine.Platform} for target \"{targetName}\"", cancellationToken);
            if (ReferenceEquals(translated, cause))
            {
                throw;
            }

            throw translated;
        }
    }

    /// <summary>
    /// Runs the <c>finish</c> hook of every prepared target, all at once under one cleanup deadline: releases are independent, and a run
    /// should not wait one budget per target. A hook that fails, or outlives the deadline (it is abandoned there with
    /// <c>CLEANUP_TIMEOUT</c>, whether or not it honors its token), is reported to <see cref="FinishScope.OnFailure"/>.
    /// </summary>
    public async Task FinishAsync(FinishScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        (IPreparableEngine Engine, string TargetName)[] targets;
        lock (_targets)
        {
            targets = [.. _targets];
            _targets.Clear();
        }

        var clock = Stopwatch.StartNew();
        await Task.WhenAll(targets.Select(target => FinishOneAsync(target.Engine, target.TargetName, scope, clock))).ConfigureAwait(false);
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
        var remaining = bounded ? TimeSpan.FromTicks(Math.Max((scope.CleanupTimeout - clock.Elapsed).Ticks, TimeSpan.TicksPerMillisecond)) : Timeout.InfiniteTimeSpan;
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
            await finishing.WaitAsync(remaining).ConfigureAwait(false);
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

    /// <summary>A hook nobody awaits any more must not leave an unobserved fault behind.</summary>
    private static void Observe(Task task) =>
        _ = task.ContinueWith(static finished => finished.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
}
