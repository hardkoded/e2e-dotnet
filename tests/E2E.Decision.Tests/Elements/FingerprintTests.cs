// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Decision.Tests.Elements.ElementsHelpers;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.Elements;

/// <summary><c>describe('fingerprint')</c> of <c>elements.test.ts</c>.</summary>
public sealed class FingerprintTests
{
    private static ExecutorNode[] Page() => [Node("a", "textbox", "Name", value: ""), Node("b", "button", "Add")];

    private static string Prints(string path, IReadOnlyList<ExecutorNode> tree, bool? keyboardVisible = null) =>
        global::E2E
            .Decision.Elements.ActionSpace(
                Context().Ctx,
                new SpaceObservation
                {
                    Path = path,
                    Viewport = new TraceViewport(800, 600),
                    Tree = new ExecutorNode { Id = "root", Children = tree },
                    KeyboardVisible = keyboardVisible,
                },
                true
            )
            .Fingerprint;

    [Fact]
    public void Is_stable_when_only_node_ids_change()
    {
        var first = Prints("/form", Page());
        var relabeled = Page()
            .Select((child, index) => Node("z" + index, child.Role, child.Name, value: child.Value))
            .ToList();
        Assert.Equal(first, Prints("/form", relabeled));
    }

    [Fact]
    public void Moves_on_a_changed_value_or_path()
    {
        var baseline = Prints("/form", Page());
        var edited = new[] { Node("a", "textbox", "Name", value: "Ada"), Page()[1] };
        Assert.NotEqual(baseline, Prints("/form", edited));
        Assert.NotEqual(baseline, Prints("/other", Page()));
    }

    [Fact]
    public void Moves_when_only_the_measured_keyboard_state_changes_and_tells_unmeasured_from_hidden()
    {
        var shown = Prints("/form", Page(), true);
        var hidden = Prints("/form", Page(), false);
        var unmeasured = Prints("/form", Page());
        Assert.NotEqual(shown, hidden);
        Assert.NotEqual(shown, unmeasured);
        Assert.NotEqual(hidden, unmeasured);
    }

    [Fact]
    public void Moves_when_other_nodes_come_into_view_and_holds_for_a_small_shift()
    {
        static ExecutorNode[] Rows(int offset) =>
            [
                .. Enumerable
                    .Range(0, 6)
                    .Select(index =>
                        Node(
                            "r" + index,
                            "button",
                            "Row " + index,
                            rect: new BoundingBox(0, (index * 200) + 50 - offset, 100, 100)
                        )
                    ),
            ];
        Assert.NotEqual(Prints("/list", Rows(600)), Prints("/list", Rows(0)));
        Assert.Equal(Prints("/list", Rows(20)), Prints("/list", Rows(0)));
    }
}
