// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Internal;

namespace E2E;

/// <summary>How <see cref="Expect.Poll{T}(Func{CancellationToken, Task{T}}, PollOptions?)"/> re-reads a value.</summary>
public sealed class PollOptions
{
    /// <summary>
    /// Deadline for the matcher to hold. Inside a test or a hosted session the default is the config's <c>assertionTimeout</c>, elsewhere 5 seconds,
    /// and the deadline of the session or test the poll runs in caps it. Zero sets no deadline of its own: the poll runs until that deadline or the cancellation token it is
    /// given ends, so it needs one that can end (<c>INVALID_CONFIG</c> otherwise). A negative value is <c>INVALID_CONFIG</c>.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Pause between reads. Zero or a negative value is <c>INVALID_CONFIG</c>, and so is passing it with <see cref="Intervals"/>.</summary>
    [Obsolete("Pass Intervals with one pause.")]
    public TimeSpan? Interval { get; init; }

    /// <summary>
    /// Pauses between reads. The default is 100, 250, 500 and 1000 milliseconds, and the last pause repeats. Each is a positive whole number of
    /// milliseconds, and the list is not empty (<c>INVALID_CONFIG</c> otherwise).
    /// </summary>
    public IReadOnlyList<TimeSpan>? Intervals { get; init; }

    /// <summary>Extra line in the timeout error.</summary>
    public string? Message { get; init; }

    internal TimeProvider Clock { get; init; } = TimeProvider.System;
}

/// <summary>
/// Re-reads a value until the chosen matcher holds or the timeout passes. A
/// read that throws is retried, and its error is reported if the poll times
/// out. A matcher that refuses the read value's type counts as not yet. With NUnit,
/// <c>ToMatchAsync</c> from <c>E2E.NUnit</c> takes any NUnit constraint.
/// </summary>
public sealed class PollExpectation<T>
{
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private static int s_warnedInterval;

    /// <summary>Lets the next use of the deprecated <c>Interval</c> warn again (a test fixture; upstream's warning flag is a process symbol).</summary>
    internal static void ResetIntervalWarning() => Interlocked.Exchange(ref s_warnedInterval, 0);

    private readonly Func<CancellationToken, Task<T>> _read;
    private readonly PollOptions _options;
    private readonly bool _negated;

    internal PollExpectation(Func<CancellationToken, Task<T>> read, PollOptions options, bool negated)
    {
        _read = read;
        _options = options;
        _negated = negated;
    }

    /// <summary>The same matchers, negated.</summary>
    public PollExpectation<T> Not => new(_read, _options, !_negated);

    /// <summary>Passes when the read value equals <paramref name="expected"/> by <see cref="EqualityComparer{T}.Default"/>.</summary>
    public Task ToBeAsync(T expected, CancellationToken cancellationToken = default)
    {
        return PollAsync(
            "toBe",
            value => EqualityComparer<T>.Default.Equals(value, expected),
            value => "received " + Format(value) + ", expected " + (_negated ? "not " : "") + Format(expected),
            cancellationToken
        );
    }

    /// <summary>Passes when <paramref name="predicate"/> returns true for the read value. <paramref name="description"/> names the condition in the error.</summary>
    public Task ToSatisfyAsync(
        Func<T, bool> predicate,
        string? description = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var condition = string.IsNullOrWhiteSpace(description) ? "the predicate" : description;
        return PollAsync(
            "toSatisfy",
            predicate,
            value => "received " + Format(value) + ", which does " + (_negated ? "" : "not ") + "satisfy " + condition,
            cancellationToken
        );
    }

    private async Task PollAsync(
        string matcher,
        Func<T, bool> check,
        Func<T, string> describe,
        CancellationToken cancellationToken
    )
    {
        await ExpectRetry
            .RetryAsync(
                async token => await _read(token).ConfigureAwait(false),
                sample =>
                {
                    if (sample.Error is { } error)
                    {
                        return ExpectRetry.Outcome.Fail(error.Message);
                    }

                    var value = (T)sample.Value!;
                    bool holds;
                    try
                    {
                        holds = check(value);
                    }
                    catch (ArgumentException ex)
                    {
                        // The matcher refused this value's type, as a sample that does not hold yet.
                        return ExpectRetry.Outcome.Fail(ex.Message);
                    }

                    return holds != _negated ? ExpectRetry.Outcome.Pass() : ExpectRetry.Outcome.Fail(describe(value));
                },
                new RetryOptions
                {
                    Api = "expect.poll",
                    Label = "expect.poll(...)." + (_negated ? "not." : "") + matcher + "(...)",
                    Timeout = _options.Timeout ?? Attempts.CurrentAttempt()?.AssertionTimeout ?? DefaultTimeout,
                    Intervals = Intervals(_options),
                    Message = _options.Message,
                    NoSample = "no read completed",
                    Clock = _options.Clock,
                },
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static IReadOnlyList<TimeSpan> Intervals(PollOptions options)
    {
#pragma warning disable CS0618 // The deprecated alias of Intervals.
        return options.Intervals ?? (options.Interval is { } interval ? [interval] : ExpectRetry.DefaultIntervals);
#pragma warning restore CS0618
    }

    private static string Format(T value)
    {
        return value switch
        {
            null => "null",
            string text => "\"" + text + "\"",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };
    }

    /// <summary>A malformed option would poll forever; it is refused before the first read (upstream's <c>checkOptions</c>).</summary>
    internal static void CheckOptions(PollOptions options)
    {
        ExpectRetry.CheckOptions("expect.poll", options.Timeout);
#pragma warning disable CS0618 // The deprecated alias of Intervals.
        if (options.Interval is { } interval)
        {
            if (options.Intervals is not null)
            {
                throw new ConfigurationException(
                    "INVALID_CONFIG",
                    "expect.poll cannot take both interval and intervals"
                );
            }

            if (interval <= TimeSpan.Zero)
            {
                throw new ConfigurationException(
                    "INVALID_CONFIG",
                    "expect.poll interval must be a finite number of milliseconds above 0; got "
                        + interval.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)
                );
            }

            if (Interlocked.Exchange(ref s_warnedInterval, 1) == 0)
            {
                Console.Error.WriteLine(
                    "e2e: [warning] expect.poll interval is deprecated. Pass Intervals with one pause."
                );
            }
        }
#pragma warning restore CS0618

        ExpectRetry.CheckIntervals("expect.poll intervals", options.Intervals);
    }
}
