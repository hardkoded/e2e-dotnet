// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.TraceAnchors.AnchorFixtures;

namespace E2E.Tests.TraceAnchors;

/// <summary><c>deltaMismatch failures</c>: the anchors that kept the recorded end state from holding.</summary>
public sealed class DeltaMismatchFailuresTests
{
    [Fact]
    public void Names_each_anchor_that_failed_by_the_way_it_failed()
    {
        var saved = Anchor("status", "Marker", text: "saved");
        var itemA = Node("listitem", "Item A");
        var declined = Node("alert", text: "Card declined");

        var mismatch = Anchors.Mismatch(
            new CacheEntry { Appeared = [saved, Anchor("link", "PB-Twin-Alpha")], Gone = [Anchor("listitem", "Item A")] },
            Project([Heading, Row, itemA, declined]),
            Project([Heading, itemA]));

        Assert.NotNull(mismatch);
        Assert.Equal([Shape(saved)], mismatch.Missing.Select(Shape));
        Assert.Equal([Shape(Anchor("listitem", "Item A"))], mismatch.Present.Select(Shape));
        Assert.Equal([Shape(Anchor("alert", text: "Card declined"))], mismatch.UnrecordedAlerts.Select(Shape));
    }
}
