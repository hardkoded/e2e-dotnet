// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Internal;

namespace E2E;

/// <summary>Retries for <c>expect.poll</c> and <c>expect(fn).toPass()</c> (upstream's <c>expect/retry.ts</c>).</summary>
internal static class ExpectRetry
{
    /// <summary>Time for the phase timeout to take precedence if the retry reaches the deadline first (upstream's <c>PHASE_TIMEOUT_GRACE_MS</c>).</summary>
    private static readonly TimeSpan PhaseTimeoutGrace = TimeSpan.FromMilliseconds(50);

    /// <summary>The pauses between reads or tries when none are given: upstream's <c>DEFAULT_PASS_OPTIONS.intervals</c>.</summary>
    internal static readonly IReadOnlyList<TimeSpan> DefaultIntervals =
        new System.Collections.ObjectModel.ReadOnlyCollection<TimeSpan>([
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(250),
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromMilliseconds(1000),
        ]);

    /// <summary>Validates pauses before the first read or try (upstream's <c>checkIntervals</c>).</summary>
    internal static void CheckIntervals(string label, IReadOnlyList<TimeSpan>? intervals)
    {
        if (intervals is null)
        {
            return;
        }

        if (intervals.Count == 0)
        {
            throw new ConfigurationException("INVALID_CONFIG", label + " must be a non-empty array of milliseconds");
        }

        for (var index = 0; index < intervals.Count; index++)
        {
            var ms = intervals[index].TotalMilliseconds;
            if (!(ms > 0) || ms != Math.Floor(ms) || ms > 9007199254740991d)
            {
                throw new ConfigurationException(
                    "INVALID_CONFIG",
                    label
                        + "["
                        + index.ToString(CultureInfo.InvariantCulture)
                        + "] must be a positive safe integer of milliseconds, got "
                        + ms.ToString(CultureInfo.InvariantCulture)
                );
            }
        }
    }

