// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Mobile;

/// <summary>
/// A failure the agent-device daemon reported: a stable <see cref="Code"/> (<c>SESSION_NOT_FOUND</c>,
/// <c>COMMAND_FAILED</c>) and a details bag the failure itself declared (<c>reason</c>, <c>hint</c>,
/// <c>runnerErrorCode</c>, <c>dispatched</c>). This is agent-device's <c>AppError</c>.
/// </summary>
public sealed class AgentDeviceError : Exception
{
    public AgentDeviceError(string code, string message, IReadOnlyDictionary<string, object?>? details = null, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
        Details = details;
    }

    public string Code { get; }

    /// <summary>True when the daemon itself reported this failure, which proves it answered. .NET-only: tells a daemon answer from a client-side or transport failure.</summary>
    internal bool Reported { get; init; }

    public IReadOnlyDictionary<string, object?>? Details { get; }
}

/// <summary>An error in agent-device's normalized form: its code and its message.</summary>
public sealed record NormalizedAgentDeviceError(string Code, string Message);

/// <summary>agent-device's <c>normalizeAgentDeviceError</c>, reduced to the code and message the engine reads.</summary>
public static partial class AgentDeviceErrors
{
    /// <summary>True for a failure the daemon reported.</summary>
    public static bool IsAgentDeviceError(Exception? cause) => cause is AgentDeviceError;

    /// <summary>
    /// The code and message of any failure. A failure that is not the daemon's has code <c>UNKNOWN</c> and
    /// keeps its message. A failed process reports the line of its stderr that names the problem.
    /// </summary>
    public static NormalizedAgentDeviceError Normalize(Exception cause)
    {
        if (cause is not AgentDeviceError error)
        {
            return new NormalizedAgentDeviceError("UNKNOWN", cause.Message);
        }

        return new NormalizedAgentDeviceError(error.Code, ProcessFailureMessage(error));
    }

    /// <summary>agent-device's <c>defaultHintForCode</c> (0.21.35): the CLI advice its normalizer adds to a failure of this code.</summary>
    public static string DefaultHintForCode(string code) => code switch
    {
        "INVALID_ARGS" => "Check command arguments and run --help for usage examples.",
        "SESSION_NOT_FOUND" => "Run open first or pass an explicit device selector.",
        "TOOL_MISSING" => "Install required platform tooling and ensure it is available in PATH.",
        "DEVICE_NOT_FOUND" => "Verify the target device is booted/connected and selectors match.",
        "APP_NOT_INSTALLED" => "Run apps to discover the exact installed package or bundle id, or install the app before open.",
        "UNSUPPORTED_OPERATION" => "This command is not available for the selected platform/device.",
        "UNSUPPORTED_PLATFORM" => "This platform is not supported for the requested operation; run devices to inspect available targets.",
        "AMBIGUOUS_MATCH" => "Multiple candidates matched. Narrow the query or pass an exact identifier.",
        "DEVICE_IN_USE" => "The device is busy with another agent-device request; retry once it frees up.",
        "REPLAY_DIVERGENCE" => "Read details.divergence (screen/suggestions) for repair context, or rerun with --json for the full report.",
        "REPAIR_SESSION_EXPIRED" => "The --save-script repair session was reaped before it was finalized; re-run replay <script> --save-script from the start.",
        "REPAIR_COMMIT_FAILED" => "The repair transaction completed, but committing its healed script failed at teardown (no-clobber refusal or a filesystem error); inspect the target path/permissions, then re-run replay <script> --save-script to retry.",
        "NOT_IMPLEMENTED" => "This command is part of the planned API but is not implemented yet.",
        "UNAUTHORIZED" => "Refresh daemon metadata and retry the command.",
        "UNKNOWN" => "Unexpected internal error. Retry with --debug and report the diagnostics log if it persists.",
        _ => "Retry with --debug and inspect diagnostics log for details.",
    };

    private static string ProcessFailureMessage(AgentDeviceError error)
    {
        if (error.Code != "COMMAND_FAILED" || error.Details?.GetValueOrDefault("processExitError") is not true)
        {
            return error.Message;
        }

        var stderr = error.Details.GetValueOrDefault("stderr") as string ?? "";
        var line = FirstMeaningfulLine(stderr);
        if (line is null)
        {
            return error.Message;
        }

        if (ExitCodeMessage().IsMatch(error.Message))
        {
            return line;
        }

        return error.Message.Contains(line, StringComparison.Ordinal) ? error.Message : $"{error.Message}: {line}";
    }

    private static string? FirstMeaningfulLine(string stderr)
    {
        foreach (var raw in stderr.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || Boilerplate().IsMatch(line))
            {
                continue;
            }

            var text = ToolPrefix().Replace(line, "", 1).Trim();
            if (text.Length > 0)
            {
                return text.Length > 200 ? text[..200] + "..." : text;
            }
        }

        return null;
    }

    [GeneratedRegex(@"^\S+ exited with code -?\d+\z")]
    private static partial Regex ExitCodeMessage();

    [GeneratedRegex(
        @"^(?:an error was encountered processing the command|underlying error\b|simulator device failed to complete the requested operation)",
        RegexOptions.IgnoreCase)]
    private static partial Regex Boilerplate();

    [GeneratedRegex(@"^(?:(?:adb|xcrun|simctl):\s*)?(?:error:\s*)?", RegexOptions.IgnoreCase)]
    private static partial Regex ToolPrefix();
}
