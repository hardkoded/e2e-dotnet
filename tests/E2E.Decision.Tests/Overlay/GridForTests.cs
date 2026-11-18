// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Decision.Tests.Overlay;

/// <summary><c>describe('gridFor')</c> of <c>overlay.test.ts</c>.</summary>
public sealed class GridForTests
{
    [Fact]
    public void Fits_at_most_ten_columns_and_rows_near_the_target_cell_size()
    {
        Assert.Equal(new Grid(10, 9, 128, 80), global::E2E.Decision.Overlay.GridFor(1280, 720, 80));
        Assert.Equal(new Grid(10, 8, 80, 75), global::E2E.Decision.Overlay.GridFor(800, 600, 80));
        Assert.Equal(new Grid(1, 1, 50, 50), global::E2E.Decision.Overlay.GridFor(50, 50, 80));
    }

    [Fact]
    public void Names_the_center_of_a_scored_position_inside_the_image()
    {
        var grid = global::E2E.Decision.Overlay.GridFor(800, 600, 80);
        Assert.Equal(new Point(240, 263), global::E2E.Decision.Overlay.PointOf(grid, 2.5, 3));
        Assert.Equal(new Point(799, 0), global::E2E.Decision.Overlay.PointOf(grid, 20, -5));
    }
}
