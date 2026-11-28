// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Mobile.Tests.ScrollTo;

/// <summary>Upstream's <c>describe('above')</c> (<c>packages/mobile/tests/unit/scroll-to.test.ts</c>).</summary>
public sealed class AboveTests
{
    private static readonly Rect Region = new(0, 100, 400, 800);

    [Fact]
    public void Cuts_the_region_off_at_the_keyboard_and_leaves_it_whole_with_none_or_one_below_it()
    {
        Assert.Equal(Region with { Height = 400 }, E2E.Mobile.ScrollTo.Above(Region, 500));
        Assert.Equal(Region, E2E.Mobile.ScrollTo.Above(Region, null));
        Assert.Equal(Region, E2E.Mobile.ScrollTo.Above(Region, 1_000));
    }
}
