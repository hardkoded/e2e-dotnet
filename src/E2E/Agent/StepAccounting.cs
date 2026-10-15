// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E;

/// <summary>
/// Upstream's <c>StepAccounting</c> (<c>agent/step-accounting.ts</c>): what one act step may spend and what it did. The
/// action slots are drawn on by replayed and live actions alike. <see cref="Recorded"/> holds the actions the step took,
/// in order, for its cache entry; the port has no step record, so the accounting keeps them.
/// </summary>
internal sealed class StepAccounting(int maxActions, int maxModelCalls)
{
    public int MaxModelCalls => maxModelCalls;

    public List<RecordedAction> Recorded { get; } = [];

    public int Used { get; private set; }

    public bool Exhausted { get; private set; }

    public string Message => "agent.act exhausted its action budget of " + maxActions.ToString(CultureInfo.InvariantCulture);

    public bool TryReserve()
    {
        if (Used >= maxActions)
        {
            Exhausted = true;
            return false;
        }

        Used++;
        return true;
    }

    public AgentException Stop(string? summary)
    {
        return new AgentException("STEP_BUDGET_EXHAUSTED", string.IsNullOrEmpty(summary) ? Message + "." : Message + ": " + summary, blocked: true);
    }
}
