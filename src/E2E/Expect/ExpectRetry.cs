// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Internal;

namespace E2E;

/// <summary>Retries for <c>expect.poll</c> and <c>expect(fn).toPass()</c> (upstream's <c>expect/retry.ts</c>).</summary>
internal static class ExpectRetry
{
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
        var started = clock.GetUtcNow();
        var deadline = Deadline(options, started, cancellationToken);
        string? last = null;
        Exception? cause = null;
        var index = 0;
        TimeSpan? interval = null;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = deadline - clock.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            Sample sample;
            using (var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var tryStarted = clock.GetUtcNow();
                try
                {
                    // The block's own TimeoutException is a failed try; only the wait below ends the retry at the deadline (upstream's READ_HUNG).
                    sample = await PollScopes
                        .PollReadAsync(() => TryAsync(read, attempt.Token, cancellationToken))
                        .WaitAsync(remaining > Time.Unbounded ? Time.Unbounded : remaining, clock, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
            var left = deadline - clock.GetUtcNow();
            if (left > TimeSpan.Zero)
            {
                await Pause(left < pause ? left : pause, clock, cancellationToken).ConfigureAwait(false);
            }

            if (pause >= left)
            {
                // The next pause would cross the deadline, so the retry ends without another read.
                break;
            }
        }

        var lines = new List<string>
        {
            options.Label
                + " timed out after "
                + ((long)options.Timeout.TotalMilliseconds).ToString(CultureInfo.InvariantCulture)
                + " ms",
        };
        if (options.Message is not null)
        {
            lines.Add(options.Message);
        }

        lines.Add("last: " + (last ?? options.NoSample));
        var text = ErrorRedaction.Redact(string.Join('\n', lines));
        throw cause is null
            ? new TestException("ASSERTION_FAILED", text)
            : new TestException("ASSERTION_FAILED", text, cause);
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

    /// <summary>
    /// The retry's own deadline. A <c>Timeout</c> of zero sets none: the port's phase is the caller's <paramref name="cancellationToken"/>, so it
    /// needs one that can end, or nothing would bound the retry (upstream's <c>INVALID_CONFIG</c> outside a test or hook).
    /// </summary>
    private static DateTimeOffset Deadline(
        RetryOptions options,
        DateTimeOffset started,
        CancellationToken cancellationToken
    )
    {
        if (options.Timeout != TimeSpan.Zero)
        {
            return started + options.Timeout;
        }

        if (!cancellationToken.CanBeCanceled)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                options.Api
                    + " timeout 0 waits for the test deadline, so it needs a cancellation token that ends with the test; pass a positive timeout or the test's token"
            );
        }

        return Time.Never;
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
