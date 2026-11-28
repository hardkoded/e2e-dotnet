// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;
using static E2E.Tests.PressCharacters.PressHarness;

namespace E2E.Tests.PressCharacters;

/// <summary>
/// <c>describe.skipIf(!existsSync(firefox.executablePath()))('press with a character off the US keyboard layout, in Firefox')</c> of
/// upstream's <c>packages/web/tests/integration/press-characters.test.ts</c>. Without Firefox installed the test returns at once, as
/// upstream's <c>skipIf</c> skips it.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class PressWithACharacterOffTheUsKeyboardLayoutInFirefoxTests
{
    [Fact]
    public async Task Refuses_the_character_before_any_input_and_still_presses_a_US_key()
    {
        using var playwright = await Playwright.CreateAsync();
        if (!File.Exists(playwright.Firefox.ExecutablePath))
        {
            return;
        }

        await using var other = await PressHarness.StartAsync(BrowserName.Firefox);
        await other.OpenPageAsync();
        foreach (
            var attempt in new Func<Task>[]
            {
                () => other.PressAsync(Open, "é"),
                () => other.PressAsync(Open, "Control+é"),
                () => other.PressFocusedAsync("é"),
            }
        )
        {
            var error = await Assert.ThrowsAsync<EngineException>(attempt);
            Assert.Equal(EngineErrorCodes.UnsupportedCapability, error.Code);
        }

        Assert.Equal("", await other.ValueOfAsync(Keys));
        await other.PressAsync(Open, "a");
        Assert.Equal("a", await other.ValueOfAsync(Open));
        Assert.Equal("keydown:a;input:a;keyup:a;", await other.ValueOfAsync(Keys));
    }
}
