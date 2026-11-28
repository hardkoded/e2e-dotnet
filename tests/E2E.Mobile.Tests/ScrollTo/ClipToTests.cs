// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Mobile.Tests.ScrollTo;

/// <summary>Upstream's <c>describe('clipTo')</c> (<c>packages/mobile/tests/unit/scroll-to.test.ts</c>).</summary>
public sealed class ClipToTests
{
    [Fact]
    public void Keeps_the_part_of_a_box_on_the_screen_and_nothing_of_one_off_it()
    {
        var screen = new ViewportSize(390, 844);

        Assert.Equal(new Rect(0, 700, 390, 144), E2E.Mobile.ScrollTo.ClipTo(new Rect(-10, 700, 420, 400), screen));
        Assert.Equal(0, E2E.Mobile.ScrollTo.ClipTo(new Rect(0, 900, 390, 40), screen).Height);
    }
}
