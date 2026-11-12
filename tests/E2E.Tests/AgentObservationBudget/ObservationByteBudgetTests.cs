// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.AgentObservationBudget;

/// <summary>Upstream's <c>describe('observationByteBudget')</c> (<c>tests/unit/agent-observation-budget.test.ts</c>).</summary>
public sealed class ObservationByteBudgetTests
{
    private const int InstructionReserve = 4_096;

    private static readonly ObservationBudgetLimits Limits = new(262_144, 200_000);

    private static readonly int PixelReserve = ModelAdapter.ImageTokenUpperBound(2_560, 1_440);

    [Fact]
    public void Returns_the_configured_ceiling_when_the_request_leaves_room_for_it()
    {
        Assert.Equal(
            262_144,
            ObservationBudget.ObservationByteBudget(Limits with { MaxInputTokens = 1_000_000 }, new ObservationBudgetRequest(10_000, 0, false)));
    }

    [Fact]
    public void Clamps_to_the_per_call_token_ceiling_minus_the_request_overhead()
    {
        const int fixedBytes = 100_000;
        Assert.Equal(
            Limits.MaxInputTokens - fixedBytes - InstructionReserve,
            ObservationBudget.ObservationByteBudget(Limits, new ObservationBudgetRequest(fixedBytes, 0, false)));
    }

    [Fact]
    public void Cuts_the_screen_by_the_measured_instruction_not_a_fixed_reserve()
    {
        const int fixedBytes = 100_000;
        var shortBudget = ObservationBudget.ObservationByteBudget(Limits, new ObservationBudgetRequest(fixedBytes, 100, false));
        var longBudget = ObservationBudget.ObservationByteBudget(Limits, new ObservationBudgetRequest(fixedBytes, 7_888, false));

        Assert.Equal(7_788, shortBudget - longBudget);
        Assert.Equal(Limits.MaxInputTokens - fixedBytes - 7_888 - InstructionReserve, longBudget);
    }

    [Fact]
    public void Reserves_the_largest_planned_screenshot_on_a_vision_request()
    {
        const int fixedBytes = 100_000;
        var text = ObservationBudget.ObservationByteBudget(Limits, new ObservationBudgetRequest(fixedBytes, 0, false));
        var vision = ObservationBudget.ObservationByteBudget(Limits, new ObservationBudgetRequest(fixedBytes, 0, true));

        Assert.Equal(PixelReserve, text - vision);
    }

    [Fact]
    public void Never_falls_below_the_floor_a_tree_can_still_name_controls_under()
    {
        Assert.Equal(1_024, ObservationBudget.ObservationByteBudget(Limits, new ObservationBudgetRequest(1_000_000, 0, true)));
    }
}
