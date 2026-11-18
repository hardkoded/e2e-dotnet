// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Decision.Tests.Overlay.OverlayHelpers;

namespace E2E.Decision.Tests.Overlay;

/// <summary><c>describe('regionHash')</c> of <c>overlay.test.ts</c>.</summary>
public sealed class RegionHashTests
{
    [Fact]
    public void Changes_only_when_the_region_around_the_point_changes()
    {
        var baseline = White(400, 300);
        var dot = White(400, 300);
        var png = PngHelpers.RgbaOf(dot.Data);
        png.Data[((10 * 400) + 10) * 4] = 0;
        var dotted = dot with { Data = PngHelpers.PngOf(400, 300, png.Data) };
        Assert.NotEqual(
            global::E2E.Decision.Overlay.RegionHash(baseline, new Point(20, 20), 50),
            global::E2E.Decision.Overlay.RegionHash(dotted, new Point(20, 20), 50)
        );
        Assert.Equal(
            global::E2E.Decision.Overlay.RegionHash(baseline, new Point(350, 250), 50),
            global::E2E.Decision.Overlay.RegionHash(dotted, new Point(350, 250), 50)
        );
        Assert.Null(global::E2E.Decision.Overlay.RegionHash(null, new Point(0, 0), 50));
    }
}
