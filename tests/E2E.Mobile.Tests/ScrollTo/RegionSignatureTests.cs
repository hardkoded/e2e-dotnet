// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Mobile.Tests.RawNodes;

namespace E2E.Mobile.Tests.ScrollTo;

/// <summary>Upstream's <c>describe('regionSignature')</c> (<c>packages/mobile/tests/unit/scroll-to.test.ts</c>).</summary>
public sealed class RegionSignatureTests
{
    private static IReadOnlyList<ProjectedNode> Projected(params RawNode[] raw)
    {
        var counter = 0;
        return SnapshotProjection.ProjectSnapshot(raw, () => $"n{++counter}", new ViewportSize(390, 844)).Index;
    }

    private static RawNode Row(string label, double y, string type = "cell") =>
        N($"@{label}", index: 0, depth: 0, type: type, label: label, rect: R(0, y, 390, 44));

    [Fact]
    public void Changes_when_a_row_in_the_region_moves_and_not_when_only_a_row_outside_it_does()
    {
        var region = new Rect(0, 100, 390, 400);
        var before = E2E.Mobile.ScrollTo.RegionSignature(Projected(Row("A", 120), Row("Outside", 600)), region);

        Assert.Equal(
            before,
            E2E.Mobile.ScrollTo.RegionSignature(Projected(Row("A", 120), Row("Outside", 640)), region)
        );
        Assert.NotEqual(
            before,
            E2E.Mobile.ScrollTo.RegionSignature(Projected(Row("A", 90), Row("Outside", 600)), region)
        );
    }

    [Fact]
    public void Tells_apart_pages_of_a_gallery_that_differ_only_in_their_test_id()
    {
        var region = new Rect(0, 0, 390, 844);
        RawNode Page(string identifier) => Row("Photo", 100, "image") with { Identifier = identifier };

        Assert.NotEqual(
            E2E.Mobile.ScrollTo.RegionSignature(Projected(Page("photo-1")), region),
            E2E.Mobile.ScrollTo.RegionSignature(Projected(Page("photo-2")), region)
        );
    }

    [Fact]
    public void Leaves_out_a_spinner_which_turns_on_its_own_and_rounds_boxes_to_whole_points()
    {
        var region = new Rect(0, 0, 390, 844);
        var baseline = E2E.Mobile.ScrollTo.RegionSignature(Projected(Row("A", 120)), region);

        Assert.Equal(
            baseline,
            E2E.Mobile.ScrollTo.RegionSignature(
                Projected(Row("A", 120.3), Row("Loading", 300, "progress-indicator")),
                region
            )
        );
    }
}
