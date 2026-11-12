// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Report;
using E2E.Cli.Tests.ReportList;
using static E2E.Cli.Tests.ReportList.ListReporterKit;

namespace E2E.Cli.Tests.LiveWindow;

/// <summary>
/// Upstream: packages/e2e/tests/unit/live-window.test.ts, describe('LiveWindow'). All 7 <c>it</c> are ported. The terminal size that upstream's
/// <c>withTerminalSize</c> sets on <c>process.stdout</c> is <c>Format.WithTerminalSize</c> (.NET-only). The window's timer comes from a
/// <see cref="TimeProvider"/> whose timers never fire, and its lock is a plain object (.NET-only: see <see cref="global::E2E.Cli.Report.LiveWindow"/>).
/// </summary>
public sealed class LiveWindowTests
{
    private const string Family = "\U0001F468‍\U0001F469‍\U0001F467‍\U0001F466";

    private const string Flag = "\U0001F1F5\U0001F1F1";

    /// <summary>Paints <paramref name="lines"/> once on a terminal <paramref name="columns"/> wide and returns that paint's payload.</summary>
    private static string Paint(IReadOnlyList<string> lines, int columns)
    {
        using var terminal = Format.WithTerminalSize(rows: 40, columns: columns);
        List<string> payloads = [];
        var window = new global::E2E.Cli.Report.LiveWindow(payloads.Add, _ => lines, new FakeTime(DateTimeOffset.UnixEpoch), new object());
        window.Start();
        window.Stop();
        return payloads[0];
    }

    /// <summary>The painted text without its control sequences and the line break.</summary>
    private static string Shown(string payload) => StripAnsi(payload).TrimEnd('\n');

    [Fact]
    public void Keeps_multiline_titles_on_one_physical_row_when_repainting()
    {
        using var terminal = Format.WithTerminalSize(rows: 40, columns: 80);
        List<string> payloads = [];
        var window = new global::E2E.Cli.Report.LiveWindow(payloads.Add, _ => ["first\nsecond"], new FakeTime(DateTimeOffset.UnixEpoch), new object());
        window.Start();
        window.Redraw();
        window.Stop();
        Assert.Equal("first second", Shown(payloads[0]));
        Assert.Contains("\u001b[1A", payloads[1], StringComparison.Ordinal);
        Assert.Equal(2, payloads[1].Split('\n').Length);
    }

    [Fact]
    public void Normalizes_CRLF_and_CR_without_removing_colors_or_exceeding_the_row_width()
    {
        Assert.Equal("first second third", Shown(Paint(["first\r\nsecond\rthird"], 80)));
        var painted = Paint(["\u001b[31mfirst\nsecond\u001b[0m"], 10);
        Assert.Contains("\u001b[31m", painted, StringComparison.Ordinal);
        Assert.Equal("first se…", Shown(painted));
    }

    [Fact]
    public void Never_cuts_a_line_inside_a_code_point()
    {
        var payload = Paint(["abc\U0001F600def"], 6);

        // A lone surrogate is the one thing that does not encode.
        Assert.Equal(payload, System.Text.Encoding.UTF8.GetString(new System.Text.UTF8Encoding(false, true).GetBytes(payload)));
        Assert.Equal("abc…", Shown(payload));
    }

    [Fact]
    public void Never_splits_a_grapheme_cluster_an_emoji_sequence_goes_whole_or_not_at_all()
    {
        Assert.Equal($"ab{Family}…", Shown(Paint([$"ab{Family}cd"], 6)));
        Assert.Equal("abc…", Shown(Paint([$"abc{Family}d"], 6)));
        Assert.Equal("abc…", Shown(Paint([$"abc{Flag}d"], 6)));
    }

    [Fact]
    public void Clamps_an_emoji_title_at_the_column_it_reaches_two_per_emoji()
    {
        var payload = Paint(["a\U0001F600b\U0001F600c"], 8);
        Assert.Equal("a\U0001F600b\U0001F600…", Shown(payload));
        Assert.Equal(7, Format.VisibleWidth(Shown(payload)));
    }

    [Fact]
    public void Clamps_a_CJK_title_through_a_color_run_keeping_the_sequences()
    {
        var payload = Paint(["\u001b[31m日本語\u001b[0mテスト"], 12);
        Assert.Equal("日本語テス…", Shown(payload));
        Assert.Contains("\u001b[31m日本語\u001b[0m", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void Counts_a_logged_CJK_line_by_the_rows_its_columns_wrap_to()
    {
        using var terminal = Format.WithTerminalSize(rows: 40, columns: 10);
        List<int> rooms = [];
        var window = new global::E2E.Cli.Report.LiveWindow(
            _ => { },
            room =>
            {
                rooms.Add(room);
                return [];
            },
            new FakeTime(DateTimeOffset.UnixEpoch),
            new object());
        window.Start();
        window.Logged("日本語テスト");
        window.Redraw();
        window.Stop();
        Assert.Equal([39, 37], rooms);
    }
}
