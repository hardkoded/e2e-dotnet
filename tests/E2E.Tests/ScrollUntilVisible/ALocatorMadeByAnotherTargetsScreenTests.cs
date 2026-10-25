// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.ScrollUntilVisible;

/// <summary>
/// Upstream's <c>describe("a locator made by another target's screen")</c> (<c>tests/unit/scroll-until-visible.test.ts</c>). Not
/// ported: <c>is refused by dragTo</c>, because the port has no <c>dragTo</c> (COMPATIBILITY, Locators).
/// </summary>
public sealed class ALocatorMadeByAnotherTargetsScreenTests
{
    private const string Foreign = "the one given belongs to another target's screen or another attempt";

    private static (ScrollScreen Web, ScrollScreen Mobile) Targets() =>
        (new ScrollScreen(new ScrollScript { Screens = [[ScrollScreen.Target]] }), new ScrollScreen(new ScrollScript { Screens = [[ScrollScreen.Target]] }));

    [Fact]
    public async Task Is_refused_by_scrollUntilVisible_before_any_step_on_either_target()
    {
        var (web, mobile) = Targets();
        var failure = await Assert.ThrowsAsync<TestException>(() => web.Screen.ScrollUntilVisibleAsync(mobile.Screen.GetByText("Accept")));
        Assert.Equal("INVALID_LOCATOR", failure.Code);
        Assert.Equal("scrollUntilVisible requires a locator made by this screen; " + Foreign, failure.Message);
        Assert.Empty(web.Steps.All());
        Assert.Empty(web.Swipes);
        Assert.Empty(mobile.Swipes);
    }

    [Fact]
    public void Is_refused_by_filter_has()
    {
        var (web, mobile) = Targets();
        var failure = Assert.Throws<TestException>(() => web.Screen.GetByRole("list").Filter(mobile.Screen.GetByText("Accept")));
        Assert.Equal("INVALID_LOCATOR", failure.Code);
        Assert.Equal("filter({ has }) requires a locator made by this screen; " + Foreign, failure.Message);
    }
}
