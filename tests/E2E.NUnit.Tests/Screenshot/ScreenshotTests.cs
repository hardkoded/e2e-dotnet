// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.NUnit.Tests.Helpers;
using static E2E.NUnit.Tests.Screenshot.Png;

namespace E2E.NUnit.Tests.Screenshot;

/// <summary>
/// The test outside any <c>describe</c> of upstream's <c>apps/testbed/tests/screenshot.e2e.ts</c> (Playwright's
/// <c>page-screenshot.spec.ts</c>): <c>page screenshot should capture css box-shadow</c>.
/// </summary>
public sealed class ScreenshotTests : ScreenshotTest
{
    [Test]
    public async Task Should_capture_css_box_shadow()
    {
        await Browser.GotoAsync(Content("""<div style="box-shadow: red 10px 10px 10px; width: 50px; height: 50px;"></div>"""));
        var screenshot = Read(await App.ScreenshotAsync());
        Assert.That(Rgb(screenshot, 30, 30), Is.EqualTo((255, 255, 255)));
        Assert.That(Rgb(screenshot, 62, 40).R, Is.EqualTo(255));
        Assert.That(Rgb(screenshot, 62, 40).G, Is.LessThan(200));
        Assert.That(Rgb(screenshot, 100, 100), Is.EqualTo((255, 255, 255)));
    }
}
