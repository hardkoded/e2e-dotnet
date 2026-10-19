// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using static E2E.Tests.ReportFailureText.FailureTextKit;

namespace E2E.Tests.ReportFailureText;

/// <summary>Upstream's <c>describe('evidenceOf')</c> (<c>tests/unit/report-failure-text.test.ts</c>).</summary>
public sealed class EvidenceOfTests
{
    [Fact]
    public void Puts_the_failures_own_screenshot_and_screen_text_first_then_one_artifact_per_kind_and_never_a_log_the_failure_did_not_capture()
    {
        ReportArtifact[] artifacts =
        [
            new("a:0", "video", "t/video.webm"),
            new("a:1", "log", "t/transcript.txt"),
            new("a:2", "screenshot", "t/001-assert.png"),
            new("a:3", "screenshot", "t/002-failure.png"),
            new("a:4", "log", "t/screen-at-failure.txt"),
        ];
        var result = Result(attempts: [Failed(artifacts: artifacts, failure: new E2E.FailureEvidence { Screenshot = "a:3", Screen = "a:4" })]);

        var told = FailureText.ToldAttempt(result, Outcome.Of(result));

        Assert.Equal(["a:3", "a:4", "a:0", "a:2"], FailureText.EvidenceOf(told).Select(artifact => artifact.Id));
    }
}
