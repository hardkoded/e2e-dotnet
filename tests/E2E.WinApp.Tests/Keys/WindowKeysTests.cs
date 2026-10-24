// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.WinApp;

namespace E2E.WinApp.Tests.Keys;

/// <summary>.NET-only: upstream has no Windows engine. The <c>press</c> grammar as the winapp library spells it.</summary>
public sealed class WindowKeysTests
{
    [Theory]
    [InlineData("Enter", "enter")]
    [InlineData("Escape", "esc")]
    [InlineData("Tab", "tab")]
    [InlineData("Backspace", "backspace")]
    [InlineData("Delete", "delete")]
    [InlineData("Insert", "insert")]
    [InlineData("Space", "space")]
    [InlineData("ArrowUp", "up")]
    [InlineData("ArrowDown", "down")]
    [InlineData("ArrowLeft", "left")]
    [InlineData("ArrowRight", "right")]
    [InlineData("Home", "home")]
    [InlineData("End", "end")]
    [InlineData("PageUp", "pageup")]
    [InlineData("PageDown", "pagedown")]
    [InlineData("F5", "f5")]
    [InlineData("Control+Enter", "ctrl+enter")]
    [InlineData("ControlOrMeta+a", "ctrl+a")]
    [InlineData("Meta+Tab", "win+tab")]
    [InlineData("Shift+Alt+F1", "shift+alt+f1")]
    [InlineData("Shift+A", "shift+A")]
    [InlineData("Shift++", "shift+vk=0xBB")]
    [InlineData("Control+ ", "ctrl+space")]
    public void Spells_a_named_key_or_a_chord_in_the_library_grammar(string key, string expected)
    {
        var keys = WindowKeys.ToWinApp(key);
        Assert.Equal(expected, keys.Text);
        Assert.False(keys.Verbatim);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("A")]
    [InlineData("+")]
    [InlineData("é")]
    [InlineData("€")]
    [InlineData(" ")]
    public void Types_a_bare_character_literally(string key)
    {
        var keys = WindowKeys.ToWinApp(key);
        Assert.Equal(key, keys.Text);
        Assert.True(keys.Verbatim);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Hyper+Q")]
    [InlineData("Shift")]
    [InlineData("Shift+Shift+a")]
    [InlineData("ab")]
    [InlineData("Control+")]
    [InlineData("Enter+Control")]
    [InlineData("\u0007")]
    public void Refuses_a_key_outside_the_grammar_as_unsupported(string key)
    {
        var error = Assert.Throws<EngineException>(() => WindowKeys.ToWinApp(key));
        Assert.Equal(EngineErrorCodes.UnsupportedCapability, error.Code);
        Assert.Contains("is not a key", error.Message, StringComparison.Ordinal);
    }
}
