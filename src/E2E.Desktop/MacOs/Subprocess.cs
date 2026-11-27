// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace E2E.Desktop.MacOs;

/// <summary>
/// A child process the driver started (<c>open</c>, <c>codesign</c>, <c>screencapture</c>), polled for its exit and killed with its
/// tree when the budget runs out. Stands in for Rust's <c>std::process::Command</c> and <c>Child</c>: stdin is closed at once,
/// stdout is drained and dropped, and stderr is kept. .NET-only helper. Arguments go through <see cref="ProcessStartInfo.ArgumentList"/>,
/// never a command line.
/// </summary>
internal sealed class Subprocess : IDisposable
{
    private readonly Process _process;
    private readonly StringBuilder _stderr = new();
    private readonly Lock _lock = new();

    private Subprocess(Process process)
    {
        _process = process;
    }

    /// <summary>The text the child wrote to stderr so far.</summary>
    public string Stderr
    {
        get
        {
            lock (_lock)
            {
                return _stderr.ToString();
            }
        }
    }

    /// <summary>
    /// Starts <paramref name="fileName"/> with these arguments. With <paramref name="environment"/> the child gets exactly that
    /// environment and none of this process's (<c>env_clear</c>); with null it inherits this process's.
    /// </summary>
    /// <exception cref="Win32Exception">The program could not be started.</exception>
    public static Subprocess Start(
        string fileName,
        IEnumerable<string> arguments,
        IEnumerable<KeyValuePair<string, string>>? environment
    )
    {
        var info = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            info.Environment.Clear();
            foreach (var (name, value) in environment)
            {
                info.Environment[name] = value;
            }
        }

        var process = new Process { StartInfo = info };
        var child = new Subprocess(process);
        process.OutputDataReceived += static (_, _) => { };
        process.ErrorDataReceived += (_, data) =>
        {
            if (data.Data is null)
            {
                return;
            }

            lock (child._lock)
            {
                child._stderr.AppendLine(data.Data);
            }
        };
        try
        {
            process.Start();
        }
        catch
        {
            process.Dispose();
            throw;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.StandardInput.Close();
        return child;
    }

    /// <summary>The exit code once the child has exited, else null. Once it has, its stderr is complete.</summary>
    public int? TryWait()
    {
        if (!_process.HasExited)
        {
            return null;
        }

        // Waits, within a bound, for the output handlers to see the end of both streams.
        _process.WaitForExit(2_000);
        return _process.ExitCode;
    }

    /// <summary>Kills the child and its descendants, and waits for it.</summary>
    public void Kill()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit();
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }
    }

    public void Dispose() => _process.Dispose();

    /// <summary>Runs a short child to its end under <paramref name="timeout"/> and answers its stderr, or null when it could not run or did not finish.</summary>
    public static string? RunForStderr(
        string fileName,
        IEnumerable<string> arguments,
        IEnumerable<KeyValuePair<string, string>>? environment,
        TimeSpan timeout
    )
    {
        Subprocess child;
        try
        {
            child = Start(fileName, arguments, environment);
        }
        catch (Win32Exception)
        {
            return null;
        }

        using (child)
        {
            var until = Instant.After(timeout);
            while (child.TryWait() is null)
            {
                if (Instant.Now >= until)
                {
                    child.Kill();
                    return null;
                }

                Thread.Sleep(10);
            }

            return child.Stderr;
        }
    }
}
