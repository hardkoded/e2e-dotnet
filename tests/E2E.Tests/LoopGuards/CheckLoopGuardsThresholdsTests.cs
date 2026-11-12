// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.LoopGuards;

/// <summary>Upstream's "checkLoopGuards thresholds" describe (loop-guards.test.ts).</summary>
public sealed class CheckLoopGuardsThresholdsTests
{
    [Fact]
    public void Honors_caller_thresholds_over_the_defaults()
    {
        var calls = new[] { ("tap", """{"target":"n1"}"""), ("tap", """{"target":"n1"}""") };
        var defaults = E2E.LoopGuards.DefaultLoopGuardThresholds;

        Assert.Equal((false, null), E2E.LoopGuards.CheckLoopGuards(calls));

        var warned = E2E.LoopGuards.CheckLoopGuards(calls, defaults with { RepeatWarn = 2 });
        Assert.False(warned.Stop);
        Assert.NotNull(warned.Reason);

        Assert.True(E2E.LoopGuards.CheckLoopGuards(calls, defaults with { RepeatWarn = 1, RepeatStop = 2 }).Stop);
    }
}
