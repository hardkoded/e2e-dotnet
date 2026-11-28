// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.SessionTools;
using E2E.Engine;

namespace E2E.Cli.Tests.McpTools;

/// <summary><c>describe('firstScreenLine')</c> of <c>mcp-tools.test.ts</c>.</summary>
public sealed class FirstScreenLineTests
{
    private static ExecutorObservation Observation(string text, bool treeUnavailable = false) =>
        new(text)
        {
            Revision = "b1",
            Path = "/",
            TreeUnavailable = treeUnavailable,
            Viewport = new TraceViewport(1280, 720),
        };

    [Fact]
    public void Names_the_screen_says_results_are_changes_against_it_and_points_at_observe()
    {
        Assert.Equal(
            "Screen: revision b1, path /, 2 nodes, not listed. Action results report changes against it; call {tool: \"observe\"} lists its nodes.",
            Tools.FirstScreenLine(new ScreenPresenter(), Observation("#n1 document \"Home\"\n #n2 button \"Save\""))
        );
    }

    [Fact]
    public void Points_at_screenshot_when_the_screen_has_no_tree_to_list()
    {
        Assert.Equal(
            "Screen: revision b1, path /, semantic capture unavailable; call {tool: \"screenshot\"} shows it.",
            Tools.FirstScreenLine(
                new ScreenPresenter(),
                Observation("[semantic capture unavailable: no nodes were read]", treeUnavailable: true)
            )
        );
    }
}
