// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Report;

namespace E2E.Cli.Tests.CodeFrameWidth;

/// <summary>
/// <c>describe('code-frame terminal columns')</c> of <c>tests/unit/code-frame-width.test.ts</c>. Each <c>it</c> is <c>Partial_</c>: upstream
/// sets <c>process.stdout.columns</c> and lets <c>codeFrame</c> read it; the port reads the console, which a test cannot set, so these pass
/// the budget the terminal would give (<c>columns - 10</c>) as <c>maxChars</c>. The CJK and emoji rows of the caret <c>it.each</c> are not ported:
/// <c>Format.VisibleWidth</c> counts a wide glyph as one column, where upstream's <c>visibleWidth</c> counts two.
/// </summary>
public sealed class CodeFrameTerminalColumnsTests : IDisposable
{
    private static readonly Colors Plain = new(false);

    private readonly string _file = Path.Combine(Path.GetTempPath(), "e2e-code-frame-width-" + Guid.NewGuid().ToString("N") + ".cs");

    public void Dispose() => File.Delete(_file);

    [Theory]
    [InlineData("const x = 1; ", 13)]
    [InlineData("const é = 1; ", 13)]
    [InlineData("\tconst x = 1; ", 14)]
    public void Partial_Aligns_the_caret_after_a_prefix(string prefix, int prefixColumns)
    {
        var rows = Render(prefix + "throw new Error();", prefix.Length + 1);

        Assert.Equal("   |" + new string(' ', prefixColumns + 1) + "^", rows[1]);
    }

    [Fact]
    public void Partial_Clips_wide_source_text_to_the_terminal_budget_without_splitting_glyphs()
    {
        var rows = Render("const x = \"" + new string('界', 50) + "\";", 1, 40);

        Assert.True(Format.VisibleWidth(rows[0]) <= 35);
        Assert.EndsWith("…", rows[0], StringComparison.Ordinal);
        Assert.Equal("   | ^", rows[1]);
    }

    [Fact]
    public void Partial_Omits_a_caret_when_the_failing_column_is_outside_the_clipped_source()
    {
        var prefix = new string('界', 30);
        var rows = Render(prefix + "throw new Error();", prefix.Length + 1, 40);

        Assert.Single(rows);
        Assert.True(Format.VisibleWidth(rows[0]) <= 35);
    }

    private IReadOnlyList<string> Render(string line, int column, int columns = 80)
    {
        File.WriteAllText(_file, line);
        return CodeFrame.Render(_file, 1, column, Plain, columns - 10);
    }
}
