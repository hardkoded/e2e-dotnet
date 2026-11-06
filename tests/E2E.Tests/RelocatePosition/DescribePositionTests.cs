// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.RelocatePosition;

/// <summary>
/// Ports the <c>describePosition</c> describe of upstream's <c>relocate-position.test.ts</c>: positional recording, twins the description
/// cannot tell apart resolve by place, at the same count only. The port's <see cref="Relocate.DescribePosition"/> takes the observation and the
/// redactor in place of upstream's map of redacted nodes.
/// </summary>
public sealed class DescribePositionTests
{
    internal static SemanticNode Button(string id, string name = "Set up") => new() { Ref = id, Role = "button", Name = name };

    internal static Observation Screen(params SemanticNode[] roots) => new() { Route = "/", Roots = roots };

    [Fact]
    public void Records_where_a_target_stands_among_the_twins_its_description_matches()
    {
        var b = Button("b");
        var cards = Screen(Button("a"), b, Button("c"));

        var position = Relocate.DescribePosition(b, null, cards, Redactor.None);

        Assert.Equal((1, 3), (position?.Index, position?.Of));
    }

    [Fact]
    public void Records_nothing_for_a_target_its_description_already_names_alone()
    {
        var x = Button("x", "Save");
        var cards = Screen(Button("a"), x);

        Assert.Null(Relocate.DescribePosition(x, null, cards, Redactor.None));
    }

    [Fact]
    public void Counts_twins_inside_the_recorded_container_only()
    {
        var d2 = Button("d2", "Delete");
        var rows = Screen(
            new SemanticNode { Ref = "r1", Role = "row", Children = [new SemanticNode { Ref = "t1", Text = "Alpha" }, Button("d1", "Delete")] },
            new SemanticNode { Ref = "r2", Role = "row", Children = [new SemanticNode { Ref = "t2", Text = "Beta" }, d2] });

        Assert.Null(Relocate.DescribePosition(d2, "Beta", rows, Redactor.None));
    }
}
