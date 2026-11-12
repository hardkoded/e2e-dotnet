// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;
using E2E.Tests.AgentLedger;

namespace E2E.Tests.AgentDerived;

/// <summary>.NET-only: the spelling of <c>derived</c> in a step's <c>cache</c> of <c>report.json</c> (the enum's wire form, upstream's <c>step.cache.derived</c>).</summary>
public sealed class DerivedReportWireTests
{
    [Theory]
    [InlineData(DerivedReason.Pixels, "pixels")]
    [InlineData(DerivedReason.WholeNode, "whole-node")]
    [InlineData(DerivedReason.MintedToken, "minted-token")]
    [InlineData(DerivedReason.Date, "date")]
    public void Writes_the_rule_a_replay_stopped_at_a_gap_reports(DerivedReason reason, string spelled)
    {
        var step = LedgerSteps.Step("agent.act", "type the code", cache: new CacheInfo { Mode = "missed", Reason = "gap", Derived = reason, TotalActions = 2 });

        Assert.Equal(spelled, (string?)ReportWire.Step(step)["cache"]!["derived"]);
    }

    [Fact]
    public void Leaves_derived_out_for_a_gap_at_a_project_tool()
    {
        var step = LedgerSteps.Step("agent.act", "seed", cache: new CacheInfo { Mode = "missed", Reason = "gap", TotalActions = 2 });

        Assert.Null(ReportWire.Step(step)["cache"]!.AsObject()["derived"]);
    }
}
