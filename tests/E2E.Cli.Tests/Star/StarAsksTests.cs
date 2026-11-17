// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Cli.Tests.Star;

/// <summary>
/// Upstream's <c>describe('star asks')</c> of <c>tests/unit/star.test.ts</c>. Not ported: "shows the wordmark after an upgrade only
/// where it fits" (the port has no <c>upgradeCelebration</c>, no <c>e2e upgrade</c>, and no wordmark). The third <c>it</c> is partial:
/// the port has no <c>updateStarLine</c>, so it checks the init strip.
/// </summary>
public sealed partial class StarAsksTests
{
    private const string Line = "★  You're all set!  Star e2e on GitHub → github.com/hardkoded/e2e-dotnet";

    private static readonly Func<string, string?> NoEnv = name => name == "NO_COLOR" ? "1" : null;

    [GeneratedRegex("\u001b\\[[0-9;]*m|\u001b\\]8;;[^\u001b]*\u001b\\\\")]
    private static partial Regex Ansi();

    [Fact]
    public void Boxes_the_init_strip_and_drops_the_box_on_a_narrow_terminal()
    {
        var boxed = Plain(E2E.Cli.Star.InitStarStrip(80, NoEnv)).Split('\n');
        Assert.Equal($"  │  {Line}  │", boxed[1]);
        Assert.Equal($"  ╭{new string('─', Line.Length + 4)}╮", boxed[0]);
        Assert.Equal($"{Line}\n\n", Plain(E2E.Cli.Star.InitStarStrip(40, NoEnv)));
    }

    [Fact]
    // Partial: the port has no updateStarLine, so the init strip stands in.
    public void Partial_Paints_gold_and_links_the_repository_where_the_terminal_styles_text_and_stays_plain_where_it_does_not()
    {
        var styled = E2E.Cli.Star.InitStarStrip(80, name => name switch
            {
                "COLORTERM" => "truecolor",
                "WT_SESSION" => "1",
                _ => null,
            });
        Assert.Contains("\u001b[38;2;255;200;0m★\u001b[39m", styled, StringComparison.Ordinal);
        Assert.Contains("\u001b]8;;https://github.com/hardkoded/e2e-dotnet\u001b\\", styled, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\u001b",
            E2E.Cli.Star.InitStarStrip(80, name => name == "TERM" ? "dumb" : null),
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "\u001b",
            E2E.Cli.Star.InitStarStrip(80, name => name == "NO_COLOR" ? "1" : null),
            StringComparison.Ordinal
        );
    }

    private static string Plain(string text) => Ansi().Replace(text, string.Empty);
}
