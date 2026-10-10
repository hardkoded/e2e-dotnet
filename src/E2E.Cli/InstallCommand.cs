// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Cli;

/// <summary>
/// <c>e2e install</c>: upstream's <c>e2e-web install</c>. It installs browsers with the Playwright version
/// this package pins, and installs Chromium when no browser is named.
/// </summary>
internal static class InstallCommand
{
    private static readonly string[] Browsers = ["chromium", "firefox", "webkit"];

    internal const string Usage = """
        Usage: e2e install [chromium|firefox|webkit ...] [--with-deps]
               dotnet e2e install [chromium|firefox|webkit ...] [--with-deps]

        Installs browsers for the Playwright version E2E runs. Installs chromium
        when no browser is named. --with-deps also installs the system libraries
        the browsers need on Linux.
        """;

    /// <summary>Runs the command with the arguments after <c>install</c>. <paramref name="run"/> is the Playwright CLI and a test seam.</summary>
    internal static async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr, Func<string[], int> run)
    {
        if (args.Any(arg => arg is "-h" or "--help"))
        {
            await stdout.WriteLineAsync(Usage).ConfigureAwait(false);
            return 0;
        }

        var options = args.Where(arg => arg.StartsWith('-')).ToList();
        var names = args.Where(arg => !arg.StartsWith('-')).ToList();
        if (options.Find(option => option != "--with-deps") is { } unknownOption)
        {
            await stderr.WriteLineAsync("unknown option \"" + unknownOption + "\"\n\n" + Usage).ConfigureAwait(false);
            return 2;
        }

        if (names.Find(name => !Browsers.Contains(name)) is { } unknownBrowser)
        {
            await stderr.WriteLineAsync("unknown browser \"" + unknownBrowser + "\"; expected one of " + string.Join(", ", Browsers)).ConfigureAwait(false);
            return 2;
        }

        return await Task.Run(() => WebEngine.RunInstall([.. options, .. names.Count > 0 ? names : ["chromium"]], run)).ConfigureAwait(false);
    }
}
