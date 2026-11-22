// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E;

// Holding steps for `e2e run --step` and tests at `screen.pause()` (upstream's run/step-hold.ts). A worker holds a top-level step before it runs,
// or a test at a pause, and asks the runner, which owns the terminal, whether it may. While any step of an attempt is held, the attempt's test
// clock is stopped, so the time a person spends looking at the app is never charged to the test.

/// <summary>A held step or pause without the runner-assigned attachment ID (upstream's <c>StepHold</c>).</summary>
internal sealed record StepHold
{
    public required StepHoldKind Kind { get; init; }

    /// <summary>Expected position in the trace, from 1. A pause uses 0.</summary>
    public required int Number { get; init; }

    public required string Api { get; init; }

    /// <summary>Already redacted.</summary>
    public required string Label { get; init; }

    public SourceLocation? Source { get; init; }

    public required string Target { get; init; }

    public required string TestId { get; init; }

    public required string Agent { get; init; }

    public required int Repeat { get; init; }

    /// <summary>The test's title path, joined with <c> › </c>.</summary>
    public required string Title { get; init; }
}

/// <summary>What an attempt's holds need from the attempt (upstream's <c>AttemptHoldsOptions</c>).</summary>
internal sealed record AttemptHoldsOptions
{
    /// <summary>The test clock a hold stops; it hands the watchdog what is left when it runs again.</summary>
    public required AttemptTimeout Timeout { get; init; }

    /// <summary>Cancelled when the attempt is cut short (a timeout, an interrupt): every hold ends and none starts.</summary>
    public required CancellationToken Signal { get; init; }

    /// <summary>Asks the runner whether <c>step</c> may run: it completes to run the step or resume the test.</summary>
    public required Func<StepHoldRequest, CancellationToken, Task> Ask { get; init; }

    /// <summary>The last pending hold ended and the test clock runs again.</summary>
    public required Action OnResume { get; init; }
}

/// <summary>
/// The holds of one attempt and the test clock they stop. The clock stops when the first hold begins and runs again when no hold is pending, so
/// steps held together in one <c>Task.WhenAll</c> are charged their wait once. Every release hands the phase a fresh deadline of what was left when
/// the clock stopped, so a released step starts with that budget even while a step beside it is still held. Upstream's <c>AttemptHolds</c>.
/// </summary>
internal sealed class AttemptHolds(AttemptHoldsOptions options)
{
    private readonly Lock _gate = new();
    private int _pending;

    /// <summary>The hold a <see cref="StepRecorder"/> takes; see <see cref="RecorderHold"/> (upstream's <c>hold</c>).</summary>
    public async Task HoldAsync(StepHoldRequest step, CancellationToken cancellationToken)
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, options.Signal);
        cancel.Token.ThrowIfCancellationRequested();

        // Already past the deadline, the body times out now, not after a prompt. Checked and counted under one lock with the release below, so a
        // hold that starts as another ends sees the clock the release leaves.
        lock (_gate)
        {
            if (_pending == 0 && !options.Timeout.Stop())
            {
                return;
            }

            _pending++;
        }

        try
        {
            await options.Ask(step, cancel.Token).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                if (--_pending > 0)
                {
                    options.Timeout.Refill();
                }
                else
                {
                    options.Timeout.Start();
                    options.OnResume();
                }
            }
        }
    }
}

/// <summary>What a test attempt's holds are built from (upstream's <c>AttemptStepHolds</c>).</summary>
internal sealed record AttemptStepHolds
{
    /// <summary>The attempt's test clock; see <see cref="AttemptHoldsOptions.Timeout"/>.</summary>
    public required AttemptTimeout Timeout { get; init; }

    /// <summary>The attempt's signal; see <see cref="AttemptHoldsOptions.Signal"/>.</summary>
    public required CancellationToken Signal { get; init; }

    /// <summary>The attempt as every step held in it names it.</summary>
    public required AttemptHoldTarget Attempt { get; init; }

    /// <summary>Callback to decide whether the step may run. The screen is available while waiting.</summary>
    public required Func<StepHold, CancellationToken, Func<Task<string?>>, Task> Ask { get; init; }

    /// <summary>Reads the screen a hold starts on.</summary>
    public required Func<CancellationToken, Task<string?>> ReadScreen { get; init; }
}

/// <summary>The part of a <see cref="StepHold"/> the attempt supplies: <c>Pick&lt;StepHold, 'target' | 'testId' | 'agent' | 'repeat' | 'title'&gt;</c>.</summary>
internal sealed record AttemptHoldTarget(string Target, string TestId, string Agent, int Repeat, string Title);

/// <summary>Holds of one test attempt, and the screen read the held steps share (upstream's <c>holdAttemptSteps</c>).</summary>
internal static class StepHolds
{
    /// <summary>The holds of one test attempt: each hold asks the runner, which can read the screen the hold starts on. Upstream's <c>holdAttemptSteps</c>.</summary>
    public static AttemptHolds HoldAttemptSteps(AttemptStepHolds options)
    {
        var gate = new Lock();

        // Steps held together (one Task.WhenAll) share a screen read.
        Task<string?>? heldScreen = null;
        return new AttemptHolds(
            new AttemptHoldsOptions
            {
                Timeout = options.Timeout,
                Signal = options.Signal,
                Ask = async (step, signal) =>
                {
                    var hold = new StepHold
                    {
                        Kind = step.Kind,
                        Number = step.Number,
                        Api = step.Api,
                        Label = step.Label,
                        Source = step.Source,
                        Target = options.Attempt.Target,
                        TestId = options.Attempt.TestId,
                        Agent = options.Attempt.Agent,
                        Repeat = options.Attempt.Repeat,
                        Title = options.Attempt.Title,
                    };
                    await options
                        .Ask(hold, signal, () =>
                        {
                            lock (gate)
                            {
                                if (heldScreen is null)
                                {
                                    Task<string?>? request = null;
                                    request = ReadOnceAsync();
                                    heldScreen = request;

                                    async Task<string?> ReadOnceAsync()
                                    {
                                        // The request is stored before its result is looked at, as a promise's continuation is.
                                        await Task.Yield();
                                        var screen = await options.ReadScreen(signal).ConfigureAwait(false);
                                        lock (gate)
                                        {
                                            if (screen is null && ReferenceEquals(heldScreen, request))
                                            {
                                                heldScreen = null;
                                            }
                                        }

                                        return screen;
                                    }
                                }

                                return heldScreen;
                            }
                        })
                        .ConfigureAwait(false);
                },

                // The clock runs again and tells the watchdog itself; the next hold reads a fresh screen.
                OnResume = () =>
                {
                    lock (gate)
                    {
                        heldScreen = null;
                    }
                },
            }
        );
    }

    /// <summary>
    /// The screen a held step waits on, as the trace page lists one: its location, then a line per node, redacted with the session's ledger. Read
    /// while the test clock is stopped; a screen that cannot be read (no app open yet, an engine without a tree) is null and never fails the test.
    /// Upstream's <c>readHeldScreen</c>.
    /// </summary>
    public static async Task<string?> ReadHeldScreenAsync(
        IEngineSession session,
        OperationContext operation,
        TraceScreenOptions options
    )
    {
        try
        {
            var screen = StepScreens.TraceScreen(
                await session.ObserveAsync(operation, operation.Signal).ConfigureAwait(false),
                options
            );
            return string.Join(
                '\n',
                (screen.Location is null ? Enumerable.Empty<string>() : [screen.Location]).Concat(
                    screen.Lines.Select(line => line.Text)
                )
            );
        }
        catch (OperationCanceledException) when (operation.Signal.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
