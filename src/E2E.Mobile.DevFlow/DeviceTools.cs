// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel;
using System.Diagnostics;

namespace E2E.Mobile.DevFlow;

/// <summary>What one tool run ended with.</summary>
internal sealed record ToolResult(int ExitCode, string Output, string Error)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>
/// Runs the device tools the launcher drives, <c>xcrun simctl</c> and <c>adb</c>: the seam unit tests script. A run
/// is bounded, reads both streams, and kills the process tree when the bound or the caller's cancellation ends it.
/// </summary>
internal interface IDeviceTools
{
    Task<ToolResult> RunAsync(string program, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken);
}

/// <summary>The tools as processes on this machine. Arguments travel as a list, never as a line built from outside text.</summary>
internal sealed class DeviceTools : IDeviceTools
{
    public static DeviceTools Instance { get; } = new();

    public async Task<ToolResult> RunAsync(string program, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(program) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
        }
        catch (Win32Exception cause)
        {
            throw new AgentDeviceError("COMMAND_FAILED", $"could not run `{program}`: {cause.Message}. {Requirement(program)}", inner: cause);
        }

        using var expiry = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        expiry.CancelAfter(timeout);
        var output = process.StandardOutput.ReadToEndAsync(expiry.Token);
        var error = process.StandardError.ReadToEndAsync(expiry.Token);
        try
        {
            await process.WaitForExitAsync(expiry.Token).ConfigureAwait(false);
            return new ToolResult(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // It exited on its own first.
            }

            cancellationToken.ThrowIfCancellationRequested();
            var line = string.Join(' ', [program, .. arguments]);
            throw new AgentDeviceError("COMMAND_FAILED", $"`{(line.Length > 160 ? line[..160] + "..." : line)}` did not finish within {timeout.TotalSeconds:0} seconds");
        }
    }

    private static string Requirement(string program) => program switch
    {
        "xcrun" => "The iOS simulator is driven with Xcode's command line tools, on macOS.",
        "adb" => "Android needs the Android SDK Platform-Tools adb executable on PATH.",
        _ => "",
    };
}

/// <summary>Turns a tool run that failed into the error the engine reads, with the tool's stderr where the engine looks for the cause.</summary>
internal static class ToolFailures
{
    /// <summary>The result when it succeeded, else the failure as an error. <paramref name="what"/> says what was attempted.</summary>
    public static ToolResult Expect(this ToolResult result, string what) =>
        result.Succeeded ? result : throw Failure(result, what);

    public static AgentDeviceError Failure(ToolResult result, string what) =>
        new(
            "COMMAND_FAILED",
            $"{what} failed with exit code {result.ExitCode}",
            new Dictionary<string, object?> { ["processExitError"] = true, ["exitCode"] = result.ExitCode, ["stderr"] = result.Error.Length > 0 ? result.Error : result.Output });
}
