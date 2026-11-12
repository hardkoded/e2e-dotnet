// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.StickyChrome;

/// <summary>Ported from upstream's <c>apps/web-benchmark/tests/sticky-chrome.e2e.ts</c>: its one test is outside any <c>describe</c>.</summary>
public sealed class StickyChromeTests : BenchmarkTest
{
    [Test]
    public async Task Accepts_the_terms_once_the_button_clears_the_sticky_footer()
    {
        await App.OpenAsync("/e/sticky-chrome");
        await Expect.That(Screen.GetByTestId("sticky-header")).ToHaveTextAsync("Terms of Service");
        await Expect.That(Screen.GetByTestId("success-message")).ToBeHiddenAsync();

        var accept = Screen.GetByRole("button", "Accept terms");
        await accept.ScrollIntoViewAsync();
        await accept.TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Terms accepted");

        var button = await accept.BoundingBoxAsync();
        var footer = await Screen.GetByTestId("sticky-footer").BoundingBoxAsync();
        if (button is null || footer is null)
        {
            throw new InvalidOperationException("the button or the footer has no box");
        }

        Assert.That(button.Y + button.Height, Is.LessThanOrEqualTo(footer.Y));
    }
}
