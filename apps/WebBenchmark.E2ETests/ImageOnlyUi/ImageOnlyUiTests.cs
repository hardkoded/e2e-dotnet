// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.ImageOnlyUi;

/// <summary>
/// Ported from <c>describe('image only ui')</c> in upstream's <c>apps/web-benchmark/tests/image-only-ui.e2e.ts</c>. Every label is a PNG and
/// the buttons are unnamed inline SVGs, so the web-only CSS locator is the one handle the DOM offers: bolt, star, heart in document order.
/// </summary>
public sealed class ImageOnlyUiTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheImageOnlyUi()
    {
        await App.OpenAsync("/e/image-only-ui");
        await Expect.That(Browser.Locator("svg")).ToHaveCountAsync(3);
    }

    [Test]
    public async Task The_icons_have_no_names_so_the_bare_svg_locator_is_ambiguous()
    {
        var error = await Support.FailureAsync(() => Browser.Locator("svg").TapAsync());
        Assert.That(error, Is.InstanceOf<E2EException>().And.Property(nameof(E2EException.Code)).EqualTo("LOCATOR_AMBIGUOUS"));
    }

    [Test]
    public async Task A_wrong_icon_shows_the_error_image_and_keeps_the_icons()
    {
        await Browser.Locator("svg").Nth(0).TapAsync();
        await Expect.That(Browser.Locator("img")).ToHaveCountAsync(2);
        await Expect.That(Browser.Locator("svg")).ToHaveCountAsync(3);
    }

    [Test]
    public async Task Tapping_the_star_then_the_heart_verifies_the_icons()
    {
        var icons = Browser.Locator("svg");
        await icons.Nth(1).TapAsync();
        await icons.Nth(2).TapAsync();
        await Expect.That(icons).ToHaveCountAsync(0);
        await Expect.That(Browser.Locator("img")).ToHaveCountAsync(1);
    }
}
