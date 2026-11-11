// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests;

/// <summary>
/// A local Chrome process with a random remote-debugging port, owned independently
/// of the engine's CDP transport: the host the engine attaches to.
/// </summary>
internal sealed partial class RemoteChrome : IAsyncDisposable
{
    private const string ProfilePrefix = "e2e-cdp-host-";
    private const int DeleteAttempts = 5;

    // A test host lives for minutes, so a profile untouched for a day belongs to a run that never cleaned up.
    private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(1);

    private static int _swept;

    private readonly Process _process;
    private readonly string _userDataDir;

    private RemoteChrome(Process process, string userDataDir)
    {
        _process = process;
        _userDataDir = userDataDir;
    }

    public string Endpoint { get; private set; } = "";

    /// <summary>Whether Chrome is still running; the engine never terminates the host.</summary>
    public bool Running => !_process.HasExited;

    /// <summary>Launches the remote host and releases its resources if it never becomes ready.</summary>
    public static async Task<RemoteChrome> LaunchAsync()
    {
        // The host runs the full Chromium build, which a headless install skips.
        await WebEngine.EnsureChromiumAsync(headed: true, CancellationToken.None);
        string executable;
        using (var playwright = await Playwright.CreateAsync())
        {
            executable = playwright.Chromium.ExecutablePath;
        }

        if (Interlocked.Exchange(ref _swept, 1) == 0)
        {
            SweepStaleProfiles(Path.GetTempPath(), StaleAfter);
        }

        var userDataDir = Directory.CreateTempSubdirectory(ProfilePrefix).FullName;
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        // The sandbox flags are what a containerized CI runner needs to start Chrome
        // at all; they change nothing about the attach under test.
        foreach (var argument in new[]
        {
            "--headless=new",
            "--remote-debugging-port=0",
            "--no-first-run",
            "--no-default-browser-check",
            "--password-store=basic",
            "--use-mock-keychain",
            "--disable-gpu",
            "--disable-extensions",
            "--disable-component-extensions-with-background-pages",
            "--no-sandbox",
            "--disable-setuid-sandbox",
            "--disable-dev-shm-usage",
            "--user-data-dir=" + userDataDir,
            "about:blank",
        })
        {
            info.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        var chrome = new RemoteChrome(process, userDataDir);
        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var buffered = new StringBuilder();
        process.ErrorDataReceived += (_, line) =>
        {
            if (line.Data is null)
            {
                return;
            }

            lock (buffered)
            {
                buffered.AppendLine(line.Data);
            }

            var match = EndpointPattern().Match(line.Data);
            if (match.Success)
            {
                ready.TrySetResult(match.Groups[1].Value);
            }
        };
        process.OutputDataReceived += (_, _) => { };

        // Surface Chrome's own stderr: it names the missing flag or library.
        process.Exited += (_, _) =>
        {
            lock (buffered)
            {
                ready.TrySetException(new InvalidOperationException("Chrome exited before it was ready:\n" + buffered.ToString().Trim()));
            }
        };
        try
        {
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            chrome.Endpoint = await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
            return chrome;
        }
        catch
        {
            await chrome.DisposeAsync();
            throw;
        }
    }

    /// <summary>Waits for Chrome to exit before removing the profile it writes.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }

            // A Chrome helper the kill did not reach can keep the pipes open, so stop reading and bound the wait.
            _process.CancelErrorRead();
            _process.CancelOutputRead();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            // A helper still holds the pipes; stop waiting and let the profile cleanup below cope.
        }
        catch (InvalidOperationException)
        {
            // The process never started.
        }

        _process.Dispose();
        await DeleteProfileAsync(_userDataDir);
    }

    /// <summary>
    /// Removes a profile directory, retrying briefly: a Chrome helper the kill did not reach can still hold a file
    /// for a moment after the main process exits. Gives up quietly; the next launch's sweep reclaims the directory.
    /// </summary>
    internal static async Task DeleteProfileAsync(string directory)
    {
        for (var attempt = 1; attempt <= DeleteAttempts; attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (attempt == DeleteAttempts)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt));
            }
        }
    }

    /// <summary>
    /// Removes profile directories that an earlier run left behind: a test host that was killed never reaches
    /// <see cref="DisposeAsync"/>. Only directories untouched for <paramref name="olderThan"/> go, so a host that
    /// another test process is using right now stays. Best effort; it never throws.
    /// </summary>
    internal static void SweepStaleProfiles(string root, TimeSpan olderThan)
    {
        try
        {
            var cutoff = DateTime.UtcNow - olderThan;
            foreach (var directory in Directory.EnumerateDirectories(root, ProfilePrefix + "*"))
            {
                try
                {
                    if (Directory.GetLastWriteTimeUtc(directory) < cutoff)
                    {
                        Directory.Delete(directory, recursive: true);
                    }
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // Another process removed it, or a leftover Chrome still holds it; the next sweep tries again.
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The temp root is unreadable; there is nothing to sweep.
        }
    }

    [GeneratedRegex(@"DevTools listening on (ws://\S+)")]
    private static partial Regex EndpointPattern();
}
