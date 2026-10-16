// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Tests;

/// <summary>
/// A local Playwright server (the driver's <c>run-server</c>): the stand-in for a grid that speaks Playwright's own protocol,
/// the one a <see cref="BrowserProtocol.Playwright"/> lease names. Each connection to it launches its own browser, with no
/// context open, as a hosted grid's does.
/// </summary>
internal sealed partial class RemotePlaywrightServer : IAsyncDisposable
{
    private readonly Process _process;

    private RemotePlaywrightServer(Process process, string endpoint)
    {
        _process = process;
        Endpoint = endpoint;
    }

    public string Endpoint { get; }

    public bool Running => !_process.HasExited;

    public static async Task<RemotePlaywrightServer> StartAsync()
    {
        await WebEngine.EnsureChromiumAsync(headed: false, CancellationToken.None);
        var driver = Path.Combine(AppContext.BaseDirectory, ".playwright");
        var platform = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        var node = OperatingSystem.IsWindows()
            ? Path.Combine(driver, "node", "win32_x64", "node.exe")
            : Path.Combine(driver, "node", (OperatingSystem.IsMacOS() ? "darwin-" : "linux-") + platform, "node");
        var info = new ProcessStartInfo(node) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { Path.Combine(driver, "package", "cli.js"), "run-server", "--port", "0" })
        {
            info.ArgumentList.Add(argument);
        }

        var process = Process.Start(info)!;
        process.ErrorDataReceived += (_, _) => { };
        process.BeginErrorReadLine();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (await process.StandardOutput.ReadLineAsync(timeout.Token) is { } line)
            {
                if (ListeningPattern().Match(line) is { Success: true } match)
                {
                    return new RemotePlaywrightServer(process, match.Groups[1].Value);
                }
            }

            throw new InvalidOperationException("the Playwright server exited before it was listening");
        }
        catch
        {
            process.Kill(entireProcessTree: true);
            process.Dispose();
            throw;
        }
    }

    /// <summary>Kills the server and the browsers it launched, as a grid that lost the session does.</summary>
    public async Task KillAsync()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
        }

        await _process.WaitForExitAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await KillAsync();
        _process.Dispose();
    }

    [GeneratedRegex(@"Listening on (ws://\S+)")]
    private static partial Regex ListeningPattern();
}
