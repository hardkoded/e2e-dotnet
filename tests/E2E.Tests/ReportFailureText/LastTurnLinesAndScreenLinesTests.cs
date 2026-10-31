// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.ReportFailureText.FailureTextKit;

namespace E2E.Tests.ReportFailureText;

/// <summary>Upstream's <c>describe('lastTurnLines and screenLines')</c> (<c>tests/unit/report-failure-text.test.ts</c>).</summary>
public sealed class LastTurnLinesAndScreenLinesTests
{
    [Fact]
    public void Shows_the_last_three_turns_without_the_verdict_turn_and_names_the_location_as_a_path_when_it_is_loopback_the_nodes_closest_to_a_failed_locator_capped()
    {
        List<StepTurn> turns = [.. Enumerable.Range(1, 5).Select(index => new StepTurn(index, ["tap({\"target\":\"n" + index + "\"})"], "Tapped #n" + index + ".\n\nScreen changes: 1 changed.")), new StepTurn(6, ["complete_step({\"status\":\"failed\"})"], "Step concluded.")];

        Assert.Equal(
            [
                "Turn 3: `tap({\"target\":\"n3\"})` → Tapped #n3.",
                "Turn 4: `tap({\"target\":\"n4\"})` → Tapped #n4.",
                "Turn 5: `tap({\"target\":\"n5\"})` → Tapped #n5.",
            ],
            FailureText.LastTurnLines(Step(1, StepKind.Agent, "agent.act", StepStatus.Failed, turns: turns)));
        Assert.Equal(["Turn 1: no tool call → nothing"], FailureText.LastTurnLines(Step(1, StepKind.Agent, "agent.act", StepStatus.Failed, turns: [new StepTurn(1, [], "nothing")])));
        Assert.Empty(FailureText.LastTurnLines(Step(1)));

        var told = Told(new E2E.FailureEvidence { Url = "http://app.test/todos", Candidates = ["#n1 button \"Add\"", "#n2 button \"Add all\"", "#n3 link \"Todos\"", "#n4 text=\"Add\""] });
        Assert.Equal(["Screen: `http://app.test/todos`", "Closest to the locator: `#n1 button \"Add\"`, `#n2 button \"Add all\"`, `#n3 link \"Todos\"`, and 1 more"], FailureText.ScreenLines(told));
        Assert.Equal(["Screen: `/todos?filter=open#top`"], FailureText.ScreenLines(Told(new E2E.FailureEvidence { Url = "http://localhost:3000/todos?filter=open#top" })));
        Assert.Empty(FailureText.ScreenLines(Told(null)));
    }

    private static ReportAttempt Told(E2E.FailureEvidence? failure)
    {
        var result = Result(attempts: [Failed(failure: failure)]);
        return FailureText.ToldAttempt(result, Outcome.Of(result));
    }
}
