// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Internal;

/// <summary>A point in time a wait must end by, as upstream's <c>Deadline</c>.</summary>
internal sealed class Deadline(DateTimeOffset endsAt)
{
    public Deadline(TimeSpan timeout, TimeProvider clock)
        : this(clock.GetUtcNow() + timeout)
    {
    }

    public DateTimeOffset EndsAt { get; } = endsAt;

    /// <summary>Remaining budget, never negative.</summary>
    public TimeSpan Remaining(DateTimeOffset now) => now >= EndsAt ? TimeSpan.Zero : EndsAt - now;

    public bool Expired(DateTimeOffset now) => now >= EndsAt;
}

/// <summary>
/// The reads of one wait, as upstream's <c>ReadWindow</c>. Each engine read runs until the wait's deadline plus the longest read the
/// wait has finished, capped by <c>cap</c> (the running phase's deadline), so a slow last read still finishes. Reads past the deadline
/// share that extra time instead of each getting it again. Without a cap, reads end at the deadline.
/// </summary>
internal sealed class ReadWindow
{
    private readonly TimeProvider _clock;
    private readonly Deadline _cap;
    private TimeSpan _slowest = TimeSpan.Zero;
    private TimeSpan _lastStartedWith = TimeSpan.MaxValue;

    public ReadWindow(Deadline deadline, TimeProvider clock, Deadline? cap = null)
    {
        Deadline = deadline;
        _clock = clock;
        _cap = cap ?? deadline;
    }

    public Deadline Deadline { get; }

    /// <summary>The budget for an engine read starting now, never under 1 ms (upstream's <c>timeoutMs</c>).</summary>
    public TimeSpan Timeout()
    {
        var now = _clock.GetUtcNow();
        var left = Deadline.EndsAt + _slowest - now;
        var capped = _cap.Remaining(now);
        var budget = capped < left ? capped : left;
        var least = TimeSpan.FromMilliseconds(1);
        return budget > least ? budget : least;
    }

    /// <summary>Marks a read starting now: the one <see cref="CutOff"/> judges until the next starts.</summary>
    public void Starting(DateTimeOffset? now = null) => _lastStartedWith = Deadline.Remaining(now ?? _clock.GetUtcNow());

    /// <summary>Runs one engine read on <see cref="Timeout"/>, timing it when it answers.</summary>
    public async Task<T> ReadAsync<T>(Func<TimeSpan, Task<T>> call)
    {
        ArgumentNullException.ThrowIfNull(call);
        var startedAt = _clock.GetUtcNow();
        Starting(startedAt);
        var value = await call(Timeout()).ConfigureAwait(false);
        var took = _clock.GetUtcNow() - startedAt;
        if (took > _slowest)
        {
            _slowest = took;
        }

        return value;
    }

    /// <summary>
    /// Whether <paramref name="cause"/> is a read the deadline cut off: an operation timeout of this window's latest read when it
    /// started with less than one poll tick, or the longest read this wait finished, of the deadline left, so it timed out because the
    /// wait did, not because the app stopped answering. A read that started with more and still timed out hung, and its timeout is the
    /// failure. Reads the engine error a locator failure wraps as well as a bare one.
    /// </summary>
    public bool CutOff(Exception cause) =>
        _lastStartedWith < (Time.PollInterval > _slowest ? Time.PollInterval : _slowest) && Time.IsOperationTimeout(cause);
}

/// <summary>What <see cref="Time.PollReadsAsync"/> polls, and how it ends.</summary>
internal sealed class PollReadsOptions
{
    public required Deadline Deadline { get; init; }

    /// <summary>The window every read of the poll runs through.</summary>
    public required ReadWindow Reads { get; init; }

    public required TimeProvider Clock { get; init; }

    public required CancellationToken Token { get; init; }

    public required bool Negated { get; init; }

    /// <summary>The longest pause between reads.</summary>
    public TimeSpan Interval { get; init; } = Time.PollInterval;

    /// <summary>
    /// Evaluates the positive condition once, every read run through the window. Returns null when the condition cannot be evaluated
    /// yet: the positive poll keeps waiting and the negation grace window resets.
    /// </summary>
    public required Func<ReadWindow, Task<bool?>> Evaluate { get; init; }

    /// <summary>Builds the poll's failure; the argument is the read the deadline cut off, when one did.</summary>
    public required Func<Exception?, Exception> OnTimeout { get; init; }
}

/// <summary>What <see cref="Time.PollConditionAsync"/> polls, and how it ends.</summary>
internal sealed class PollConditionOptions
{
    public required Deadline Deadline { get; init; }

    public required TimeProvider Clock { get; init; }

    public required CancellationToken Token { get; init; }

    public required bool Negated { get; init; }

    /// <summary>The longest pause between reads.</summary>
    public TimeSpan Interval { get; init; } = Time.PollInterval;

    /// <summary>
    /// Evaluates the positive condition once, every read bounded by the deadline: a read on a longer budget outlasts
    /// the poll. Returns null when the condition cannot be evaluated yet: the positive poll keeps waiting and the
    /// negation grace window resets.
    /// </summary>
    public required Func<Task<bool?>> Evaluate { get; init; }

    /// <summary>Builds the poll's failure; the argument is the read the deadline cut off, when one did.</summary>
    public required Func<Exception?, Exception> OnTimeout { get; init; }
}

