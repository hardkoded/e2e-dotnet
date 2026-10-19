// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using E2E.Internal;

namespace E2E;

/// <summary>
/// What one assertion poll read, round by round, kept as the distinct values in the order they appeared (upstream's
/// <c>SampleHistory</c>, <c>expect/samples.ts</c>): whether the screen never showed what was expected, or showed it and moved on,
/// is what separates a wrong expectation from a race. <see cref="Event"/> turns the readings into the step's <c>poll</c> event.
/// </summary>
internal sealed class SampleHistory
{
    /// <summary>Characters of one reading the event keeps.</summary>
    private const int MaxValueChars = 80;

    /// <summary>Distinct readings the event names: the first, then the latest ones.</summary>
    private const int MaxRuns = 4;

    private readonly string _name;
    private readonly Func<string, string> _redact;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly List<Run> _runs = [];
    private string? _lastRaw;
    private bool _dropped;
    private int _reads;

    /// <param name="name">What polled, as the event names it (<c>expect</c>, <c>waitFor</c>).</param>
    /// <param name="redact">
    /// Replaces secret values in a reading before it is clipped here, so no cut leaves a secret's head behind. The step recorder
    /// redacts every event's detail again; that pass cannot see a value this clip already cut.
    /// </param>
    public SampleHistory(string name, Func<string, string> redact)
    {
        _name = name;
        _redact = redact;
    }

    /// <summary>Adds one round's reading.</summary>
    public void Add(string value)
    {
        _reads++;

        // The reading the last round took folds without being redacted again.
        if (_runs.Count > 0 && string.Equals(_lastRaw, value, StringComparison.Ordinal))
        {
            _runs[^1].Count++;
            return;
        }

        // Folded whole, so two long readings that differ past the clip stay two.
        _lastRaw = value;
        var redacted = _redact(value);
        if (_runs.Count > 0 && string.Equals(_runs[^1].Value, redacted, StringComparison.Ordinal))
        {
            _runs[^1].Count++;
            return;
        }

        _runs.Add(new Run(redacted, (long)Stopwatch.GetElapsedTime(_started).TotalMilliseconds));

        // Only the first reading and the latest ones are told, so a value that keeps changing keeps no more than those.
        if (_runs.Count > MaxRuns)
        {
            _runs.RemoveAt(1);
            _dropped = true;
        }
    }

    /// <summary>
    /// The poll as one <c>poll</c> event: how many rounds it read over how long and, when the reading changed or the poll failed,
    /// the readings in order, <c>"2 remaining" x3 -> at 410ms "1 remaining" x12</c>.
    /// </summary>
    public StepEvent Event(StepEventStatus status) => new()
    {
        Kind = StepEventKind.Poll,
        Name = _name,
        StartedAt = _startedAt,
        DurationMs = (long)Stopwatch.GetElapsedTime(_started).TotalMilliseconds,
        Status = status,
        Count = _reads,
        Detail = (_runs.Count > 1 || _dropped || status != StepEventStatus.Passed) && _runs.Count > 0 ? Detail() : null,
    };

    /// <summary>
    /// Runs one poll and records it as the running step's <c>poll</c> event once it settles: passed, failed, or cancelled when the
    /// run was cancelled meanwhile.
    /// </summary>
    public async Task RecordAsync(StepRecorder? steps, CancellationToken cancellationToken, Func<Task> poll)
    {
        var status = StepEventStatus.Failed;
        try
        {
            await poll().ConfigureAwait(false);
            status = StepEventStatus.Passed;
        }
        finally
        {
            steps?.RecordEvent(Event(status == StepEventStatus.Failed && cancellationToken.IsCancellationRequested ? StepEventStatus.Cancelled : status));
        }
    }

    private string Detail()
    {
        var shown = _runs;
        var parts = shown.Select((run, index) =>
        {
            var skipped = index == 1 && _dropped ? "… -> " : "";
            var at = index == 0 ? "" : "at " + run.AtMs.ToString(CultureInfo.InvariantCulture) + "ms ";
            return skipped + at + TextRules.Bound(run.Value, MaxValueChars) + (run.Count > 1 ? " x" + run.Count.ToString(CultureInfo.InvariantCulture) : "");
        });
        return TextRules.Bound(string.Join(" -> ", parts), StepRecorder.MaxEventDetailChars);
    }

    /// <summary>One distinct reading, how many rounds in a row read it, and the milliseconds from the poll's start to its first read.</summary>
    private sealed class Run(string value, long atMs)
    {
        public string Value { get; } = value;

        public int Count { get; set; } = 1;

        public long AtMs { get; } = atMs;
    }
}
