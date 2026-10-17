// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace E2E.Internal;

/// <summary>The two settings a mode is resolved for.</summary>
internal enum RecordingKind
{
    Trace,
    Video,
}

/// <summary>Which attempts keep what they captured: every one, or only one that ran and did not pass.</summary>
internal enum Keep
{
    Always,
    OnFailure,
}

/// <summary>How a final attempt ended.</summary>
internal enum AttemptStatus
{
    Passed,
    Failed,
    TimedOut,
    Interrupted,
    Skipped,
}

/// <summary>What a recording that fails to start or finalize does.</summary>
internal enum VideoPolicy
{
    /// <summary>A default mode drops the recording quietly.</summary>
    BestEffort,

    /// <summary>A mode someone set fails the attempt's launch or cleanup.</summary>
    Required,
}

/// <summary>The video one attempt records.</summary>
internal sealed record VideoRecording(Keep Keep, VideoPolicy Policy);

/// <summary>
/// The modes <c>trace</c> and <c>video</c> share, and the one per-attempt decision both follow: which attempts keep what
/// they captured. Upstream <c>internal/recording-modes.ts</c>.
/// </summary>
internal static class RecordingModes
{
    /// <summary>Every mode, in the order the error messages list them. Upstream <c>RECORDING_MODES</c>.</summary>
    public static readonly IReadOnlyList<RecordingMode> All =
        [RecordingMode.Off, RecordingMode.On, RecordingMode.RetainOnFailure, RecordingMode.OnFirstRetry, RecordingMode.OnAllRetries];

    /// <summary>The modes as a sentence fragment every message that names one reads.</summary>
    public static string List => string.Join(", ", All.Select(Name));

    /// <summary>The spelling of a mode in the config and the environment.</summary>
    public static string Name(RecordingMode mode) => mode switch
    {
        RecordingMode.Off => "off",
        RecordingMode.On => "on",
        RecordingMode.RetainOnFailure => "retain-on-failure",
        RecordingMode.OnFirstRetry => "on-first-retry",
        RecordingMode.OnAllRetries => "on-all-retries",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    /// <summary>Reads a mode from its spelling. Upstream <c>isRecordingMode</c>.</summary>
    public static bool TryParse(string? value, out RecordingMode mode)
    {
        foreach (var candidate in All)
        {
            if (string.Equals(Name(candidate), value, StringComparison.Ordinal))
            {
                mode = candidate;
                return true;
            }
        }

        mode = default;
        return false;
    }

    /// <summary>
    /// An old trace spelling and the mode it meant, for the message that refuses it: <c>all</c> is <c>on</c>,
    /// and <c>retries</c> is <c>on-all-retries</c>, bare or as the block <c>{ record }</c>. <paramref name="was"/> spells
    /// the value as written. False for anything else. Upstream <c>legacyTraceSpelling</c>.
    /// </summary>
    public static bool TryLegacyTraceSpelling(string? record, bool block, [NotNullWhen(true)] out string? was, out RecordingMode mode)
    {
        mode = record == "retries" ? RecordingMode.OnAllRetries : RecordingMode.On;
        was = record is "retries" or "all" ? (block ? "{ record: '" + record + "' }" : "'" + record + "'") : null;
        return was is not null;
    }

    /// <summary>
    /// Whether an attempt that ended <paramref name="status"/> keeps what it captured under <paramref name="keep"/>:
    /// nothing of an attempt that never ran, and under <see cref="Keep.OnFailure"/> only one that did not pass,
    /// interrupted ones included. One rule for the trace and the video alike.
    /// </summary>
    public static bool Keeps(Keep keep, AttemptStatus status)
    {
        return status != AttemptStatus.Skipped && (keep == Keep.Always || status != AttemptStatus.Passed);
    }

    /// <summary>Whether a mode records retries only, so a test with no retries records nothing under it.</summary>
    public static bool IsRetryMode(RecordingMode mode) => mode is RecordingMode.OnFirstRetry or RecordingMode.OnAllRetries;

    /// <summary>
    /// Which attempts keep what they capture under <paramref name="mode"/>, for the attempt at <paramref name="attemptIndex"/>
    /// (0 for the first run, 1 for the first retry). Null captures nothing.
    /// </summary>
    public static Keep? AttemptKeep(RecordingMode mode, int attemptIndex) => mode switch
    {
        RecordingMode.Off => null,
        RecordingMode.On => Keep.Always,
        RecordingMode.RetainOnFailure => Keep.OnFailure,
        RecordingMode.OnFirstRetry => attemptIndex == 1 ? Keep.Always : null,
        RecordingMode.OnAllRetries => attemptIndex >= 1 ? Keep.Always : null,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    /// <summary>The video an attempt at <paramref name="attemptIndex"/> records under <paramref name="recording"/>. Null records none.</summary>
    public static VideoRecording? AttemptVideo(ResolvedRecording recording, int attemptIndex)
    {
        return AttemptKeep(recording.Mode, attemptIndex) is { } keep
            ? new VideoRecording(keep, recording.Source == RecordingSource.Default ? VideoPolicy.BestEffort : VideoPolicy.Required)
            : null;
    }

    /// <summary>Whether any attempt of a test with <paramref name="retries"/> records under <paramref name="mode"/>: what the engine has to be able to honour before the run starts.</summary>
    public static bool RecordsOnSomeAttempt(RecordingMode mode, int retries) => IsRetryMode(mode) ? retries >= 1 : mode != RecordingMode.Off;
}