    /// <summary>Validates a call's <c>timeout</c> (the timeout half of upstream's <c>checkOptions</c>).</summary>
    internal static void CheckOptions(string api, TimeSpan? timeout)
    {
        if (timeout is not { } value)
        {
            return;
        }

        if (value < TimeSpan.Zero)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                api + " timeout must be a finite number of milliseconds, 0 or more; got " + Milliseconds(value)
            );
        }

        if (value.TotalMilliseconds > Time.MaxTimerMs)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                api
                    + " timeout must be at most "
                    + Time.MaxTimerMs.ToString(CultureInfo.InvariantCulture)
                    + " milliseconds, got "
                    + Milliseconds(value)
            );
        }
    }

    internal static string Milliseconds(TimeSpan value) =>
        value == Timeout.InfiniteTimeSpan ? "Infinity" : value.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Runs <paramref name="read"/> until <paramref name="evaluate"/> accepts its result or the deadline passes (upstream's <c>retry</c>; named apart from the test-retry class in <c>Run</c>).
    /// Each try gets a token that ends when the try fails or is cut at the deadline, so steps it left running stop.
    /// </summary>
    internal static Task<object?> RetryAsync(
        Func<CancellationToken, Task<object?>> read,
        Func<Sample, Outcome> evaluate,
        RetryOptions options,
        CancellationToken cancellationToken
    )
    {
        // Tracked in the phase that started it, so one the phase did not await ends with the phase (upstream's `scope.track`).
        var scope = PollScopes.CurrentPollScope() ?? Attempts.CurrentAttempt()?.Polls;
        if (scope is null)
        {
            return RunAsync(read, evaluate, options, cancellationToken);
        }

        var stack = new System.Diagnostics.StackTrace(1, fNeedFileInfo: true);
        return scope.Track(options.Label, stack, async scopeToken =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, scopeToken);
            return await RunAsync(read, evaluate, options, linked.Token).ConfigureAwait(false);
        });
    }

    private static async Task<object?> RunAsync(
        Func<CancellationToken, Task<object?>> read,
        Func<Sample, Outcome> evaluate,
        RetryOptions options,
        CancellationToken cancellationToken
    )
    {
        var clock = options.Clock;
        var context = Attempts.CurrentAttempt();
        var budget = context?.Budget;
        var started = clock.GetUtcNow();

        // Inside an attempt the poll ends with the phase it runs in (the test deadline in the body, the hook's cleanup budget in a teardown) and stops the
        // moment the attempt is cancelled, so a timed-out test never keeps reading through teardown. A timeout of 0 sets no deadline of its own.
        var phaseToken = budget?.Token ?? default;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, phaseToken);
        var token = linked.Token;
        DateTimeOffset? own = options.Timeout == TimeSpan.Zero ? null : started + options.Timeout;
        if (own is null && budget?.Deadline is null && !cancellationToken.CanBeCanceled)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                options.Api
                    + " timeout 0 waits for the test deadline, so it needs a cancellation token that ends with the test; pass a positive timeout or the test's token"
            );
        }

        // The poll's own deadline capped by its phase's, read at call time because test.setTimeout moves the phase's.
        DateTimeOffset Current() =>
            (own, budget?.Deadline?.EndsAt) switch
            {
                ({ } mine, { } phase) => phase < mine ? phase : mine,
                ({ } mine, null) => mine,
                (null, { } phase) => phase,
                _ => Time.Never,
            };
        bool EndsAtOwn() => own is { } mine && (budget?.Deadline is not { } phase || mine <= phase.EndsAt);

        string? last = null;
        Exception? cause = null;
        DateTimeOffset? endedAt = null;
        bool? endedAtOwn = null;
        TestException AssertionFailure(bool atOwn, DateTimeOffset ended)
        {
            var lines = new List<string>
            {
                options.Label
                    + (
                        atOwn
                            ? " timed out after "
                                + ((long)options.Timeout.TotalMilliseconds).ToString(CultureInfo.InvariantCulture)
                                + " ms"
                            : " stopped at the attempt deadline after "
                                + ((long)(ended - started).TotalMilliseconds).ToString(CultureInfo.InvariantCulture)
                                + " ms"
                    ),
            };
            if (options.Message is not null)
            {
                lines.Add(options.Message);
            }

            lines.Add("last: " + (last ?? options.NoSample));
            var text = ErrorRedaction.Redact(string.Join('\n', lines));
            // Upstream redacts the kept cause when serializeError walks it; the port has no report boundary, so it is redacted here.
            return ErrorRedaction.Cause(cause, ErrorRedaction.Redact) is { } redactedCause
                ? new TestException("ASSERTION_FAILED", text, redactedCause)
                : new TestException("ASSERTION_FAILED", text);
        }

        try
        {
            var index = 0;
            TimeSpan? interval = null;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var remaining = Current() - clock.GetUtcNow();
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                Sample sample;
                using (var attempt = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    var tryStarted = clock.GetUtcNow();
                    try
                    {
                        // The block's own TimeoutException is a failed try; only the wait below ends the retry at the deadline (upstream's READ_HUNG).
                        sample = await PollScopes
                            .PollReadAsync(() => TryAsync(read, attempt.Token, token))
                            .WaitAsync(remaining > Time.Unbounded ? Time.Unbounded : remaining, clock, token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (TimeoutException)
                    {
                        // The try outlived the deadline; its outcome is dropped and what it was running is cancelled.
                        await attempt.CancelAsync().ConfigureAwait(false);
                        break;
                    }

                    if (sample.Error is not null)
                    {
                        await attempt.CancelAsync().ConfigureAwait(false);
                    }

                    // A try counts only if it settles within the time that remained when it started.
                    if (clock.GetUtcNow() - tryStarted > remaining)
                    {
                        if (sample.Error is { } late)
                        {
                            (last, cause) = (late.Message, late);
                        }

                        break;
                    }
                }

                var outcome = evaluate(sample);
                if (outcome.Passed)
                {
                    return outcome.Value;
                }

                (last, cause) = (outcome.Last, outcome.Cause);
                if (index < options.Intervals.Count)
                {
                    interval = options.Intervals[index++];
                }

                var pause = interval ?? TimeSpan.Zero;
                var left = Current() - clock.GetUtcNow();
                if (left > TimeSpan.Zero)
                {
                    await Pause(left < pause ? left : pause, clock, token).ConfigureAwait(false);
                }

                if (pause >= left)
                {
                    // The next pause would cross the deadline, so the retry ends without another read.
                    break;
                }
            }

            (endedAt, endedAtOwn) = (clock.GetUtcNow(), EndsAtOwn());
            if (endedAtOwn is false)
            {
                // The phase's own timeout takes precedence if it fires within the grace.
                await Pause(PhaseTimeoutGrace, clock, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested && phaseToken.IsCancellationRequested)
        {
            // The phase ended the poll (its timeout fired, which the timer may do just ahead of the clock that reads the deadline): a poll its deadline cut tells its last failed assertion beside the phase's error.
            var now = clock.GetUtcNow();
            if (!EndsAtOwn() && (Current() <= now || budget?.Reason?.Invoke() is not null) && last is not null)
            {
                context?.RecordSecondaryError?.Invoke(AssertionFailure(false, endedAt ?? now));
            }

            throw budget?.AbortReason() ?? new OperationCanceledException(phaseToken);
        }

        throw AssertionFailure(endedAtOwn ?? true, endedAt ?? clock.GetUtcNow());
    }

    private static async Task<Sample> TryAsync(
        Func<CancellationToken, Task<object?>> read,
        CancellationToken token,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return new Sample(await read(token).ConfigureAwait(false), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SkipException)
        {
            // A skip ends the retry and propagates unchanged.
            throw;
        }
        catch (Exception ex)
        {
            return new Sample(null, ex);
        }
    }

    private static Task Pause(TimeSpan pause, TimeProvider clock, CancellationToken cancellationToken) =>
        // Rounded up to a whole millisecond: a delay under one completes at once, and the loop would spin to the deadline.
        Task.Delay(TimeSpan.FromMilliseconds(Math.Ceiling(pause.TotalMilliseconds)), clock, cancellationToken);

    /// <summary>One read or try: its value, or the error it threw.</summary>
    internal readonly record struct Sample(object? Value, Exception? Error);

    /// <summary>What a matcher made of a sample: a pass, or the line the timeout error ends with.</summary>
    internal readonly record struct Outcome(bool Passed, object? Value, string? Last, Exception? Cause)
    {
        public static Outcome Pass(object? value = null) => new(true, value, null, null);

        public static Outcome Fail(string last, Exception? cause = null) => new(false, null, last, cause);
    }
}

/// <summary>The settings of one retry (upstream's <c>RetryOptions</c>).</summary>
internal sealed class RetryOptions
{
    public required string Api { get; init; }

    public required string Label { get; init; }

    public required TimeSpan Timeout { get; init; }

    public required IReadOnlyList<TimeSpan> Intervals { get; init; }

    public string? Message { get; init; }

    public required string NoSample { get; init; }

    public TimeProvider Clock { get; init; } = TimeProvider.System;
}
