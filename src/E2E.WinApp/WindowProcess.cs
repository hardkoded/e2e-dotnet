// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.Versioning;
using E2E.Engine;

namespace E2E.WinApp;

/// <summary>
/// One app process the driver started: launched without a shell, its output pumped to the attempt's app log, and ended with its
/// whole tree. The launch waits for the process to show a visible top-level window; a process that exits first, or shows none in
/// time, is ended and reported as a launch failure.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowProcess : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private readonly Process _process;

    private WindowProcess(Process process, long mainWindow)
    {
        _process = process;
        MainWindow = mainWindow;
    }

    public int ProcessId => _process.Id;

    /// <summary>The first visible window the process showed.</summary>
    public long MainWindow { get; }

    /// <summary>
    /// Starts <paramref name="executable"/> with <paramref name="arguments"/> and waits for a visible window of the process, as
    /// <paramref name="windows"/> lists them, within <paramref name="timeout"/>. On timeout, cancel, or an early exit the process tree is ended.
    /// </summary>
    public static async Task<WindowProcess> StartAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        Action<AppLogEntry>? log,
        Func<int, IReadOnlyList<WindowInfo>> windows,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(windows);
        if (!File.Exists(executable))
        {
            throw new WindowDriverException(DriverFault.Launch, $"the app executable {executable} does not exist; build the app first, or point app.appPath (or {WinAppTargetEngine.AppPathVariable}) at the build");
        }

        var info = new ProcessStartInfo(executable)
        {
            WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : Path.GetDirectoryName(executable) ?? ".",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        Process process;
        try
        {
            process = Process.Start(info) ?? throw new WindowDriverException(DriverFault.Launch, $"{executable} did not start");
        }
        catch (Exception cause) when (cause is not WindowDriverException and not OperationCanceledException)
        {
            throw new WindowDriverException(DriverFault.Launch, $"{executable} could not be started: {cause.Message}", cause);
        }

        try
        {
            Pump(process, log);
            var deadline = Stopwatch.StartNew();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (process.HasExited)
                {
                    throw new WindowDriverException(DriverFault.Launch, $"{Path.GetFileName(executable)} exited with code {process.ExitCode} before it showed a window");
                }

                var visible = windows(process.Id);
                if (visible.Count > 0)
                {
                    return new WindowProcess(process, visible[0].Handle);
                }

                if (deadline.Elapsed >= timeout)
                {
                    throw new WindowDriverException(DriverFault.Launch, $"{Path.GetFileName(executable)} showed no window within {timeout.TotalSeconds:0.#} s");
                }

                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            await EndAsync(process, E2EDefaults.CleanupTimeout).ConfigureAwait(false);
            process.Dispose();
            throw;
        }
    }

    /// <summary>Ends the process tree and waits for it, no longer than <paramref name="timeout"/>.</summary>
    public async Task KillAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            await EndAsync(_process, timeout, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _process.Dispose();
        }
    }

    public void Dispose() => _process.Dispose();

    /// <summary>Reads both output streams as they come, so a chatty app never blocks on a full pipe, and hands each line to the log.</summary>
    private static void Pump(Process process, Action<AppLogEntry>? log)
    {
        process.OutputDataReceived += (_, args) => Forward(log, AppLogLevel.Info, args.Data);
        process.ErrorDataReceived += (_, args) => Forward(log, AppLogLevel.Error, args.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    private static void Forward(Action<AppLogEntry>? log, AppLogLevel level, string? line)
    {
        if (line is null || log is null)
        {
            return;
        }

        try
        {
            log(new AppLogEntry(AppLogSource.Console, level, line));
        }
        catch (Exception)
        {
            // The trace is a reader of the attempt, never a reason it fails.
        }
    }

    private static async Task EndAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }

        using var expiry = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        expiry.CancelAfter(timeout < TimeSpan.Zero ? TimeSpan.Zero : timeout);
        try
        {
            await process.WaitForExitAsync(expiry.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The budget ran out; the kill was sent and the process is abandoned.
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }
    }
}
