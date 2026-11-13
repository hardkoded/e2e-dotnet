// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.NewTabFlow;

/// <summary>Ported from <c>describe('new tab flow')</c> in upstream's <c>apps/web-benchmark/tests/new-tab-flow.e2e.ts</c>.</summary>
public sealed class NewTabFlowTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheNewTabFlow() => await App.OpenAsync("/e/new-tab-flow");

    [Test]
    public async Task Reads_the_code_in_the_tab_the_app_opens_and_enters_it_back_in_the_original_tab()
    {
        var verification = await Browser.WaitForPopupAsync(() => Screen.GetByTestId("open-tab-button").TapAsync());
        await Expect.That(Browser).ToHaveTitleAsync("Verification");
        var code = await Screen.GetByText(new Regex(@"^TAB-\d+$")).TextContentAsync();
        await verification.CloseAsync();
        await Screen.GetByTestId("code-input").FillAsync(code ?? "");
        await Screen.GetByTestId("verify-button").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Verified successfully");
    }

    [Test]
    public async Task A_tap_that_opens_a_tab_leaves_the_screen_on_the_original_tab_and_activate_shows_the_new_one()
    {
        await Screen.GetByTestId("open-tab-button").TapAsync();
        await Screen.GetByTestId("code-input").FillAsync("TAB-00000");
        await Screen.GetByTestId("verify-button").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("Wrong verification code");
        await Expect.Poll(() => Browser.Tabs().Count).ToBeAsync(2);
        var tabs = Browser.Tabs();
        var (original, verification) = (tabs[0], tabs[1]);
        Assert.That(original.IsActive, Is.True);
        await verification.ActivateAsync();
        await Expect.That(Browser).ToHaveTitleAsync("Verification");
    }
}
