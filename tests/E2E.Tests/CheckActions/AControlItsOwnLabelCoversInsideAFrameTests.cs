// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;
using static E2E.Tests.CheckActions.CheckPage;
using static E2E.Tests.CheckActions.CoveredControl;

namespace E2E.Tests.CheckActions;

/// <summary>A control its own label covers, inside a frame. Ported from upstream <c>check-actions.test.ts</c>.</summary>
[Collection(BrowserCollection.Name)]
public sealed class AControlItsOwnLabelCoversInsideAFrameTests
{
    [Fact]
    public Task Checks_an_Element_Plus_radio_button() => RunWithScreenAsync(
        "<p>Outer</p><iframe srcdoc=\"" + ElementPlusPage.Replace("\"", "&quot;", StringComparison.Ordinal) + "\"></iframe>",
        async session =>
        {
            var locator = session.Browser.FrameLocator("iframe").Locator("#install");
            await locator.CheckAsync();
            Assert.True(await locator.IsCheckedAsync());
        });
}
