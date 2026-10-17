// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using Modes = E2E.Internal.RecordingModes;

namespace E2E.Tests.RecordingModes;

/// <summary>How firmly the engine is asked for the video. Upstream's <c>attemptVideo</c> describe.</summary>
public sealed class AttemptVideoTests
{
    [Fact]
    public void Records_under_the_same_keep_rule_best_effort_for_a_default_mode_and_required_for_one_somebody_set()
    {
        Assert.Equal(
            new VideoRecording(Keep.OnFailure, VideoPolicy.Required),
            Modes.AttemptVideo(new ResolvedRecording(RecordingMode.RetainOnFailure, RecordingSource.Run), 0));
        Assert.Null(Modes.AttemptVideo(new ResolvedRecording(RecordingMode.Off, RecordingSource.Run), 0));
        Assert.Equal(VideoPolicy.BestEffort, Modes.AttemptVideo(new ResolvedRecording(RecordingMode.On, RecordingSource.Default), 0)?.Policy);
        foreach (var source in new[] { RecordingSource.Run, RecordingSource.Target, RecordingSource.Test })
        {
            Assert.Equal(VideoPolicy.Required, Modes.AttemptVideo(new ResolvedRecording(RecordingMode.On, source), 0)?.Policy);
        }
    }
}
