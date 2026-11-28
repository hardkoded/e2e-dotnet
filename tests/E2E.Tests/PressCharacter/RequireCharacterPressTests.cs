// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.PressCharacter;

/// <summary>
/// <c>describe('requireCharacterPress')</c> of upstream's <c>packages/web/tests/unit/press-character.test.ts</c>: which <c>press</c> keys the
/// engine sends itself, and on which browser.
/// </summary>
public sealed class RequireCharacterPressTests
{
    private static readonly BrowserName[] Browsers = [BrowserName.Chromium, BrowserName.Firefox, BrowserName.Webkit];

    [Theory]
    [InlineData(" ")]
    [InlineData("~")]
    [InlineData("a")]
    [InlineData("$")]
    [InlineData("+")]
    [InlineData("Enter")]
    [InlineData("Control+a")]
    [InlineData("Shift++")]
    [InlineData("Shift+Tab")]
    public void Leaves_key_to_Playwright_on_every_browser(string key)
    {
        foreach (var browser in Browsers)
        {
            Assert.Null(CharacterPressing.RequireCharacterPress(key, browser));
        }
    }

    [Theory]
    [InlineData("é")]
    [InlineData("ß")]
    [InlineData("€")]
    [InlineData("中")]
    [InlineData("😀")]
    [InlineData("¡")]
    public void Presses_key_itself_on_Chromium(string key)
    {
        var press = CharacterPressing.RequireCharacterPress(key, BrowserName.Chromium);
        Assert.Equal(key, press!.Character);
        Assert.Empty(press.Modifiers);
    }

    [Fact]
    public void Keeps_the_modifiers_in_the_order_written()
    {
        var press = CharacterPressing.RequireCharacterPress("ControlOrMeta+Alt+中", BrowserName.Chromium);
        Assert.Equal("中", press!.Character);
        Assert.Equal([KeyModifier.ControlOrMeta, KeyModifier.Alt], press.Modifiers);
    }

    [Theory]
    [InlineData("é", BrowserName.Firefox)]
    [InlineData("Control+é", BrowserName.Firefox)]
    [InlineData("中", BrowserName.Webkit)]
    public void Refuses_key_on_browser(string key, BrowserName browser)
    {
        var error = Assert.Throws<EngineException>(() => CharacterPressing.RequireCharacterPress(key, browser));
        Assert.Equal(EngineErrorCodes.UnsupportedCapability, error.Code);
    }
}
