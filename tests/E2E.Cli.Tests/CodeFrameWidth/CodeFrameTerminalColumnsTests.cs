// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Report;

namespace E2E.Cli.Tests.CodeFrameWidth;

/// <summary>
/// <c>describe('code-frame terminal columns')</c> of <c>tests/unit/code-frame-width.test.ts</c>. All 7 cases are ported. Upstream sets
/// <c>process.stdout.columns</c>; here <c>Format.WithTerminalSize</c> (.NET-only) sets the width the frame measures against.
/// </summary>
public sealed class CodeFrameTerminalColumnsTests : IDisposable
{
    private static readonly Colors Plain = new(false);

    private readonly string _file = Path.Combine(Path.GetTempPath(), "e2e-code-frame-width-" + Guid.NewGuid().ToString("N") + ".cs");

    public void Dispose() => File.Delete(_file);

    [Theory]
    [InlineData("const x = 1; ", 13)]
    [InlineData("const 界 = 1; ", 14)]
    [InlineData("const é = 1; ", 13)]
    [InlineData("const value = \"\U0001F469\u200d\U0001F4BB\"; ", 20)]
    [InlineData("\tconst x = 1; ", 14)]
    public void Aligns_the_caret_after_a_prefix(string prefix, int prefixColumns)
    {
        var rows = Render(prefix + "throw new Error();", prefix.Length + 1);

        Assert.Equal("   |" + new string(' ', prefixColumns + 1) + "^", rows[1]);
    }

    [Fact]
    public void Clips_wide_source_text_to_the_terminal_budget_without_splitting_glyphs()
    {
        var rows = Render("const x = \"" + new string('界', 50) + "\";", 1, 40);

        Assert.True(Format.VisibleWidth(rows[0]) <= 35);
        Assert.EndsWith("…", rows[0], StringComparison.Ordinal);
        Assert.Equal("   | ^", rows[1]);
    }

    [Fact]
    public void Omits_a_caret_when_the_failing_column_is_outside_the_clipped_source()
    {
        var prefix = new string('界', 30);
        var rows = Render(prefix + "throw new Error();", prefix.Length + 1, 40);

        Assert.Single(rows);
        Assert.True(Format.VisibleWidth(rows[0]) <= 35);
    }

    private IReadOnlyList<string> Render(string line, int column, int columns = 80)
    {
        using var terminal = Format.WithTerminalSize(columns: columns);
        File.WriteAllText(_file, line);
        return CodeFrame.Render(_file, 1, column, Plain);
    }
}
