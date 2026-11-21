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
        var calls = new[]
        {
            new GuardToolCall("tap", """{"target":"n1"}"""),
            new GuardToolCall("tap", """{"target":"n1"}"""),
        };
        var defaults = E2E.LoopGuards.DefaultLoopGuardThresholds;

        Assert.Equal(LoopGuardKind.Clear, E2E.LoopGuards.CheckLoopGuards(calls).Kind);

        var warned = E2E.LoopGuards.CheckLoopGuards(calls, defaults with { RepeatWarn = 2 });
        Assert.Equal(LoopGuardKind.Warn, warned.Kind);
        Assert.NotNull(warned.Reason);

        Assert.Equal(
            LoopGuardKind.Stop,
            E2E.LoopGuards.CheckLoopGuards(calls, defaults with { RepeatWarn = 1, RepeatStop = 2 }).Kind
        );
    }
}