/// <summary>Deadline and polling helpers, as upstream's <c>internal/time.ts</c>.</summary>
internal static class Time
{
    /// <summary>A budget as a message names it, exactly, in the largest unit that divides it: <c>5m</c>, <c>30s</c>, <c>1500ms</c> (upstream's <c>budgetText</c>).</summary>
    public static string BudgetText(long ms)
    {
        if (ms > 0 && ms % 60_000 == 0)
        {
            return $"{ms / 60_000}m";
        }

        return ms > 0 && ms % 1000 == 0 ? $"{ms / 1000}s" : $"{ms}ms";
    }

    /// <summary>
    /// The token a call runs under: the caller's token, ended also by the session's. No source is made when one token
    /// already stands for both, so a call does not leave a registration on the session token. When both can cancel
    /// and differ, the linked source lives until either token ends.
    /// </summary>
    public static CancellationToken Link(CancellationToken caller, CancellationToken session)
    {
        if (caller == session || !caller.CanBeCanceled)
        {
            return session;
        }

        return session.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(caller, session).Token : caller;
    }

    /// <summary>Canonical polling cadence for locator and assertion loops.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>How long a negated assertion must hold before it passes.</summary>
    public static readonly TimeSpan NegationGrace = TimeSpan.FromMilliseconds(1000);

    /// <summary>Whether <paramref name="cause"/> is the engine's <c>OPERATION_TIMEOUT</c>, bare or wrapped by the locator failure it was translated into (upstream's <c>isOperationTimeout</c>).</summary>
    public static bool IsOperationTimeout(Exception cause) =>
        (cause as EngineException ?? cause.InnerException as EngineException) is { Code: EngineErrorCodes.OperationTimeout };

    /// <summary>
    /// Polls a condition until it holds (or, when negated, until its negation has held continuously for the negation
    /// grace window), throwing the caller's error at the deadline. No sleep runs past the deadline, so the last read
    /// starts at it and never sees a state that arrived later.
    /// A negation holds from the moment the read that first saw it was issued, so a slow read counts toward the
    /// window. A budget shorter than the window still has to be satisfiable: the negation then only needs to hold for
    /// the whole budget, and passes at the deadline on what it has seen, since a read past the deadline has no budget
    /// left. A read the deadline cut off (see <see cref="ReadWindow.CutOff"/>) after an earlier one completed saw
    /// nothing: the poll ends there, and a negation passes if it had held long enough by then.
    /// </summary>
    public static Task PollConditionAsync(PollConditionOptions options) =>
        PollReadsAsync(new PollReadsOptions
        {
            Deadline = options.Deadline,
            Reads = new ReadWindow(options.Deadline, options.Clock),
            Clock = options.Clock,
            Token = options.Token,
            Negated = options.Negated,
            Interval = options.Interval,
            Evaluate = _ => options.Evaluate(),
            OnTimeout = options.OnTimeout,
        });

    /// <summary>
    /// <see cref="PollConditionAsync"/> with its reads run through <c>options.Reads</c>: a window with a cap lets the last read run past
    /// the deadline by the longest read the poll has finished, and a read cut off (see <see cref="ReadWindow.CutOff"/>) is one that
    /// started with under one poll tick, or that longest read, left.
    /// </summary>
    public static async Task PollReadsAsync(PollReadsOptions options)
    {
        var (deadline, reads, clock, token, negated) = (options.Deadline, options.Reads, options.Clock, options.Token, options.Negated);
        var startedAt = clock.GetUtcNow();
        var grace = deadline.Remaining(startedAt) < NegationGrace ? deadline.Remaining(startedAt) : NegationGrace;
        var readAt = startedAt;
        DateTimeOffset? holdingSince = null;
        var sampled = false;
        bool Holds(DateTimeOffset now) => holdingSince is { } since && now - since >= grace;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            bool? value;
            reads.Starting(readAt);
            try
            {
                value = await options.Evaluate(reads).ConfigureAwait(false);
                sampled = true;
            }
            catch (Exception ex) when (sampled && reads.CutOff(ex))
            {
                var now = clock.GetUtcNow();
                if (negated && Holds(now < deadline.EndsAt ? now : deadline.EndsAt))
                {
                    return;
                }

                throw options.OnTimeout(ex);
            }

            if (!negated)
            {
                if (value == true)
                {
                    return;
                }
            }
            else
            {
                // False from the first sample means false for the whole budget so far, so the clock starts with the budget.
                holdingSince = value == false ? holdingSince ?? readAt : null;
            }

            var current = clock.GetUtcNow();
            if (Holds(current < deadline.EndsAt ? current : deadline.EndsAt))
            {
                return;
            }

            if (deadline.Expired(current))
            {
                throw options.OnTimeout(null);
            }

            // Rounded up to whole milliseconds, which is what Task.Delay waits, so the capped pause never wakes before the deadline.
            var remaining = TimeSpan.FromMilliseconds(Math.Ceiling(deadline.Remaining(current).TotalMilliseconds));
            await Task.Delay(remaining < options.Interval ? remaining : options.Interval, clock, token).ConfigureAwait(false);

            // A read past the deadline has no budget left, so a negation decides at the deadline on what it has seen.
            readAt = clock.GetUtcNow();
            if (negated && deadline.Expired(readAt))
            {
                if (Holds(readAt))
                {
                    return;
                }

                throw options.OnTimeout(null);
            }
        }
    }
}
