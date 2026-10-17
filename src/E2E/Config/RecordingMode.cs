// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// Which attempts of a test record a trace or a video. Upstream <c>RecordingMode</c>. In
/// <c>e2e.config.json</c> and in <c>E2E_TRACE</c> and <c>E2E_VIDEO</c> a mode is written
/// <c>off</c>, <c>on</c>, <c>retain-on-failure</c>, <c>on-first-retry</c>, or <c>on-all-retries</c>.
/// </summary>
public enum RecordingMode
{
    /// <summary>Record nothing.</summary>
    Off,

    /// <summary>Record every attempt and keep it.</summary>
    On,

    /// <summary>Record every attempt and keep one that did not pass.</summary>
    RetainOnFailure,

    /// <summary>Record the first retry only.</summary>
    OnFirstRetry,

    /// <summary>Record every retry.</summary>
    OnAllRetries,
}

/// <summary>
/// Where an effective <see cref="RecordingMode"/> was set. Upstream <c>RecordingSource</c>.
/// A <see cref="Run"/> mode (the config root or the environment variable) applies to every engine that can record.
/// A <see cref="Target"/> or <see cref="Test"/> mode was asked for by name, so the engine must be able to record it.
/// </summary>
public enum RecordingSource
{
    /// <summary>Nobody set it.</summary>
    Default,

    /// <summary>The config root, or <c>E2E_TRACE</c> and <c>E2E_VIDEO</c>, the port's counterpart of <c>--trace</c> and <c>--video</c>.</summary>
    Run,

    /// <summary>The target's own <c>trace</c> or <c>video</c> key.</summary>
    Target,

    /// <summary>A setting made for one test.</summary>
    Test,
}

/// <summary>One effective mode and where it came from. Upstream <c>ResolvedRecording</c>.</summary>
/// <param name="Mode">Which attempts record.</param>
/// <param name="Source">Where <paramref name="Mode"/> was set.</param>
public sealed record ResolvedRecording(RecordingMode Mode, RecordingSource Source);
