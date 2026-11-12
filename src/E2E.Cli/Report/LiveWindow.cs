// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace E2E.Cli.Report;

/// <summary>
/// Live block for terminal reporters, after vitest's window renderer (upstream's <c>report/live-window.ts</c>): the reporter supplies the lines,
/// the window keeps them painted below the permanent log and repaints on a timer so elapsed times tick. Constructed without a raw control channel
/// it is a no-op, so callers never branch. .NET-only: the timer comes from a <see cref="TimeProvider"/>, and its callback takes <c>gate</c>, the
/// lock the reporter holds while it handles an event, since a timer thread is not the one that handles events.
/// </summary>
internal sealed class LiveWindow(Action<string>? raw, Func<int, IReadOnlyList<string>> getWindow, TimeProvider time, object gate)
{
    private const char Esc = '\u001b';

    /// <summary>Repaint cadence, and so the animation frame of anything the window shows: a spinner keyed to it advances exactly once per paint (upstream's <c>REPAINT_INTERVAL_MS</c>).</summary>
    public const int RepaintIntervalMs = 80;

    /// <summary>
    /// Columns a window line leaves free: the ellipsis takes one, and a line that reaches the exact width puts some terminals into pending-wrap,
    /// which breaks the erase row count the same way real wrapping does (upstream's <c>WIDTH_MARGIN</c>).
    /// </summary>
    public const int WidthMargin = 2;

    /// <summary>Synchronized-output markers: terminals that support them repaint atomically.</summary>
    private const string SyncStart = "\u001b[?2026h";

    private const string SyncEnd = "\u001b[?2026l";

    /// <summary>Rows the block currently occupies below the log.</summary>
    private int _rows;

    /// <summary>
    /// Screen rows the permanent log holds above the block: every line printed since the run began, counted by wrapped rows. The block pads
    /// itself to the rows under them, so the summary sits at the bottom of the screen on any terminal instead of hanging mid-screen on a tall one.
    /// </summary>
    private int _above;

    private bool _active;

    private ITimer? _timer;

    /// <summary>Records one permanent line written above the block, so the block knows the room left under the log (upstream's <c>logged</c>).</summary>
    public void Logged(string line)
    {
        if (raw is not null)
        {
            _above += RowsOf(line, Format.TerminalColumns());
        }
    }

    /// <summary>Rows the block may take without scrolling the log: the screen under the log, minus the row the cursor rests on.</summary>
    private int Room() => Math.Max(0, Format.TerminalRows() - 1 - _above);

    /// <summary>Begins painting; the block repaints on a timer until <see cref="Stop"/>.</summary>
    public void Start()
    {
        if (raw is null || _active)
        {
            return;
        }

        _active = true;
        _timer = time.CreateTimer(_ => Tick(), null, TimeSpan.FromMilliseconds(RepaintIntervalMs), TimeSpan.FromMilliseconds(RepaintIntervalMs));
        Redraw();
    }

    private void Tick()
    {
        // A timer thread has no caller to hear a failure: a closed stdout ends the repaints instead of the process.
        lock (gate)
        {
            try
            {
                Redraw();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                _timer?.Dispose();
                _timer = null;
                _active = false;
            }
        }
    }

    /// <summary>Clears the block so permanent log lines can be appended.</summary>
    public void Erase()
    {
        if (raw is null || _rows == 0)
        {
            return;
        }

        raw($"{Esc}[{_rows}A{Esc}[0J");
        _rows = 0;
    }

    /// <summary>Repaints the block below the log; every line is clamped so the row count stays exact.</summary>
    public void Redraw()
    {
        if (raw is null || !_active)
        {
            return;
        }

        var columns = Format.TerminalColumns();
        var all = getWindow(Room());

        // Safety net under the reporter's own budget: a block taller than the screen breaks the cursor-up erase, so the top of it is dropped.
        var maxRows = Format.TerminalRows() - 1;
        IReadOnlyList<string> lines = all.Count > maxRows ? [.. all.Skip(all.Count - maxRows)] : all;

        // A block taller than the room scrolls the log up by the excess; the rows above it are then exactly what the block left.
        _above = Math.Min(_above, maxRows - lines.Count);
        var payload = new StringBuilder(SyncStart);
        if (_rows > 0)
        {
            payload.Append($"{Esc}[{_rows}A{Esc}[0J");
        }

        foreach (var line in lines)
        {
            payload.Append(ClampToWidth(line, columns)).Append('\n');
        }

        payload.Append(SyncEnd);
        _rows = lines.Count;
        raw(payload.ToString());
    }

    /// <summary>Ends the block for good: the timer is cleared and the block erased, so nothing repaints after the final summary.</summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _active = false;
        Erase();
    }

    /// <summary>
    /// Clamps one line to the terminal width in columns, skipping ANSI sequences when counting and never cutting inside a grapheme cluster: a
    /// glyph or emoji sequence that would cross the limit is dropped whole. The erase sequence counts physical rows, so a line that wraps would
    /// leave stale fragments behind on every repaint. An entry with a line break still paints as one row (upstream's <c>clampToWidth</c>).
    /// </summary>
    private static string ClampToWidth(string text, int columns)
    {
        text = text.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\r', ' ').Replace('\n', ' ');
        var max = Math.Max(4, columns - WidthMargin);
        var width = 0;
        var clamped = new StringBuilder();
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == Esc)
            {
                var end = text.IndexOf('m', i);
                if (end == -1)
                {
                    break;
                }

                clamped.Append(text, i, end + 1 - i);
                i = end + 1;
                continue;
            }

            var next = text.IndexOf(Esc, i);
            var run = text[i..(next == -1 ? text.Length : next)];
            foreach (var (cluster, clusterColumns) in Format.Graphemes(run))
            {
                if (width + clusterColumns > max)
                {
                    return $"{clamped}{Esc}[0m…";
                }

                clamped.Append(cluster);
                width += clusterColumns;
            }

            i += run.Length;
        }

        return clamped.ToString();
    }

    /// <summary>Screen rows one permanent line takes: one per wrapped row of each of its lines. A line as wide as the terminal still fits in one row (upstream's <c>rowsOf</c>).</summary>
    private static int RowsOf(string text, int columns) =>
        text.Split('\n').Sum(line => Math.Max(1, (int)Math.Ceiling(Format.VisibleWidth(line) / (double)Math.Max(1, columns))));
}
