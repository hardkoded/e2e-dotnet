// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Cli.Report;

namespace E2E.Cli;

/// <summary>
/// Upstream's <c>cli/star.ts</c>: the ask for a GitHub star, a gold strip under <c>e2e init</c>. The caller prints it only where a person
/// reads a terminal. Not ported: <c>updateStarLine</c> and <c>upgradeCelebration</c>, since the port has no update notice, no
/// <c>e2e upgrade</c>, and no wordmark.
/// </summary>
internal static class Star
{
    private const string Esc = "\u001b";
    private const string ShortUrl = "github.com/hardkoded/e2e-dotnet";

    /// <summary>Gold for each wordmark row, top to bottom, warming toward amber (upstream's <c>GRADIENT</c>; the strip uses the second).</summary>
    private static readonly (int R, int G, int B)[] Gradient =
    [
        (255, 214, 10),
        (255, 200, 0),
        (255, 186, 0),
        (252, 170, 0),
        (246, 154, 0),
        (238, 138, 0),
    ];

    /// <summary>
    /// The strip <c>e2e init</c> ends with: one line in a gold box, then a blank line. A terminal too narrow for the box gets the line
    /// alone.
    /// </summary>
    public static string InitStarStrip(int? columns, Func<string, string?>? environment = null)
    {
        var env = environment ?? Environment.GetEnvironmentVariable;
        var gold = Rgb(env, Gradient[1]);
        var line =
            $"{gold("★")}  {Bold(env, "You're all set!")}  Star e2e on GitHub {Dim(env, "→")} {RepositoryLink(env)}";
        var width = Format.VisibleWidth(line) + 4;
        var available = columns is > 0 ? columns.Value : 80;
        if (available < width + 4)
        {
            return $"{line}\n\n";
        }

        string Border(string left, string right) => $"  {gold($"{left}{new string('─', width)}{right}")}";
        return string.Join("\n", Border("╭", "╮"), $"  {gold("│")}  {line}  {gold("│")}", Border("╰", "╯"), "", "");
    }

    /// <summary>Whether the terminal styles text (upstream asks picocolors: no <c>NO_COLOR</c>, and not a dumb terminal).</summary>
    private static bool Styled(Func<string, string?> env) =>
        string.IsNullOrEmpty(env("NO_COLOR")) && env("TERM") != "dumb";

    private static bool TrueColor(Func<string, string?> env) => env("COLORTERM") is "truecolor" or "24bit";

    private static string Bold(Func<string, string?> env, string text) =>
        Styled(env) ? $"{Esc}[1m{text}{Esc}[22m" : text;

    private static string Dim(Func<string, string?> env, string text) =>
        Styled(env) ? $"{Esc}[2m{text}{Esc}[22m" : text;

    private static Func<string, string> Rgb(Func<string, string?> env, (int R, int G, int B) color)
    {
        if (!Styled(env))
        {
            return text => text;
        }

        return TrueColor(env)
            ? text =>
                string.Create(CultureInfo.InvariantCulture, $"{Esc}[38;2;{color.R};{color.G};{color.B}m{text}{Esc}[39m")
            : text => $"{Esc}[33m{text}{Esc}[39m";
    }

    /// <summary>Windows' legacy console prints link escapes as text; Windows Terminal sets <c>WT_SESSION</c>.</summary>
    private static bool Hyperlinks(Func<string, string?> env) =>
        Styled(env) && (!OperatingSystem.IsWindows() || env("WT_SESSION") is not null);

    /// <summary>The repository as <c>github.com/hardkoded/e2e-dotnet</c>, a clickable link where the terminal supports one.</summary>
    private static string RepositoryLink(Func<string, string?> env)
    {
        var text = Styled(env) ? $"{Esc}[4m{Esc}[36m{ShortUrl}{Esc}[39m{Esc}[24m" : ShortUrl;
        return Hyperlinks(env) ? $"{Esc}]8;;{DocsUrl.RepositoryUrl}{Esc}\\{text}{Esc}]8;;{Esc}\\" : text;
    }
}
