// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.StepCache;

/// <summary>Upstream's <c>describe('recordedVerdictOf')</c> of <c>step-cache.test.ts</c>.</summary>
public sealed class RecordedVerdictOfTests
{
    [Fact]
    public async Task Keeps_only_the_recorded_verdict_of_a_replay_summary_even_one_that_mentions_a_verdict_itself()
    {
        var context = StepCacheHarness.EntryContext(endPath: "/customers", summary: "saw \"recorded verdict: none\" in the log");
        var session = StepCacheHarness.MakeSession(context, StepCacheHarness.MakeHost(["/pricing", "/customers#top"]));
        var verdict = await session.BeginAsync();

        Assert.Equal("saw \"recorded verdict: none\" in the log", StepTraceSession.RecordedVerdictOf(verdict!.Value.Summary));
    }
}
