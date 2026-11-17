// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Cli.Tests.CliInit;

/// <summary>
/// Upstream's <c>describe('on a terminal')</c> of <c>tests/unit/cli-init.test.ts</c>. Not ported: "leaves the strip out when the command
/// init was started for begins right away" (the port has no <c>continueWith</c>).
/// </summary>
public sealed partial class OnATerminalTests : CliInitTestBase
{
    [GeneratedRegex(
        @"Next: [^\n]*\n[\s\S]*★ {2}You're all set! {2}Star e2e on GitHub → github\.com/hardkoded/e2e-dotnet"
    )]
    private static partial Regex Strip();

    [GeneratedRegex("\u001b\\[[0-9;]*m|\u001b\\]8;;[^\u001b]*\u001b\\\\")]
    private static partial Regex Ansi();

    [Fact]
    public async Task Ends_with_the_star_strip_and_not_in_CI()
    {
        await RunInitAsync(Terminal(_ => null));
        Assert.Matches(Strip(), Ansi().Replace(Output, string.Empty));
        Directory.Delete(Dir, recursive: true);
        Directory.CreateDirectory(Dir);
        ClearOutput();
        await RunInitAsync(Terminal(name => name == "CI" ? "true" : null));
        Assert.Contains("Next: ", Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Star e2e on GitHub", Output, StringComparison.Ordinal);
    }

    private static InitOptions Terminal(Func<string, string?> environment) =>
        new()
        {
            Yes = true,
            OutputIsTerminal = true,
            Environment = environment,
        };
}
