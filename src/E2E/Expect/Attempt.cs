// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// The running attempt, published for code that has no session to hang off, such as <see cref="Launch"/>. Upstream's <c>PublishedAttempt</c>
/// (<c>expect/attempt.ts</c>); the port keeps the part a host and its sessions read, since it has no <c>expect.poll</c> scope or soft-failure list to publish. The budget it publishes is the running phase's signal and deadline.
/// </summary>
internal sealed record PublishedAttempt
{
    /// <summary>The attempt's id.</summary>
    public required string AttemptId { get; init; }

    /// <summary>
    /// Takes in the other sessions the test drives beside its own, the ones a <see cref="Launch"/> host opens: their steps are recorded with the
    /// attempt's own. A test's attempt offers it; a hosted one (<see cref="E2EHost"/>, <c>e2e mcp</c>) does not yet, so a session used there tells nobody.
    /// </summary>
    public IAttemptGuests? Guests { get; init; }

    /// <summary>
    /// Moves this attempt's timeout (upstream's <c>setTimeout</c>); false once the attempt ended or its timeout fired. Null where the attempt has
    /// no timeout to move, as in a hosted session.
    /// </summary>
    public Func<TimeSpan, bool>? SetTimeout { get; init; }

    /// <summary>The config default for a poll's <c>Timeout</c> (upstream's <c>assertionTimeout</c>). Null where the attempt names none.</summary>
    public TimeSpan? AssertionTimeout { get; init; }

    /// <summary>Callback for the last failed assertion when the phase timeout is the primary error (upstream's <c>recordSecondaryError</c>).</summary>
    public Action<Exception>? RecordSecondaryError { get; init; }

    /// <summary>Read at call time: the running phase's signal and deadline (upstream's <c>budget</c>). Null where the attempt has none to publish.</summary>
    public AttemptBudget? Budget { get; init; }

    /// <summary>The config's <c>toPass</c> defaults, which <see cref="ValueExpectation{T}.ToPassAsync(PassOptions?, CancellationToken)"/> falls back to (upstream's <c>toPass</c>).</summary>
    public PassOptions? ToPass { get; init; }
}

/// <summary>
/// Which attempt is running. Upstream's <c>publishAttempt</c> and <c>currentAttempt</c> (<c>expect/attempt.ts</c>). Upstream keeps one slot per
/// process, because one attempt runs at a time there and a second live attempt is refused. A .NET test framework may run tests side by side, and a
/// program may launch a host in the middle of them, so the port names the attempt through the async flow of the test instead (an
/// <see cref="AsyncLocal{T}"/>): a host launched in the flow of a test is that test's, and one launched anywhere else is no test's. A
/// framework adapter reserves the slot in its own flow before it starts the session (<see cref="Reserve"/>), and the session fills it when it is up.
/// </summary>
internal static class Attempts
{
    private static readonly AsyncLocal<Slot?> Current = new();

    /// <summary>
    /// Reserves the slot of the calling flow, for the attempt a session about to start publishes into. Call it from a method that is not async, so
    /// the slot belongs to the flow of the test body that follows.
    /// </summary>
    public static void Reserve() => Current.Value = new Slot();

    /// <summary>Names the attempt in the flow that reserved the slot, until the returned scope is disposed, or until <paramref name="until"/> is cancelled.</summary>
    public static IDisposable PublishAttempt(PublishedAttempt context, CancellationToken until = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (until.IsCancellationRequested)
        {
            return new Published(null, context);
        }

        // A caller that did not reserve a slot publishes into its own flow, as far as it reaches.
        var slot = Current.Value ?? (Current.Value = new Slot());
        slot.Attempt = context;
        var published = new Published(slot, context);
        if (until.CanBeCanceled)
        {
            published.Watch(until);
        }

        return published;
    }

    /// <summary>The attempt a host launched now runs inside, or null outside every test.</summary>
    public static PublishedAttempt? CurrentAttempt() => Current.Value?.Attempt;

    private sealed class Slot
    {
        public volatile PublishedAttempt? Attempt;
    }

    private sealed class Published(Slot? slot, PublishedAttempt context) : IDisposable
    {
        private CancellationTokenRegistration _registration;
        private int _done;

        public void Watch(CancellationToken until) => _registration = until.Register(Dispose);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) != 0)
            {
                return;
            }

            _registration.Dispose();

            // A stale clear never removes a newer attempt.
            if (slot is not null && ReferenceEquals(slot.Attempt, context))
            {
                slot.Attempt = null;
            }
        }
    }
}
