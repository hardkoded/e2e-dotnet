// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.KeyboardShadowFocus;

/// <summary>Upstream's <c>describe('closed shadow focus')</c>: typing validates the control that has focus inside closed shadow roots.</summary>
[Collection(BrowserCollection.Name)]
public sealed class ClosedShadowFocusTests
{
    private const string Mode = "closed";

    [Theory]
    [InlineData("<button>Stop</button>")]
    [InlineData("<input readonly value=\"kept\">")]
    public async Task Refuses_a_noneditable_focused_leaf(string html)
    {
        var (session, page) = await ShadowFocusPage.OpenAsync();
        await using (session)
        {
            await ShadowFocusPage.FocusShadowAsync(page, Mode, html);

            var error = await Assert.ThrowsAsync<EngineException>(() => session.TypeTextAsync("lost", replace: true, CancellationToken.None));

            Assert.Equal("NOT_ACTIONABLE", error.Code);
            Assert.Null(await ShadowFocusPage.AttributeAsync(page, "data-keys"));
            Assert.Null(await ShadowFocusPage.AttributeAsync(page, "data-value"));
        }
    }

    [Fact]
    public async Task Types_into_a_nested_editable_leaf()
    {
        var (session, page) = await ShadowFocusPage.OpenAsync();
        await using (session)
        {
            await ShadowFocusPage.FocusShadowAsync(page, Mode, "<input value=\"before\">");
            await session.TypeTextAsync("after", replace: true, CancellationToken.None);
            Assert.Equal("after", await ShadowFocusPage.AttributeAsync(page, "data-value"));
        }
    }

    [Fact]
    public async Task Retains_focused_custom_widgets_that_opt_into_keystrokes()
    {
        var (session, page) = await ShadowFocusPage.OpenAsync();
        await using (session)
        {
            await ShadowFocusPage.FocusShadowAsync(page, Mode, "<div tabindex=\"0\">Custom widget</div>");
            await session.TypeTextAsync("a", replace: false, CancellationToken.None);
            Assert.Equal("1", await ShadowFocusPage.AttributeAsync(page, "data-keys"));
        }
    }
}
