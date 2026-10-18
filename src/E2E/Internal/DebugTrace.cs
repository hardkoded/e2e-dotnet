// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace E2E.Internal;

/// <summary>
/// Opt-in phase timing collection for <c>E2E_DEBUG=1</c>, the port's counterpart of <c>--debug</c>. Aggregates named phase
/// durations for one test. When disabled every method is a near-zero pass-through, so call sites never branch.
/// Upstream <c>internal/debug.ts</c>.
/// </summary>
internal sealed class DebugTrace(bool enabled)
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly Lock _gate = new();

    /// <summary>A trace that collects nothing, for a caller that was given none.</summary>
    public static DebugTrace Off { get; } = new(false);

    /// <summary>Whether this trace collects anything.</summary>
    public bool Enabled { get; } = enabled;

    /// <summary>Records one completed phase duration under a label.</summary>
    public void Record(string label, double durationMs)
    {
        if (!Enabled)
        {
            return;
        }

        lock (_gate)
        {
            if (_entries.TryGetValue(label, out var entry))
            {
                entry.TotalMs += durationMs;
                entry.Count++;
                entry.MaxMs = Math.Max(entry.MaxMs, durationMs);
            }
            else
            {
                _entries[label] = new Entry { TotalMs = durationMs, Count = 1, MaxMs = durationMs };
            }
        }
    }

    /// <summary>Times one async phase and records it, rethrowing any failure.</summary>
    public async Task<T> Time<T>(string label, Func<Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (!Enabled)
        {
            return await work().ConfigureAwait(false);
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            return await work().ConfigureAwait(false);
        }
        finally
        {
            Record(label, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    /// <summary>Times one async phase that returns nothing and records it, rethrowing any failure.</summary>
    public async Task Time(string label, Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        await Time(label, async () =>
        {
            await work().ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
    }

    /// <summary>Formats aggregated timings as one aligned table, phases sorted by total time.</summary>
    public string Summary()
    {
        List<string[]> rows;
        lock (_gate)
        {
            rows = [.. _entries
                .OrderByDescending(pair => pair.Value.TotalMs)
                .Select(pair => new[]
                {
                    pair.Key,
                    pair.Value.Count.ToString(CultureInfo.InvariantCulture),
                    FormatMs(pair.Value.TotalMs),
                    FormatMs(pair.Value.TotalMs / pair.Value.Count),
                    FormatMs(pair.Value.MaxMs),
                })];
        }

        return Table(
            "[e2e debug] phase timings (wall " + FormatMs(Stopwatch.GetElapsedTime(_started).TotalMilliseconds) + ")",
            ["phase", "count", "total", "avg", "max"],
            rows,
            "(no phases recorded)");
    }

    /// <summary>
    /// Renders one aligned table. The first column is left-aligned and the rest are right-aligned, which suits numbers;
    /// <paramref name="leftAligned"/> names any further columns that hold prose and would be unreadable pushed to the right.
    /// </summary>
    public static string Table(string title, IReadOnlyList<string> header, IReadOnlyList<string[]> rows, string empty, ISet<int>? leftAligned = null)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(rows);
        var widths = header.Select((label, column) => Math.Max(label.Length, rows.Select(row => column < row.Length ? row[column].Length : 0).DefaultIfEmpty(0).Max())).ToArray();
        string Line(IReadOnlyList<string> row) =>
            "  " + string.Join(
                "  ",
                row.Select((cell, column) => column == 0 || leftAligned?.Contains(column) == true ? cell.PadRight(widths[column]) : cell.PadLeft(widths[column]))).TrimEnd();

        var text = new StringBuilder().Append(title).Append('\n').Append(Line(header)).Append('\n');
        if (rows.Count == 0)
        {
            text.Append("  ").Append(empty).Append('\n');
        }
        else
        {
            foreach (var row in rows)
            {
                text.Append(Line(row)).Append('\n');
            }
        }

        return text.ToString();
    }

    /// <summary>A duration the way the tables show it: whole milliseconds, or seconds from 10 s up.</summary>
    public static string FormatMs(double value) =>
        value >= 10_000
            ? (value / 1000).ToString("0.0", CultureInfo.InvariantCulture) + "s"
            : Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "ms";

    private sealed class Entry
    {
        public double TotalMs;
        public int Count;
        public double MaxMs;
    }
}
