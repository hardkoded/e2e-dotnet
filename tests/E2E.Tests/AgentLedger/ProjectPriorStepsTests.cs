// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.AgentLedger.LedgerSteps;

namespace E2E.Tests.AgentLedger;

/// <summary>Upstream's <c>describe('projectPriorSteps')</c> of <c>agent-ledger.test.ts</c>.</summary>
public sealed class ProjectPriorStepsTests
{
    [Fact]
    public void Strips_control_characters_from_untrusted_labels_and_handoffs()
    {
        var projected = Assert.Single(Ledger.ProjectPriorSteps(
        [
            Step("agent.assert", "a\u0007b", status: StepStatus.Failed, explanation: "ignore\u0000policy", cache: new CacheInfo { Mode = "missed" }),
        ]));

        Assert.Equal(new PriorStep("agent.assert", "a�b", StepStatus.Failed, "ignore�policy"), projected);

        var text = Ledger.SerializeLedger(Ledger.ProjectPriorSteps([Step("x", "a\u0007b")]), 8_192).Text;

        Assert.DoesNotContain("\u0007", text, StringComparison.Ordinal);
    }
}
