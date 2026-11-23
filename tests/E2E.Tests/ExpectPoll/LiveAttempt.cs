// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.ExpectPoll;

/// <summary>
/// A published attempt with a running phase (upstream's <c>attempt()</c> helper in <c>expect-poll.test.ts</c>): an assertion timeout, a phase
/// deadline, and a way to cancel the phase. Create it in the method of the test, so the attempt belongs to the flow of that test.
/// </summary>
internal sealed class LiveAttempt : IDisposable
{
    private readonly CancellationTokenSource _cancel = new();
    private readonly IDisposable _published;

    public LiveAttempt(TimeSpan assertionTimeout, TimeSpan deadline)
    {
        Budget = new AttemptBudget(_cancel) { Deadline = new Deadline(deadline, TimeProvider.System) };
        _published = Attempts.PublishAttempt(
            new PublishedAttempt
            {
                AttemptId = "attempt",
                AssertionTimeout = assertionTimeout,
                Budget = Budget,
            }
        );
    }

    public AttemptBudget Budget { get; }

    /// <summary>Moves the phase deadline later by <paramref name="by"/> (upstream's <c>budget.shiftDeadline</c>).</summary>
    public void ShiftDeadline(TimeSpan by) => Budget.Deadline = new Deadline(Budget.Deadline!.EndsAt + by);

    public void Cancel() => _cancel.Cancel();

    public void Dispose()
    {
        _published.Dispose();
        Budget.Dispose();
    }
}
