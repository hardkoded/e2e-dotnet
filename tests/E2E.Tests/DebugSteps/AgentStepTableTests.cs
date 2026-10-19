// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Tests.Traces;
using Steps = global::E2E.DebugSteps;

namespace E2E.Tests.DebugSteps;

/// <summary>Upstream's <c>describe('agentStepTable')</c> (<c>tests/unit/debug-steps.test.ts</c>): one row per agent step, a <c>via</c> column only when the steps named several models.</summary>
public sealed partial class AgentStepTableTests
{
    private static StepModelInfo Model(string provider, string id, int calls) =>
        new() { Provider = provider, Model = id, Calls = calls, InputTokens = 100 * calls, OutputTokens = 10 * calls };

    private static StepRecord Step(string label, string api = "agent.act", StepModelInfo? model = null)
    {
        var step = ReportBuilders.Step(0, StepKind.Agent, api, label);
        step.DurationMs = 1200;
        step.Model = model;
        return step;
    }

    // Header cells and the rows' cells, split on the table's two-space gutter.
    private static List<string[]> Cells(string table) =>
        [.. table.TrimEnd().Split('\n').Skip(1).Select(line => Gutter().Split(line.Trim()))];

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Gutter();

    // Partial: upstream also reads the steps of a serial group's members, and the port has no serial groups; the third step is a second attempt's.
    [Fact]
    public void Adds_a_via_column_labelled_through_the_shared_helper_when_the_steps_named_several_models()
    {
        var acted = Model("typesafe-ai", "jev", 2);
        var judged = Model("fake-loop", "scripted-loop", 1);

        var table = Steps.AgentStepTable(
        [
            [Step("pay", model: acted), Step("no model call")],
            [Step("confirm", "agent.assert", judged)],
        ]);

        Assert.Matches(@"\A\[e2e debug\] agent steps \(execution order\)\n", table);
        var cells = Cells(table);
        Assert.Equal(["step", "via", "total", "model", "observe", "action", "calls", "tokens in/out", "cached", "cost"], cells[0]);
        Assert.Equal(
            [["agent.act \"pay\"", "typesafe-ai/jev"], ["agent.act \"no model call\"", "-"], ["agent.assert \"confirm\"", "fake-loop/scripted-loop"]],
            cells.Skip(1).Select(row => row[..2]));
        Assert.Equal("typesafe-ai/jev", ReportFormat.StepModelLabel(acted));
    }

    [Fact]
    public void Shows_a_dash_for_a_step_whose_model_record_lacks_provenance_and_never_prints_undefined()
    {
        var table = Steps.AgentStepTable(
        [
            [
                Step("pay", model: Model("typesafe-ai", "jev", 2)),
                Step("judge", model: new StepModelInfo { Calls = 1, InputTokens = 100, OutputTokens = 10 }),
                Step("confirm", model: Model("fake-loop", "scripted-loop", 1)),
            ],
        ]);

        var rows = Cells(table).Skip(1).ToList();
        Assert.Equal(["typesafe-ai/jev", "-", "fake-loop/scripted-loop"], rows.Select(row => row[1]));
        Assert.DoesNotContain("undefined", table, StringComparison.Ordinal);
        Assert.DoesNotContain("null", table, StringComparison.Ordinal);
    }
}
