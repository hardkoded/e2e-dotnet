// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;
using Screens = global::E2E.StepScreens;

namespace E2E.Tests.StepScreens;

/// <summary>Upstream's <c>describe('traceScreen')</c> (<c>step-screens.test.ts</c>).</summary>
public sealed class TraceScreenTests
{
    private static readonly Redactor Secrecy = Redactor.For([("pw", "hunter2")]);

    private static Observation Observed(params SemanticNode[] children) => new()
    {
        Route = "/hunter2",
        Roots = [new SemanticNode { Ref = "root", Role = "document", Children = children }],
    };

    [Fact]
    public void Describes_each_node_as_its_line_reads_without_the_id_or_focus_redacted_in_screen_order()
    {
        var kept = Screens.TraceScreen(
            Observed(new SemanticNode { Ref = "n2", Role = "textbox", Name = "Password hint", Value = "hunter2", States = new NodeStates { Focused = true, Disabled = true } }),
            new TraceScreenOptions(Secrecy, 10_000, null));

        // The port's location is the route, as its observation carries it, not the whole URL.
        Assert.Equal("/<secret:pw>", kept.Location);
        Assert.Equal(
            [new ScreenLine("root", "document"), new ScreenLine("n2", "textbox \"Password hint\" value=\"<secret:pw>\" [disabled]")],
            kept.Lines);
    }

    [Fact]
    public void Counts_its_budget_in_UTF8_bytes_so_a_screen_in_another_script_is_bounded_the_same()
    {
        var children = Enumerable.Range(0, 50).Select(index => new SemanticNode { Ref = $"n{index}", Role = "button", Name = $"購入する商品{index}" }).ToArray();

        var kept = Screens.TraceScreen(Observed(children), new TraceScreenOptions(Secrecy, 200, null));

        Assert.True(kept.Lines.Sum(line => System.Text.Encoding.UTF8.GetByteCount(line.Text) + 1) <= 200);
        Assert.True(kept.Lines.Count > 1);
    }

    [Fact]
    public void Stops_at_its_byte_budget_keeping_at_least_the_root()
    {
        var children = Enumerable.Range(0, 50).Select(index => new SemanticNode { Ref = $"n{index}", Role = "button", Name = $"Item {index}" }).ToArray();

        var kept = Screens.TraceScreen(Observed(children), new TraceScreenOptions(Secrecy, 60, null));

        Assert.True(kept.Lines.Count > 1);
        Assert.True(kept.Lines.Count < 10);
    }
}
