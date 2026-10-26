// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>What <see cref="ObservationBudget.ObservationByteBudget"/> reads of the agent's limits (upstream's <c>ObservationBudgetLimits</c>).</summary>
/// <param name="MaxObservationBytes"><c>agent.maxObservationBytes</c>: the configured ceiling.</param>
/// <param name="MaxInputTokens"><c>agent.maxInputTokens</c>: the per-call ceiling the model pre-flight checks.</param>
internal readonly record struct ObservationBudgetLimits(int MaxObservationBytes, int MaxInputTokens);

/// <summary>What <see cref="ObservationBudget.ObservationByteBudget"/> reads of the request (upstream's <c>ObservationBudgetRequest</c>).</summary>
/// <param name="FixedBytes">Bytes the rest of the request already costs: system message, ledger, context.</param>
/// <param name="InstructionBytes">Bytes of the caller's instruction as the prompt carries it; 0 when it is not sent with the observation.</param>
/// <param name="Pixels">Whether a screenshot travels with the tree, reserving its token cost too.</param>
internal readonly record struct ObservationBudgetRequest(int FixedBytes, int InstructionBytes, bool Pixels);

/// <summary>
/// The observation byte budget both agent tiers capture under (upstream's <c>agent/observation-budget.ts</c>). The judgment tier
/// derived it first; the act loop reuses it so a dense page costs the same bounded slice of the per-call token ceiling in either path.
/// </summary>
internal static class ObservationBudget
{
    /// <summary>
    /// Headroom reserved for what travels with the instruction, in the byte-scaled units <see cref="ModelAdapter.TokenUpperBound"/> works in:
    /// the request text, the prompt's markup, and a repair section quoting a rejected response. The instruction itself is measured, not
    /// reserved for: a long extract instruction outgrows any fixed figure, and the pre-flight refuses the request instead of the screen
    /// being cut to fit.
    /// </summary>
    private const int InstructionReserve = 4_096;

    /// <summary>
    /// Headroom reserved for one attached screenshot on a vision call, in the same units. The exact cost is only known once the
    /// observation reports its viewport, which is after the budget has to be fixed, so this reserves for the largest viewport worth
    /// planning for, derived through the same function the pre-flight bills with.
    /// </summary>
    private static readonly int PixelReserve = ModelAdapter.ImageTokenUpperBound(2_560, 1_440);

    /// <summary>Floor below which a clamped budget never falls; a tree this small still names the visible controls.</summary>
    private const int MinObservationBytes = 1_024;

    /// <summary>
    /// Bytes one observation may contribute to a request. <c>agent.maxObservationBytes</c> is the configured ceiling, but the per-call
    /// token limit binds first on a large screen. Deriving the budget from what the rest of the request actually costs makes a big screen
    /// truncate visibly rather than fail the pre-flight check.
    /// </summary>
    public static int ObservationByteBudget(ObservationBudgetLimits limits, ObservationBudgetRequest request)
    {
        var overhead = (long)request.FixedBytes + request.InstructionBytes + InstructionReserve + (request.Pixels ? PixelReserve : 0);
        var withinTokenCeiling = Math.Max(MinObservationBytes, limits.MaxInputTokens - overhead);
        return (int)Math.Min(limits.MaxObservationBytes, withinTokenCeiling);
    }
}
