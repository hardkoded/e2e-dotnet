// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.TraceAnchors.AnchorFixtures;

namespace E2E.Tests.TraceAnchors;

/// <summary><c>describeDelta</c>, volatile anchors across both sides of the delta.</summary>
public sealed class VolatileAnchorsAcrossBothSidesOfTheDeltaTests
{
    private const string MintedA = "rf__node-3f9c2a1b-7d4e-4c1a-9b2e-0a1b2c3d4e5f";

    private static readonly SemanticNode Record = Node("row", "Example record");

    private static SemanticNode Clock(string text) => Node(text: text);

    [Fact]
    public void Drops_a_clock_that_appeared_when_the_other_side_holds_a_stable_result()
    {
        var (appeared, gone) = Describe([Heading, Record, Clock("10:00")], [Heading, Clock("09:59")]);

        Assert.Empty(appeared);
        Assert.Equal(["row|Example record"], Shapes(gone));
    }

    [Fact]
    public void Drops_a_clock_that_disappeared_when_the_other_side_holds_a_stable_result()
    {
        var (appeared, gone) = Describe([Heading, Clock("10:00")], [Heading, Node("status", text: "Record created"), Clock("10:01")]);

        Assert.Equal(["status|~Record created"], Shapes(appeared));
        Assert.Empty(gone);
    }

    [Fact]
    public void Drops_a_minted_id_only_anchor_when_the_opposite_side_has_a_stable_result()
    {
        var minted = Node("box", testId: MintedA);

        var (appeared, gone) = Describe([Heading, Record], [Heading, minted]);
        Assert.Empty(appeared);
        Assert.Equal(["row|Example record"], Shapes(gone));

        var (reverseAppeared, reverseGone) = Describe([Heading, minted], [Heading, Record]);
        Assert.Equal(["row|Example record"], Shapes(reverseAppeared));
        Assert.Empty(reverseGone);
    }

    [Fact]
    public void Keeps_the_volatile_anchors_when_no_side_of_the_delta_has_a_stable_one()
    {
        var (appeared, gone) = Describe([Heading, Clock("10:00")], [Heading, Clock("09:59")]);

        Assert.Equal(["|~09:59"], Shapes(appeared));
        Assert.Equal(["|~10:00"], Shapes(gone));
    }

    [Fact]
    public void Keeps_the_count_an_import_reports_when_the_other_side_holds_a_stable_result()
    {
        var before = new[] { Heading, Node("dialog", "Import CSV"), Node("status", text: "0 records imported") };
        var after = new[] { Heading, Node("status", text: "3 records imported") };

        var (appeared, gone) = Describe(before, after);
        Assert.Equal(["status|~3 records imported"], Shapes(appeared));
        Assert.Equal(["dialog|Import CSV"], Shapes(gone));

        var brokenReplay = new[] { Heading, Node("status", text: "0 records imported") };
        var mismatch = Anchors.Mismatch(new CacheEntry { Appeared = appeared, Gone = gone }, Project(brokenReplay), Project(before));

        Assert.NotNull(mismatch);
        Assert.Equal(["status|~3 records imported"], Shapes(mismatch.Missing));
        Assert.Empty(mismatch.Present);
        Assert.Empty(mismatch.UnrecordedAlerts);
    }

    [Fact]
    public void Still_records_an_alert_beside_a_clock()
    {
        var (appeared, gone) = Describe([Heading, Record, Clock("10:00")], [Heading, Node("alert", text: "Delete failed"), Clock("09:59")]);

        Assert.Equal(["alert|~Delete failed"], Shapes(appeared));
        Assert.Equal(["row|Example record"], Shapes(gone));
    }
}
