// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.KeyboardShadowFocus;

/// <summary>The <c>it</c> of keyboard-shadow-focus.test.ts that sits outside any <c>describe</c>.</summary>
[Collection(BrowserCollection.Name)]
public sealed class KeyboardShadowFocusTests
{
    [Fact]
    public async Task Does_not_mistake_the_iframe_host_for_an_editable_control_when_its_button_has_focus()
    {
        var (session, page) = await ShadowFocusPage.OpenAsync();
        await using (session)
        {
            await page.EvaluateAsync(
                """
                async () => {
                  const frame = document.createElement('iframe');
                  const loaded = new Promise((resolve) => { frame.addEventListener('load', () => resolve(), { once: true }); });
                  frame.srcdoc = '<button id="stop">Stop</button>';
                  document.body.append(frame);
                  await loaded;
                  frame.contentDocument.getElementById('stop').focus();
                }
                """);

            var error = await Assert.ThrowsAsync<EngineException>(() => session.TypeTextAsync("lost", replace: false, CancellationToken.None));

            Assert.Equal("NOT_ACTIONABLE", error.Code);
        }
    }
}
