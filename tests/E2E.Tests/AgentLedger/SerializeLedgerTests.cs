// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using static E2E.Tests.AgentLedger.LedgerSteps;

namespace E2E.Tests.AgentLedger;

/// <summary>Upstream's <c>describe('serializeLedger')</c> of <c>agent-ledger.test.ts</c>.</summary>
public sealed class SerializeLedgerTests
{
    [Fact]
    public void Presents_entries_in_chronological_order()
    {
        var text = Ledger.SerializeLedger(
            Ledger.ProjectPriorSteps([Step("app.open", "/", StepKind.App), Step("agent.act", "the buy button")]),
            8_192).Text;

        Assert.True(text.IndexOf("app.open", StringComparison.Ordinal) < text.IndexOf("agent.act", StringComparison.Ordinal));
        Assert.Contains("1. app.open passed :: /", text, StringComparison.Ordinal);
        Assert.Contains("2. agent.act passed :: the buy button", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Drops_the_oldest_entries_and_reports_how_many_were_omitted()
    {
        var steps = Enumerable.Range(0, 40).Select(index => Step("agent.act", $"target-{index}")).ToList();

        var (text, bytes) = Ledger.SerializeLedger(Ledger.ProjectPriorSteps(steps), 200);

        Assert.True(bytes <= 200 + text.Split('\n')[0].Length);
        Assert.Matches(@"^\[\d+ earlier step\(s\) omitted\]$", text.Split('\n')[0]);
        Assert.Contains("target-39", text, StringComparison.Ordinal);
        Assert.DoesNotContain("target-0\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Quotes_the_step_explanation_as_a_bounded_handoff()
    {
        var text = Ledger.SerializeLedger(
            Ledger.ProjectPriorSteps([Step("agent.assert", "x", explanation: new string('y', 2_000))]),
            65_536).Text;

        var handoff = Regex.Match(text, "observed: (y+)").Groups[1].Value;

        Assert.Equal(Ledger.MaxHandoffBytes, handoff.Length);
    }

    [Fact]
    public void Hands_later_steps_the_recorded_verdict_of_a_replayed_step_not_the_replay_notice()
    {
        const string explanation = "replayed 2 recorded action(s) zero-turn from the trace cache; recorded verdict: opened the customers page";
        var replayed = Step("agent.act", "open customers", explanation: explanation, cache: new CacheInfo { Mode = "self-finalized", ReplayedActions = 2, TotalActions = 2 });

        // Projection is the trust boundary where records become model input; the notice goes there.
        var text = Ledger.SerializeLedger(Ledger.ProjectPriorSteps([replayed]), 65_536).Text;

        Assert.Contains("opened the customers page", text, StringComparison.Ordinal);
        Assert.DoesNotContain("replayed 2 recorded", text, StringComparison.Ordinal);

        // A step the executor ran keeps its explanation as written, whatever it starts with.
        var ran = Step("agent.act", "open customers", explanation: explanation, cache: new CacheInfo { Mode = "missed" });

        Assert.Contains("replayed 2 recorded", Ledger.SerializeLedger(Ledger.ProjectPriorSteps([ran]), 65_536).Text, StringComparison.Ordinal);
    }
}
