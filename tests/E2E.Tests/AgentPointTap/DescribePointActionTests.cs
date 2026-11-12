// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.AgentPointTap;

/// <summary>
/// Upstream's <c>describe('describePointAction')</c> (<c>agent-point-tap.test.ts</c>). A node reads as the port's own snapshot line, so
/// the expected text carries that line in place of upstream's.
/// </summary>
public sealed class DescribePointActionTests
{
    private static readonly PointTap.NodeProse Prose = node => SnapshotText.FormatNode(node, 0, Redactor.None, null).TrimStart();

    [Fact]
    public void Names_the_control_the_point_resolved_to_by_the_line_the_model_already_reads()
    {
        var save = PointTapScreen.Find("n2");
        var text = PointTap.DescribePointAction("tapAt", new Point(50, 30), save, save, true, Prose);
        Assert.Equal("Tapped " + Prose(save) + ", the control at (50, 30).", text);
    }

    [Fact]
    public void Names_the_control_a_bare_point_landed_inside()
    {
        var save = PointTapScreen.Find("n2");
        var text = PointTap.DescribePointAction("tapAt", new Point(50, 30), save, save, false, Prose);
        Assert.Equal("Tapped the point (50, 30) inside " + Prose(save) + ".", text);
    }

    [Fact]
    public void Says_a_bare_point_landed_on_nothing_listed_with_what_sits_under_it()
    {
        var paragraph = PointTapScreen.Find("n5");
        var text = PointTap.DescribePointAction("tapAt", new Point(640, 120), null, paragraph, false, Prose);
        Assert.Equal("Tapped the point (640, 120); no listed control is there (under it: " + Prose(paragraph) + ").", text);
    }

    [Fact]
    public void Falls_back_to_the_id_when_the_line_was_cut_from_the_text()
    {
        Assert.Equal("#n9", PointTap.NodeLine("#n1 document", "n9"));
    }
}
